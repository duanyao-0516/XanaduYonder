using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.LinkMatch;
using XanaduYonder.Runtime.LinkMatch;

internal static partial class Program
{
    private static void ExpandedChecks()
    {
        Check("immutable production-v1 fixtures retain their checked-in byte hashes", () => {
            HashFixture("primary", "b6f06178aba7e6fbbab04789cdbe694913830cfb9922a68d093ea92102d68dba");
            HashFixture("backup", "ec682dc1aaa3751c5de6cdf5d742ca3efe4e8dcf0578549d7514956486fc9e45");
        });
        Check("explicit legacy decoder accepts original fixtures and rejects current JSON", () => {
            foreach (string name in new[] { "primary", "backup" }) { Assert(LinkMatchSaveCodec.TryDecodeLegacyV1(Legacy(name), out var data, out var reason), reason); EqualMigrated(JObject.Parse(Legacy(name)), data); }
            Assert(!LinkMatchSaveCodec.TryDecodeLegacyV1(LinkMatchSaveCodec.Encode(Fixture()), out var rejected, out var error) && rejected == null && error != null);
        });
        Check("real legacy primary migrates all identities and fields into v2 atomically", () => WithDirectory(dir => {
            string legacy = Path.Combine(dir, "link-match-v1.json"), original = Legacy("primary"); File.WriteAllText(legacy, original);
            var store = new LinkMatchSaveStore(dir); var migrated = store.Load(out var warning);
            Assert(migrated != null, "Valid production v1 progress was not migrated"); Assert(warning == null, warning);
            Assert(File.Exists(store.SavePath) && !File.Exists(store.SavePath + ".tmp")); Assert(File.ReadAllText(legacy) == original);
            EqualMigrated(JObject.Parse(original), migrated); Assert(store.Load(out warning) != null && warning == null);
        }));
        Check("legacy corrupted primary migrates valid backup and preserves both originals", () => WithDirectory(dir => {
            string legacy = Path.Combine(dir, "link-match-v1.json"), backup = Legacy("backup"); File.WriteAllText(legacy, "{broken"); File.WriteAllText(legacy + ".bak", backup);
            var store = new LinkMatchSaveStore(dir); var migrated = store.Load(out var warning);
            Assert(migrated != null && warning != null); EqualMigrated(JObject.Parse(backup), migrated);
            Assert(File.ReadAllText(legacy) == "{broken" && File.ReadAllText(legacy + ".bak") == backup);
            Assert(Directory.GetFiles(dir, "link-match-v1.json.corrupt-*").Length == 1);
        }));
        Check("existing corrupt current saves cannot roll back to valid legacy progress", () => {
            foreach (string suffix in new[] { "", ".bak" }) WithDirectory(dir => {
                File.WriteAllText(Path.Combine(dir, "link-match-v1.json"), Legacy("primary"));
                var store = new LinkMatchSaveStore(dir); File.WriteAllText(store.SavePath + suffix, "{bad-current");
                Assert(store.Load(out var warning) == null && warning != null, "Existing invalid v2 rolled back to v1");
                Assert(!File.Exists(store.SavePath) || File.ReadAllText(store.SavePath) == "{bad-current");
            });
        });
        Check("current backup wins over legacy when current primary is absent or corrupt", () => {
            foreach (bool corruptPrimary in new[] { false, true }) WithDirectory(dir => {
                var store = new LinkMatchSaveStore(dir); var current = Fixture(); current.SelectedLevelId = "level-05";
                File.WriteAllText(store.SavePath + ".bak", LinkMatchSaveCodec.Encode(current));
                if (corruptPrimary) File.WriteAllText(store.SavePath, "broken");
                File.WriteAllText(Path.Combine(dir, "link-match-v1.json"), Legacy("primary"));
                var loaded = store.Load(out var warning); Assert(loaded != null && warning != null && loaded.SelectedLevelId == "level-05");
            });
        });
        Check("migration write failure returns all live progress and can retry without recounting", () => WithDirectory(dir => {
            var store = new LinkMatchSaveStore(dir); File.WriteAllText(Path.Combine(dir, "link-match-v1.json"), Legacy("primary"));
            Directory.CreateDirectory(store.SavePath + ".tmp"); var live = store.Load(out var warning);
            Assert(live != null && store.MigrationPending && !string.IsNullOrEmpty(warning), "Failed migration discarded the valid in-memory progress");
            EqualMigrated(JObject.Parse(Legacy("primary")), live); Assert(!File.Exists(store.SavePath));
            var profile = new LinkMatchProfile(live); var terminal = new LinkMatchSession(profile.GetSlot(LinkMatchMode.Relaxed, "level-01")); profile.Capture(terminal);
            Eq(profile.GetRecord(LinkMatchMode.Relaxed, "level-01").Wins, 1);
            Directory.Delete(store.SavePath + ".tmp"); Assert(store.TrySave(profile.Export(), out var error), error); Assert(!store.MigrationPending);
            var restored = store.Load(out warning); Assert(restored != null && warning == null); EqualMigrated(JObject.Parse(Legacy("primary")), restored);
        }));
        Check("legacy required null types identity and old slot placement remain strict", () => {
            var original = JObject.Parse(Legacy("primary"));
            foreach (string path in new[] { "", "slots[3]", "records[3]" }) {
                var part = path == "" ? original : (JObject)original.SelectToken(path);
                foreach (var property in part.Properties()) { var copy = (JObject)original.DeepClone(); (path == "" ? copy : (JObject)copy.SelectToken(path)).Remove(property.Name); RejectLegacyFile(copy.ToString()); }
                var extra = (JObject)original.DeepClone(); (path == "" ? extra : (JObject)extra.SelectToken(path))["unknown"] = 1; RejectLegacyFile(extra.ToString());
            }
            foreach (string path in new[] { "schemaVersion", "activitySequence", "slots[3].columns", "slots[3].hintsUsed", "slots[3].randomState", "records[3].wins" })
                foreach (JToken value in new JToken[] { JValue.CreateNull(), new JValue("1"), new JValue(1.5) }) RejectLegacyMutation(path, value);
            foreach (string path in new[] { "selectedLevelId", "slots[3].levelId", "records[3].levelId" }) RejectLegacyMutation(path, new JValue("level-04"));
            RejectLegacyMutation("schemaVersion", new JValue(2)); RejectLegacyMutation("contentVersion", new JValue("2"));
            RejectLegacyMutation("slots[3].tiles", new JArray(Enumerable.Repeat(0, 49)));
            RejectLegacyMutation("slots[3].mode", new JValue("Relaxed")); RejectLegacyMutation("records[3].mode", new JValue("Relaxed"));
            var swapped = (JObject)original.DeepClone(); var first = swapped["slots"][3].DeepClone(); swapped["slots"][3] = swapped["slots"][4].DeepClone(); swapped["slots"][4] = first; RejectLegacyFile(swapped.ToString());
            RejectLegacyMutation("slots", new JArray()); RejectLegacyMutation("records", JValue.CreateNull());
            RejectLegacyMutation("slots[3].resultRecorded", new JValue(true)); RejectLegacyMutation("slots[3].remainingSeconds", new JValue(0));
        });
        Check("current parser does not silently accept legacy schema or old six-slot shapes", () => {
            Reject(Legacy("primary")); var root = JObject.Parse(Legacy("primary")); root["schemaVersion"] = 2; root["contentVersion"] = "2"; Reject(root.ToString());
            root = JObject.Parse(LinkMatchSaveCodec.Encode(Fixture())); root["schemaVersion"] = 1; Reject(root.ToString());
        });
        Check("192-cell sessions preserve clock aids RNG restart and final outcome", () => {
            var profile = new LinkMatchProfile(); var active = profile.Start(LinkMatchMode.Challenge, "level-05", 192);
            active.Enter(); active.Tick(53.25); active.TryHint(); active.TryShuffle(); profile.Capture(active);
            var other = profile.Start(LinkMatchMode.Relaxed, "level-04", 96); var originalId = active.AttemptId;
            var resumed = profile.Start(LinkMatchMode.Challenge, "level-05", 999); Assert(resumed.AttemptId == originalId); Eq(resumed.Game.Remaining, 192);
            Eq(resumed.ElapsedSeconds, 53.25); Eq(resumed.RemainingSeconds, 546.75); Eq(resumed.HintsUsed, 1); Eq(resumed.ShufflesUsed, 1);
            var data = resumed.Export(); for (int i = 0; i < data.Tiles.Length; i++) data.Tiles[i] = -1; data.Tiles[0] = data.Tiles[1] = 47;
            var final = new LinkMatchSession(data); final.Enter(); Assert(final.TryMatch(0, 1, out var path)); profile.Capture(final); profile.Capture(final);
            var restored = new LinkMatchProfile(Decode(LinkMatchSaveCodec.Encode(profile.Export()))); Eq(restored.GetRecord(LinkMatchMode.Challenge, "level-05").Wins, 1);
            Assert(restored.GetLatestActive().AttemptId == other.AttemptId); var fresh = restored.Start(LinkMatchMode.Challenge, "level-05", 5);
            Assert(fresh.AttemptId != originalId && fresh.Game.Remaining == 192); Assert(restored.GetSlot(LinkMatchMode.Relaxed, "level-04").AttemptId == other.AttemptId);
        });
        Check("v2 oversized board and invalid appended icon IDs are rejected", () => {
            var profile = new LinkMatchProfile(); profile.Start(LinkMatchMode.Relaxed, "level-05", 48); var original = JObject.Parse(LinkMatchSaveCodec.Encode(profile.Export()));
            foreach (JToken bad in new JToken[] { new JValue(48), new JValue(-2), new JValue("47"), JValue.CreateNull() }) { var copy = (JObject)original.DeepClone(); copy["slots"][4]["tiles"][0] = bad; Reject(copy.ToString()); }
            original["slots"][4]["tiles"] = new JArray(Enumerable.Repeat(0, 193)); Reject(original.ToString());
        });
    }
    private static string Legacy(string name) { return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-v1-" + name + ".json")); }
    private static void HashFixture(string name, string expected) { using (var sha = SHA256.Create()) Assert(BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Legacy(name)))).Replace("-", "").ToLowerInvariant() == expected, "Immutable legacy fixture changed"); }
    private static void RejectLegacyMutation(string path, JToken value) { var root = JObject.Parse(Legacy("primary")); root.SelectToken(path).Replace(value); RejectLegacyFile(root.ToString()); }
    private static void RejectLegacyFile(string json) { WithDirectory(dir => { var store = new LinkMatchSaveStore(dir); File.WriteAllText(Path.Combine(dir, "link-match-v1.json"), json); Assert(store.Load(out var warning) == null && warning != null, "Invalid legacy input was accepted or ignored silently"); Assert(!File.Exists(store.SavePath)); }); }
    private static void EqualMigrated(JObject old, LinkMatchSaveData actual)
    {
        var current = JObject.Parse(LinkMatchSaveCodec.Encode(actual)); Eq(actual.Slots.Length, 10); Eq(actual.Records.Length, 10);
        foreach (string field in new[] { "selectedMode", "selectedLevelId", "activitySequence" }) Assert(JToken.DeepEquals(old[field], current[field]), "Migration changed " + field);
        for (int i = 0; i < 6; i++) { int mapped = i < 3 ? i : i + 2; Assert(JToken.DeepEquals(old["slots"][i], current["slots"][mapped]), "Migration changed legacy slot " + i); Assert(JToken.DeepEquals(old["records"][i], current["records"][mapped]), "Migration changed legacy record " + i); }
        foreach (int index in new[] { 3, 4, 8, 9 }) { Assert(actual.Slots[index] == null); Eq(actual.Records[index].Wins, 0); Eq(actual.Records[index].Losses, 0); Assert(actual.Records[index].BestAttemptId == null); }
        Assert(actual.ContentVersion == "2"); Eq((int)current["schemaVersion"], 2);
    }
}
