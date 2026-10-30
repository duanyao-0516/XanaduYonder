import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/import-idioms.py"
FONT = ROOT / "Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf"


def encoded(value):
    return json.dumps(value, ensure_ascii=False).encode("utf-8")


class ImportCliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.sources = self.base / "sources"
        self.sources.mkdir()
        common = {"word": "一心一意", "pinyin": "yī xīn yī yì", "explanation": "心思专一。"}
        payloads = {
            "jingmo.json": ("jingmo", {"data": [dict(common, id=1), dict(common, id=2)]}, 2),
            "xinhua.json": ("xinhua", [dict(common, example="无", derivation="无")], 1),
        }
        files = []
        for filename, (source, payload, count) in payloads.items():
            data = encoded(payload)
            (self.sources / filename).write_bytes(data)
            files.append({"source": source, "cacheName": filename, "sha256": hashlib.sha256(data).hexdigest(),
                          "expectedRecords": count, "url": "https://example.invalid/" + filename})
        self.manifest = self.base / "manifest.json"
        self.manifest.write_bytes(encoded({"schemaVersion": 1, "ruleVersion": "idiom-clean-v1",
            "files": files, "licenses": [],
            "font": {"path": str(FONT), "sha256": hashlib.sha256(FONT.read_bytes()).hexdigest()}}))
        self.exclusions = self.base / "exclusions.json"
        self.exclusions.write_bytes(encoded({"schemaVersion": 1, "entries": []}))

    def run_cli(self, output):
        return subprocess.run([sys.executable, "-B", str(SCRIPT), "--manifest", str(self.manifest),
            "--exclusions", str(self.exclusions), "--source-dir", str(self.sources), "--output", str(output)],
            cwd=ROOT, capture_output=True, text=True)

    def test_full_import_merges_internal_and_cross_source_duplicates(self):
        output = self.base / "result"
        result = self.run_cli(output)
        self.assertEqual(result.returncode, 0, result.stderr)
        audit = json.loads((output / "audit.json").read_text())
        self.assertEqual(audit["counts"]["inputRecords"], 3)
        self.assertEqual(audit["counts"]["uniqueWords"], 1)
        self.assertEqual(audit["counts"]["playableWords"], 1)
        entries = [json.loads(line) for line in (output / "catalog.jsonl").read_text().splitlines()]
        self.assertEqual(len(entries), 1)
        self.assertEqual(len(entries[0]["sourceRecords"]), 3)
        index = json.loads((output / "game-index.json").read_text())
        self.assertEqual(index["entries"][0]["word"], "一心一意")
        self.assertTrue((output / index["entries"][0]["detailFile"]).is_file())

    def test_export_includes_source_variants_and_all_readings(self):
        payload = {"data": [
            {"word": "当仁不让", "pinyin": " Dāng rén bù ràng ", "explanation": " 释义甲。 "},
            {"word": "一心一意", "pinyin": "bad", "explanation": None},
            {"word": "空前绝后", "pinyin": "bad", "explanation": None},
            {"word": "九回肠", "pinyin": "jiǔ huí cháng", "explanation": "非四字"}]}
        data = encoded(payload)
        (self.sources / "jingmo.json").write_bytes(data)
        manifest = json.loads(self.manifest.read_text())
        manifest["files"][0].update(sha256=hashlib.sha256(data).hexdigest(), expectedRecords=4)
        self.manifest.write_bytes(encoded(manifest))
        output = self.base / "result"
        result = self.run_cli(output)
        self.assertEqual(0, result.returncode, result.stderr)
        index = json.loads((output / "game-index.json").read_text())
        entries = {row["word"]: row for row in index["entries"]}
        self.assertEqual({"当仁不让", "一心一意", "空前绝后"}, set(entries))
        self.assertEqual([], entries["空前绝后"]["readings"])
        self.assertEqual("", entries["空前绝后"]["firstKey"])
        self.assertEqual("", entries["空前绝后"]["lastKey"])
        self.assertNotIn("", index["byFirstSyllable"])
        self.assertEqual(" Dāng rén bù ràng ", entries["当仁不让"]["pinyin"]["display"])
        self.assertEqual([{"firstKey": "yi", "lastKey": "yi"}], entries["一心一意"]["readings"])
        self.assertEqual("bad / yī xīn yī yì", entries["一心一意"]["pinyin"]["display"])
        details = json.loads((output / entries["一心一意"]["detailFile"]).read_text())["entries"]
        row = next(row for row in details if row["word"] == "一心一意")
        self.assertEqual(2, len(row.get("sourceVariants", [])))
        self.assertEqual("", row["sourceVariants"][0]["meaning"])
        self.assertEqual("心思专一。", row["sourceVariants"][1]["meaning"])
        self.assertEqual(entries["一心一意"]["senseId"], row["senseId"])

    def test_hash_mismatch_fails_before_publishing(self):
        (self.sources / "xinhua.json").write_text("[]")
        output = self.base / "result"
        result = self.run_cli(output)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("SHA256", result.stderr)
        self.assertFalse(output.exists())

    def test_generation_is_reproducible_and_safe_to_repeat(self):
        first, second = self.base / "first", self.base / "second"
        for output in (first, second, first):
            result = self.run_cli(output)
            self.assertEqual(result.returncode, 0, result.stderr)
        files1 = {str(p.relative_to(first)): p.read_bytes() for p in first.rglob("*") if p.is_file()}
        files2 = {str(p.relative_to(second)): p.read_bytes() for p in second.rglob("*") if p.is_file()}
        self.assertEqual(files1, files2)

    def test_existing_unknown_output_is_not_overwritten(self):
        output = self.base / "result"
        output.mkdir()
        marker = output / "user-file.txt"
        marker.write_text("keep this")
        result = self.run_cli(output)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(marker.read_text(), "keep this")
        self.assertEqual(sorted(p.name for p in output.iterdir()), ["user-file.txt"])

    def test_changed_generated_file_is_not_silently_overwritten(self):
        output = self.base / "result"
        result = self.run_cli(output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = output / "report.md"
        report.write_text("my annotations")
        result = self.run_cli(output)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("checksum mismatch", result.stderr)
        self.assertEqual(report.read_text(), "my annotations")

    def test_declared_record_count_is_checked_after_hash(self):
        manifest = json.loads(self.manifest.read_text())
        manifest["files"][0]["expectedRecords"] = 99
        self.manifest.write_bytes(encoded(manifest))
        output = self.base / "result"
        result = self.run_cli(output)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("record count mismatch", result.stderr)
        self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
