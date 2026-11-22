using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed class BoardProfile
    {
        internal BoardMatchOptions Options;
        internal bool ConfirmArmyMove = true;
        internal bool TutorialSeen;
        internal readonly Dictionary<int, BoardMatch> Matches = new Dictionary<int, BoardMatch>();
        internal BoardProfile(BoardGameId game) { Options = BoardMatchOptions.Default(game); }
    }

    internal sealed class BoardSaveStore
    {
        private const int Limit = 1024 * 1024;
        private const int MoveLimit = 10000;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        // This protocol contains only fixed ASCII keys, integers, booleans and arrays.
        private static readonly Regex Lexeme = new Regex(
            @"\G(?:[ \t\r\n]+|-?(?:0|[1-9][0-9]*)(?=[ \t\r\n,}\]])|true|false|""[a-zA-Z0-9-]+""|[{}\[\]:]|,(?![ \t\r\n]*[}\]]))",
            RegexOptions.CultureInvariant);
        private readonly BoardGameId game;
        private readonly string path;
        internal bool ProtectDamaged { get; private set; }

        internal BoardSaveStore(string directory, BoardGameId game)
        {
            this.game = game;
            path = Path.Combine(directory, BoardCatalog.Id(game) + "-v1.json");
        }

        internal BoardProfile Load(out string warning)
        {
            warning = null;
            ProtectDamaged = false;
            if (Read(path, out BoardProfile profile, out bool damaged)) return profile;
            if (Read(path + ".bak", out profile, out bool backupDamaged))
            {
                warning = "存档已从最近一次完整备份恢复";
                return profile;
            }
            ProtectDamaged = damaged || backupDamaged;
            if (ProtectDamaged) warning = "存档无法恢复，确认新局后将写入新进度";
            return new BoardProfile(game);
        }

        internal void AllowReplacement() { ProtectDamaged = false; }

        internal bool Save(BoardProfile profile)
        {
            if (ProtectDamaged) return false;
            try
            {
                var slots = new JArray();
                foreach (var slot in profile.Matches.OrderBy(entry => entry.Key))
                {
                    var match = slot.Value;
                    var moves = new JArray();
                    foreach (var move in match.History)
                        moves.Add(new JArray(move.From, move.To, move.Choice, new JArray(move.Path)));
                    if (moves.Count > MoveLimit) return false;
                    slots.Add(new JObject {
                        ["slot"] = slot.Key, ["options"] = EncodeOptions(match.Options),
                        ["first"] = new JArray(match.ArmyFirst), ["second"] = new JArray(match.ArmySecond), ["moves"] = moves,
                    });
                }
                var root = new JObject {
                    ["version"] = 1, ["game"] = BoardCatalog.Id(game), ["options"] = EncodeOptions(profile.Options),
                    ["confirmMove"] = profile.ConfirmArmyMove, ["tutorialSeen"] = profile.TutorialSeen, ["matches"] = slots,
                };
                byte[] bytes = Utf8.GetBytes(root.ToString(Formatting.None));
                if (bytes.Length > Limit) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, Read(path, out _, out _) ? path + ".bak" : null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception e) when (StorageError(e)) { return false; }
        }

        private bool Read(string source, out BoardProfile profile, out bool damaged)
        {
            profile = null;
            damaged = false;
            try
            {
                string json;
                using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > Limit) throw new FormatException();
                    using (var reader = new StreamReader(stream, Utf8, false)) json = reader.ReadToEnd();
                }
                for (int next = 0; next < json.Length;)
                {
                    var match = Lexeme.Match(json, next);
                    if (!match.Success || match.Index != next) throw new FormatException();
                    next += match.Length;
                }
                JObject value;
                using (var input = new StringReader(json))
                using (var reader = new JsonTextReader(input) { MaxDepth = 10, DateParseHandling = DateParseHandling.None })
                {
                    value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new FormatException();
                }
                CheckFields(value, 6);
                if (Integer(value["version"], 1, 1) != 1 || value["game"]?.Type != JTokenType.String ||
                    (string)value["game"] != BoardCatalog.Id(game)) throw new FormatException();
                var decoded = new BoardProfile(game) {
                    Options = DecodeOptions(value["options"]), ConfirmArmyMove = Boolean(value["confirmMove"]),
                    TutorialSeen = Boolean(value["tutorialSeen"]),
                };
                foreach (var token in Array(value["matches"], game == BoardGameId.Checkers ? 5 : 2))
                {
                    var slot = token as JObject;
                    CheckFields(slot, 5);
                    int id = Integer(slot["slot"], 0, game == BoardGameId.Checkers ? 4 : 1);
                    var options = DecodeOptions(slot["options"]);
                    if (id != options.Slot || decoded.Matches.ContainsKey(id)) throw new FormatException();
                    int[] first = Array(slot["first"], 30).Select(rank => Integer(rank, 0, 12)).ToArray();
                    int[] second = Array(slot["second"], 30).Select(rank => Integer(rank, 0, 12)).ToArray();
                    if ((game == BoardGameId.Army && (!ArmyGame.IsFormationValid(first) || !ArmyGame.IsFormationValid(second))) ||
                        (game != BoardGameId.Army && (first.Length != 0 || second.Length != 0))) throw new FormatException();
                    var match = new BoardMatch(game, options, first, second);
                    foreach (var encoded in Array(slot["moves"], MoveLimit))
                    {
                        var move = Array(encoded, 4);
                        if (move.Count != 4) throw new FormatException();
                        var route = Array(move[3], 120).Select(point => Integer(point, 0, 120)).ToArray();
                        if (!match.Play(new BoardMove(Integer(move[0], -2, 120), Integer(move[1], -2, 120), Integer(move[2], 0, 12), route)))
                            throw new FormatException("Illegal turn in board history.");
                    }
                    decoded.Matches.Add(id, match);
                }
                profile = decoded;
                return true;
            }
            catch (FileNotFoundException) { damaged = HasBlockedAncestor(source); return false; }
            catch (DirectoryNotFoundException) { damaged = HasBlockedAncestor(source); return false; }
            catch (Exception e) when (StorageError(e)) { damaged = true; return false; }
        }

        private static bool HasBlockedAncestor(string source)
        {
            try
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(source));
                while (!string.IsNullOrEmpty(parent))
                {
                    try { return (File.GetAttributes(parent) & FileAttributes.Directory) == 0; }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    parent = Path.GetDirectoryName(parent);
                }
                return false;
            }
            catch (Exception e) when (StorageError(e)) { return true; }
        }

        private static JObject EncodeOptions(BoardMatchOptions options) => new JObject {
            ["local"] = options.Local, ["difficulty"] = (int)options.Difficulty, ["seats"] = options.Seats,
            ["human"] = options.Human, ["flip"] = options.Flip,
        };

        private BoardMatchOptions DecodeOptions(JToken encoded)
        {
            var value = encoded as JObject;
            CheckFields(value, 5);
            var options = new BoardMatchOptions(Boolean(value["local"]), (BoardDifficulty)Integer(value["difficulty"], 0, 2),
                Integer(value["seats"], 2, 6), Integer(value["human"], 0, 5), Boolean(value["flip"]));
            if (!options.ValidFor(game)) throw new FormatException();
            return options;
        }

        private static void CheckFields(JObject value, int count)
        { if (value == null || value.Count != count) throw new FormatException(); }

        private static JArray Array(JToken value, int max)
        { if (!(value is JArray array) || array.Count > max) throw new FormatException(); return array; }

        private static int Integer(JToken value, int min, int max)
        {
            if (value?.Type != JTokenType.Integer || !int.TryParse(value.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                out int number) || number < min || number > max) throw new FormatException();
            return number;
        }

        private static bool Boolean(JToken value)
        { if (value?.Type != JTokenType.Boolean) throw new FormatException(); return (bool)value; }

        private static bool StorageError(Exception error) => error is IOException || error is UnauthorizedAccessException ||
            error is System.Security.SecurityException || error is NotSupportedException || error is ArgumentException ||
            error is FormatException || error is JsonException;
    }
}
