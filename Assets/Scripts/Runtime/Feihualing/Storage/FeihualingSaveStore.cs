using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;
using static XanaduYonder.Runtime.Idiom.IdiomStrictJson;

namespace XanaduYonder.Runtime.Feihualing
{
    public sealed class FeihualingProfile
    {
        public const int SlotCount = 24;
        public const int MaximumDraftLength = 100;
        public static int SlotFor(PoetryPlayers players, PoetryGoal goal, PoetryRule rule) => ((int)players * 3 + (int)goal) * 4 + (int)rule;
        public int SelectedSlot { get; set; }
        public bool AutoCompanion { get; set; }
        public string[] Drafts { get; } = new string[SlotCount];
        public string[] SelectedCards { get; } = new string[SlotCount];
        public PoetrySnapshot[] Matches { get; } = new PoetrySnapshot[SlotCount];
        private readonly SortedDictionary<string,long> best = new SortedDictionary<string,long>(StringComparer.Ordinal);
        internal IEnumerable<KeyValuePair<string,long>> Best => best;
        public long BestMilliseconds(string cardId) => cardId != null && best.TryGetValue(cardId, out long value) ? value : 0;
        public void RecordBest(string cardId, long elapsedMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(cardId) || elapsedMilliseconds < 1 || elapsedMilliseconds > PoetrySession.SoloMilliseconds)
                throw new ArgumentException("Invalid personal record.");
            if (!best.TryGetValue(cardId, out long previous) || elapsedMilliseconds < previous) best[cardId] = elapsedMilliseconds;
        }
    }

    public sealed class FeihualingSaveStore : IdiomFileStore<FeihualingProfile>
    {
        private readonly PoetryCatalog catalog;
        private readonly string directory;
        public FeihualingSaveStore(string directory, PoetryCatalog catalog) : base(directory, "feihualing-v4.json", FeihualingSaveCodec.MaximumBytes,
            json => FeihualingSaveCodec.Decode(json, catalog), data => FeihualingSaveCodec.Encode(data, catalog), "飞花令存档")
        {
            this.catalog = catalog;
            this.directory = directory;
        }
        protected override bool IsReady => catalog != null;
        protected override string RecoveryDirectoryName => "feihualing-recovery";
        protected override bool PreserveCorruptPrimaryAfterBackup => true;

        public FeihualingProfile LoadProfile(out string warning)
        {
            var profile = Load(out warning);
            if (LoadState != IdiomLoadState.Missing) return profile;

            foreach (string filename in new[] { "feihualing-v3.json", "feihualing-v2.json" })
            {
                var previous = new PriorStore(directory, filename, json => FeihualingSaveCodec.Decode(json, catalog));
                profile = previous.Load(out _);
                if (profile != null)
                {
                    warning = "已继承上一版对局和草稿。原存档和竞速纪录已保留。";
                    if (!TrySave(profile, out var previousSaveError)) warning += previousSaveError;
                    return profile;
                }
                if (previous.LoadState != IdiomLoadState.Missing)
                {
                    warning = "上一版飞花令存档已保留，可使用新诗库开局。";
                    return null;
                }
            }

            var legacy = new PriorStore(directory, "feihualing-v1.json", json => FeihualingSaveCodec.DecodeLegacyPreferences(json, catalog));
            profile = legacy.Load(out _);
            if (profile == null)
            {
                if (legacy.LoadState != IdiomLoadState.Missing)
                    warning = "旧版飞花令存档已保留，可使用新诗库开局。";
                return null;
            }

            warning = "已继承旧版选项和草稿，原对局与成绩保留在旧版存档。";
            if (!TrySave(profile, out var saveError)) warning += saveError;
            return profile;
        }

        private sealed class PriorStore : IdiomFileStore<FeihualingProfile>
        {
            public PriorStore(string directory, string filename, Func<string, FeihualingProfile> decode)
                : base(directory, filename, FeihualingSaveCodec.MaximumBytes, decode,
                    _ => throw new NotSupportedException(), "旧版飞花令存档") { }
        }
    }

    internal static class FeihualingSaveCodec
    {
        internal const int MaximumBytes = 8 * 1024 * 1024;
        private const int MaxHistory = 100000;
        private const int MaxLineId = 449999;
        private const int MaxDraft = FeihualingProfile.MaximumDraftLength;
        private const string GameId = "feihualing";
        private const int SchemaVersion = 2;
        private const string InitialCatalog = "feihualing-v2-fc0a511ef50a59044028dee5";
        private const string FirstSupplementCatalog = "feihualing-v2-c7e498a4832a31f2219067de";
        private const string AuditedCatalog = "feihualing-v2-ac119e75542abe64706aea35";
        private const int InitialLineCount = 253241;
        private const int FirstSupplementLineCount = 253257;

        internal static FeihualingProfile Decode(string json, PoetryCatalog catalog)
        {
            var root = Read(json, MaximumBytes);
            if (Int(root["schemaVersion"], int.MaxValue) != SchemaVersion || Text(root["gameId"], 64) != GameId)
                throw new IdiomVersionException();
            var data = Object(root, "schemaVersion", "gameId", "corpus", "selected", "slots", "best", "autoCompanion");
            string savedCatalog = Text(data["corpus"], 96);
            int previousLineLimit = 0;
            if (savedCatalog != catalog.Version)
            {
                previousLineLimit = PreviousLineCount(savedCatalog);
                if (previousLineLimit == 0 || !IsValidLineLimit(catalog, previousLineLimit)) throw new IdiomVersionException();
            }
            var profile = new FeihualingProfile {
                SelectedSlot = Int(data["selected"], FeihualingProfile.SlotCount - 1),
                AutoCompanion = Boolean(data["autoCompanion"])
            };
            var slots = IdiomStrictJson.Array(data["slots"], FeihualingProfile.SlotCount);
            if (slots.Count != FeihualingProfile.SlotCount) throw new FormatException("Incomplete poetry slots.");
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = Object(slots[i], "draft", "card", "match");
                profile.Drafts[i] = Text(slot["draft"], MaxDraft, true);
                string cardId = Text(slot["card"], 64, true);
                profile.SelectedCards[i] = cardId;
                if (slot["match"].Type != JTokenType.Null) profile.Matches[i] = ReadMatch(slot["match"], catalog, previousLineLimit);
                ValidateSlot(i, cardId, profile.Matches[i], catalog);
            }
            var records = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in IdiomStrictJson.Array(data["best"], catalog.Cards.Length))
            {
                var row = IdiomStrictJson.Array(token, 2);
                if (row.Count != 2) throw new FormatException("Bad record format.");
                string id = Text(row[0], 64);
                if (!records.Add(id) || catalog.FindCard(id) == null) throw new FormatException("Invalid record card.");
                long elapsed = Integer(row[1], 1, PoetrySession.SoloMilliseconds);
                if (previousLineLimit == 0) profile.RecordBest(id, elapsed);
            }
            return profile;
        }

        internal static string Encode(FeihualingProfile profile, PoetryCatalog catalog)
        {
            if (profile == null || profile.SelectedSlot < 0 || profile.SelectedSlot >= FeihualingProfile.SlotCount) throw new FormatException("Invalid poetry profile.");
            var slots = new JArray();
            for (int i = 0; i < FeihualingProfile.SlotCount; i++)
            {
                var match = profile.Matches[i];
                string cardId = profile.SelectedCards[i] ?? "";
                string draft = profile.Drafts[i] ?? "";
                ValidateText(draft, MaxDraft, true);
                ValidateText(cardId, 64, true);
                ValidateSlot(i, cardId, match, catalog);
                if (match != null && (!IsValidLineLimit(catalog, match.AnswerLineLimit) ||
                    !PoetrySession.TryRestore(catalog, match, 0, out _)))
                    throw new FormatException("Invalid session snapshot.");
                slots.Add(new JObject { ["draft"] = draft, ["card"] = cardId,
                    ["match"] = match == null ? JValue.CreateNull() : (JToken)WriteMatch(match) });
            }
            var best = new JArray();
            foreach (var record in profile.Best)
            {
                if (catalog.FindCard(record.Key) == null) throw new FormatException("Unknown record card.");
                best.Add(new JArray(record.Key, record.Value));
            }
            return Write(new JObject { ["schemaVersion"] = SchemaVersion, ["gameId"] = GameId, ["corpus"] = catalog.Version,
                ["selected"] = profile.SelectedSlot, ["slots"] = slots, ["best"] = best,
                ["autoCompanion"] = profile.AutoCompanion }, MaximumBytes);
        }

        internal static FeihualingProfile DecodeLegacyPreferences(string json, PoetryCatalog catalog)
        {
            const int legacySlotCount = 16;
            var data = Object(Read(json, MaximumBytes), "schemaVersion", "gameId", "corpus", "selected", "slots", "best");
            Identity(data, GameId);
            Text(data["corpus"], 96);
            var slots = IdiomStrictJson.Array(data["slots"], legacySlotCount);
            if (slots.Count != legacySlotCount) throw new FormatException("Incomplete legacy poetry slots.");
            var profile = new FeihualingProfile { SelectedSlot = LegacySlot(Int(data["selected"], legacySlotCount - 1)) };
            for (int index = 0; index < slots.Count; index++)
            {
                var slot = Object(slots[index], "draft", "card", "match");
                string draft = Text(slot["draft"], MaxDraft, true);
                string cardId = Text(slot["card"], 64, true);
                var card = catalog.FindCard(cardId);
                if (cardId.Length != 0 && (card == null || (int)card.Rule != index % 4)) continue;
                int newSlot = LegacySlot(index);
                profile.SelectedCards[newSlot] = cardId;
                profile.Drafts[newSlot] = draft;
            }
            return profile;
        }

        private static int LegacySlot(int index)
        {
            var players = (PoetryPlayers)(index / 8);
            var goal = (index / 4) % 2 == 0 ? PoetryGoal.Learning : PoetryGoal.Speed;
            return FeihualingProfile.SlotFor(players, goal, (PoetryRule)(index % 4));
        }

        private static void ValidateSlot(int slot, string selectedCard, PoetrySnapshot match, PoetryCatalog catalog)
        {
            if (selectedCard.Length != 0)
            {
                var card = catalog.FindCard(selectedCard);
                if (card == null || (int)card.Rule != slot % 4) throw new FormatException("Card in wrong slot.");
            }
            if (match == null) return;
            var matchCard = catalog.FindCard(match.CardId);
            if (matchCard == null || FeihualingProfile.SlotFor(match.Players, match.Goal, matchCard.Rule) != slot)
                throw new FormatException("Match in wrong slot.");
        }

        private static int PreviousLineCount(string version)
        {
            if (version == InitialCatalog) return InitialLineCount;
            if (version == FirstSupplementCatalog) return FirstSupplementLineCount;
            return 0;
        }

        private static bool IsValidLineLimit(PoetryCatalog catalog, int limit)
        {
            if (limit == 0) return true;
            if (limit >= catalog.Lines.Length) return false;
            return (catalog.Version == AuditedCatalog && (limit == InitialLineCount || limit == FirstSupplementLineCount)) ||
                (catalog.Version == FirstSupplementCatalog && limit == InitialLineCount);
        }

        private static PoetrySnapshot ReadMatch(JToken token, PoetryCatalog catalog, int previousLineLimit)
        {
            var source = token as JObject ?? throw new FormatException("Invalid poetry match.");
            var fields = new List<string> { "rules", "card", "players", "goal", "phase", "outcome", "first", "player",
                "position", "round", "winner", "elapsed", "left", "history", "rounds", "exposed", "hints", "hintsShown" };
            var competitionHints = source["competitionHints"];
            if (competitionHints != null) fields.Add("competitionHints");
            var lineLimit = source["answerLineLimit"];
            if (lineLimit != null) fields.Add("answerLineLimit");
            var row = Object(source, fields.ToArray());
            int rules = Int(row["rules"], int.MaxValue);
            if (rules != PoetrySession.RulesVersion) throw new IdiomVersionException();
            int limit = lineLimit == null ? 0 : Int(lineLimit, MaxLineId);
            if (previousLineLimit != 0)
            {
                if (limit > previousLineLimit) throw new FormatException("The match exceeds its preceding catalog.");
                if (limit == 0) limit = previousLineLimit;
            }
            if (!IsValidLineLimit(catalog, limit)) throw new FormatException("Invalid poetry line limit.");
            var entries = new List<PoetryEntry>();
            foreach (var entry in IdiomStrictJson.Array(row["history"], MaxHistory))
            {
                var values = IdiomStrictJson.Array(entry, 7);
                if (values.Count != 7) throw new FormatException("Invalid answer record.");
                entries.Add(new PoetryEntry { LineId = Int(values[0], MaxLineId), Input = Text(values[1], MaxDraft),
                    Round = Int(values[2], PoetrySession.MaximumRounds), Player = Int(values[3], 1), Assisted = Boolean(values[4]),
                    Companion = Boolean(values[5]), ElapsedMilliseconds = Integer(values[6], 0, long.MaxValue) });
            }
            var rounds = new List<PoetryRoundResult>();
            foreach (var round in IdiomStrictJson.Array(row["rounds"], PoetrySession.MaximumRounds))
            {
                var values = IdiomStrictJson.Array(round, 3);
                if (values.Count != 3) throw new FormatException("Invalid poetry round result.");
                rounds.Add(new PoetryRoundResult { Round = Int(values[0], PoetrySession.MaximumRounds), Winner = (int)Integer(values[1], -1, 1),
                    Outcome = (PoetryOutcome)Int(values[2], (int)PoetryOutcome.Abandoned) });
            }
            var result = new PoetrySnapshot
            {
                CorpusVersion = catalog.Version, RulesVersion = rules, CardId = Text(row["card"], 64),
                Players = (PoetryPlayers)Int(row["players"], 1), Goal = (PoetryGoal)Int(row["goal"], (int)PoetryGoal.Speed),
                Phase = (PoetryPhase)Int(row["phase"], (int)PoetryPhase.Finished),
                Outcome = (PoetryOutcome)Int(row["outcome"], (int)PoetryOutcome.Abandoned),
                FirstPlayer = Int(row["first"], 1), CurrentPlayer = Int(row["player"], 1), Position = Int(row["position"], 7),
                Round = Int(row["round"], PoetrySession.MaximumRounds), Winner = (int)Integer(row["winner"], -1, 1),
                RoundElapsedMilliseconds = Integer(row["elapsed"], 0, long.MaxValue), RemainingMilliseconds = Integer(row["left"], -1, PoetrySession.SoloMilliseconds),
                History = entries.ToArray(), Rounds = rounds.ToArray(), ExposedGroups = ReadIds(row["exposed"], MaxHistory, int.MaxValue),
                HintLines = ReadIds(row["hints"], 3, MaxLineId), HintsShown = Boolean(row["hintsShown"]),
                CompetitionHintLines = competitionHints == null ? System.Array.Empty<int>() : ReadIds(competitionHints, PoetrySession.CompetitionHintLimit, MaxLineId),
                AnswerLineLimit = limit
            };
            if (!PoetrySession.TryRestore(catalog, result, 0, out _)) throw new FormatException("Poetry session failed validation.");
            return result;
        }

        private static int[] ReadIds(JToken node, int count, int max)
        {
            var rows = IdiomStrictJson.Array(node, count);
            var result = new int[rows.Count];
            for (int i = 0; i < result.Length; i++) result[i] = Int(rows[i], max);
            return result;
        }

        private static JObject WriteMatch(PoetrySnapshot match)
        {
            var history = new JArray();
            foreach (var item in match.History) history.Add(new JArray(item.LineId, item.Input, item.Round, item.Player, item.Assisted, item.Companion, item.ElapsedMilliseconds));
            var rounds = new JArray();
            foreach (var result in match.Rounds) rounds.Add(new JArray(result.Round, result.Winner, (int)result.Outcome));
            return new JObject
            {
                ["rules"] = match.RulesVersion, ["card"] = match.CardId, ["players"] = (int)match.Players, ["goal"] = (int)match.Goal,
                ["phase"] = (int)match.Phase, ["outcome"] = (int)match.Outcome, ["first"] = match.FirstPlayer,
                ["player"] = match.CurrentPlayer, ["position"] = match.Position, ["round"] = match.Round, ["winner"] = match.Winner,
                ["elapsed"] = match.RoundElapsedMilliseconds, ["left"] = match.RemainingMilliseconds, ["history"] = history,
                ["rounds"] = rounds, ["exposed"] = new JArray(match.ExposedGroups), ["hints"] = new JArray(match.HintLines),
                ["hintsShown"] = match.HintsShown, ["competitionHints"] = new JArray(match.CompetitionHintLines),
                ["answerLineLimit"] = match.AnswerLineLimit
            };
        }

        private static int Int(JToken value, int max) => (int)Integer(value, 0, max);
    }
}
