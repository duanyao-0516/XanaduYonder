#!/usr/bin/env python3
"""Content boundary and transactional publication tests; never modify live sources."""
import copy
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("sokoban_import", ROOT / "scripts/import-sokoban-content.py")
content = importlib.util.module_from_spec(spec)
spec.loader.exec_module(content)


class ContentImportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="sokoban-content-test-")
        self.directory = Path(self.temp.name)
        self.tutorials = content.read_json(ROOT / "Data/Sokoban/campaign/tutorials.json")

    def tearDown(self):
        self.temp.cleanup()

    def tutorial_input(self, value):
        path = self.directory / "tutorials.json"
        content.write_json(path, value)
        return path

    def test_closed_maps_and_full_legal_witnesses(self):
        campaign, proofs = content.build_campaign(self.tutorial_input(self.tutorials))
        self.assertEqual(108, len(campaign["levels"]))
        self.assertEqual(108, len({p["mapSha256"] for p in proofs}))
        self.assertEqual(4596, sum(p["moves"] for p in proofs))
        self.assertEqual(1203, sum(p["pushes"] for p in proofs))

    def test_rejects_duplicate_map_with_distinct_identity(self):
        self.tutorials["levels"][2]["rows"] = self.tutorials["levels"][1]["rows"]
        self.tutorials["levels"][2]["solution"] = self.tutorials["levels"][1]["solution"]
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            content.build_campaign(self.tutorial_input(self.tutorials))

    def test_prototype_fixture_cannot_drift(self):
        for index, name in [(0, "compact"), (7, "tall")]:
            with self.subTest(level_id=self.tutorials["levels"][index]["levelId"]):
                changed = copy.deepcopy(self.tutorials)
                changed["levels"][index]["rows"][1] = "# .    #"
                with self.assertRaisesRegex(ValueError, f"approved {name}"):
                    content.build_campaign(self.tutorial_input(changed))

    def test_prototype_fixtures_use_stable_ids_across_order_and_format_changes(self):
        compact, tall = self.tutorials["levels"][0], self.tutorials["levels"][7]
        prototype = "const fallbackLevels = [\n" + ",\n".join(
            "{rows: " + json.dumps(level["rows"], indent=2) + ",\n"
            + "title: 'UI label may change', levelId: '" + level["levelId"] + "'}"
            for level in [tall, compact]
        ) + "\n];\nconst unrelated = {levelId: 'tutorial-001', rows: ['wrong']};"
        self.assertEqual(
            {level["levelId"]: level["rows"] for level in [compact, tall]},
            content.prototype_fixture_rows(prototype),
        )

    def test_prototype_fixtures_reject_missing_ids_even_with_matching_rows_elsewhere(self):
        fixtures = [self.tutorials["levels"][0], self.tutorials["levels"][7]]
        for missing in fixtures:
            with self.subTest(level_id=missing["levelId"]):
                remaining = [level for level in fixtures if level is not missing]
                prototype = "const fallbackLevels = " + json.dumps(remaining) + ";\n"
                prototype += "const unrelated = " + json.dumps(missing) + ";"
                with self.assertRaisesRegex(ValueError, "Prototype fixture missing: " + missing["levelId"]):
                    content.prototype_fixture_rows(prototype)

    def test_prototype_fixtures_reject_duplicate_ids(self):
        fixtures = [self.tutorials["levels"][0], self.tutorials["levels"][7], self.tutorials["levels"][0]]
        prototype = "const fallbackLevels = " + json.dumps(fixtures) + ";"
        with self.assertRaisesRegex(ValueError, "Duplicate prototype fixture: tutorial-001"):
            content.prototype_fixture_rows(prototype)

    def test_witness_does_not_allow_blocked_or_post_victory_steps(self):
        for solution in ("L", "RR", "RX", ""):
            with self.subTest(solution=solution), self.assertRaises(ValueError):
                content.replay(["#####", "#@$.#", "#####"], solution)
        self.assertEqual(1, content.replay(["#####", "#@$.#", "#####"], "R")["pushes"])

    def test_significant_spaces_not_trimmed(self):
        with self.assertRaisesRegex(ValueError, "rectangle"):
            content.replay(["#####", "#@$.# ", "#####"], "R")
        self.assertNotEqual(content.map_hash(["# #"]), content.map_hash(["# # "]))

    def test_source_rows_are_checked_against_pinned_archive(self):
        original = content.read_json

        def tampered(path):
            value = original(path)
            if path.name == "verified-candidates.json":
                value["levels"][0]["rows"][1] = "###   .  #"
            return value

        with mock.patch.object(content, "read_json", side_effect=tampered), self.assertRaisesRegex(ValueError, "differ from upstream"):
            content.build_campaign(self.tutorial_input(self.tutorials))

    def test_source_identity_is_not_renamed(self):
        original = content.read_json

        def renamed(path):
            value = original(path)
            if path.name == "verified-candidates.json":
                value["levels"][0]["id"] = "renamed-source-zero"
            return value

        with mock.patch.object(content, "read_json", side_effect=renamed), self.assertRaisesRegex(ValueError, "renamed"):
            content.build_campaign(self.tutorial_input(self.tutorials))

    def test_duplicate_json_keys_rejected(self):
        path = self.directory / "duplicate.json"
        path.write_text('{"schemaVersion":1,"schemaVersion":1}')
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"):
            content.read_json(path)

    def test_same_version_level_id_map_is_immutable(self):
        published = self.directory / "published"
        published.mkdir()
        campaign, _ = content.build_campaign(self.tutorial_input(self.tutorials))
        content.write_json(published / "campaign.json", campaign)
        changed = copy.deepcopy(campaign)
        changed["levels"][1]["rows"][1] = "#    . #"
        with self.assertRaisesRegex(ValueError, "existing contentVersion/levelId"):
            content.unchanged_ids(changed, published)
        changed["contentVersion"] = "sokoban-campaign-v2"
        content.unchanged_ids(changed, published)

    def test_failed_import_preserves_good_release_byte_for_byte(self):
        published = self.directory / "release"
        shutil.copytree(ROOT / "Assets/Resources/Sokoban", published)
        before = content.tree_hashes(published)
        self.tutorials["levels"][1]["solution"] = "LLLLL"
        result = subprocess.run([sys.executable, str(ROOT / "scripts/import-sokoban-content.py"), "--tutorials", str(self.tutorial_input(self.tutorials)), "--output", str(published)], capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Blocked witness", result.stderr)
        self.assertEqual(before, content.tree_hashes(published))
        self.assertEqual([], list(self.directory.glob(".sokoban-import-*")))

    def test_same_version_cannot_remove_completed_record_identity(self):
        published = self.directory / "published"
        published.mkdir()
        campaign, _ = content.build_campaign(self.tutorial_input(self.tutorials))
        content.write_json(published / "campaign.json", campaign)
        changed = copy.deepcopy(campaign)
        changed["levels"][8]["levelId"] = "replacement-boxoban-id"
        with self.assertRaisesRegex(ValueError, "Catalog membership changed"):
            content.unchanged_ids(changed, published)
        changed["contentVersion"] = "sokoban-campaign-v2"
        content.unchanged_ids(changed, published)

    def test_failed_production_validation_keeps_previous_output(self):
        published, stage = self.directory / "published", self.directory / "stage"
        published.mkdir()
        stage.mkdir()
        (published / "sentinel").write_text("previous good release")
        before = content.tree_hashes(published)
        with mock.patch.object(content.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "production-replay")), self.assertRaises(subprocess.CalledProcessError):
            content.generate(stage, self.tutorial_input(self.tutorials), published)
        self.assertEqual(before, content.tree_hashes(published))

    def test_atomic_exchange_retains_old_release_until_swap(self):
        published, stage = self.directory / "published", self.directory / "stage"
        published.mkdir()
        stage.mkdir()
        (published / "old").write_text("old")
        (stage / "new").write_text("new")
        content.atomic_publish(stage, published)
        self.assertEqual({"new"}, {p.name for p in published.iterdir()})
        self.assertEqual("old", (stage / "old").read_text())

    def test_failed_atomic_exchange_preserves_old_release(self):
        published, stage = self.directory / "published", self.directory / "stage"
        published.mkdir()
        stage.mkdir()
        (published / "old").write_text("old")
        (stage / "new").write_text("new")
        library = mock.Mock()
        library.renamex_np.return_value = -1
        library.renameat2.return_value = -1
        with mock.patch.object(content.ctypes, "CDLL", return_value=library), self.assertRaises(OSError):
            content.atomic_publish(stage, published)
        self.assertEqual("old", (published / "old").read_text())
        self.assertEqual("new", (stage / "new").read_text())

    def test_manifest_covers_every_published_payload(self):
        published = ROOT / "Assets/Resources/Sokoban"
        manifest = content.read_json(published / "manifest.json")
        actual = content.tree_hashes(published)
        del actual["manifest.json"]
        self.assertEqual(actual, {entry["path"]: entry["sha256"] for entry in manifest["files"]})
        for entry in manifest["media"]:
            self.assertEqual((ROOT / entry["source"]).read_bytes(), (published / entry["path"]).read_bytes())

    def test_canonical_unity_metadata_retains_full_normalized_fields_and_ids(self):
        templates = ROOT / content.SPRITE_META_SOURCE
        manifest = content.read_json(templates / "manifest.json")
        self.assertEqual("6000.3.27f1", manifest["unityVersion"])
        self.assertEqual(7, len(manifest["files"]))
        identifiers = set()
        for entry in manifest["files"]:
            path = entry["path"][:-5]
            emitted = content.meta(path).encode("utf-8")
            self.assertEqual((templates / entry["path"]).read_bytes(), emitted)
            self.assertEqual((ROOT / "Assets/Resources/Sokoban" / entry["path"]).read_bytes(), emitted)
            self.assertEqual(entry["sha256"], content.digest(emitted))
            text = emitted.decode("utf-8")
            for field in ["internalIDToNameTable:", "spriteCustomMetadata:", "spriteGenerateFallbackPhysicsShape: 1", "spriteMode: 1", "textureType: 8", "enableMipMap: 0", "buildTarget: DefaultTexturePlatform", "buildTarget: Standalone", "buildTarget: Android"]:
                self.assertIn(field, text)
            identifier = text.split("guid: ", 1)[1].splitlines()[0]
            self.assertEqual(32, len(identifier))
            identifiers.add(identifier)
            self.assertRegex(text, r"\n      213: -?[1-9][0-9]+\n")
            self.assertRegex(text, r"\n      spriteID: [0-9a-f]{32}\n")
            self.assertRegex(text, r"\n      internalID: -?[1-9][0-9]+\n")
        self.assertEqual(7, len(identifiers))

    def test_canonical_unity_metadata_hash_rejects_dropped_fields(self):
        templates = self.directory / "unity-importer"
        shutil.copytree(ROOT / content.SPRITE_META_SOURCE, templates)
        path = templates / "Sprites/wall.png.meta"
        path.write_text(path.read_text().replace("    buildTarget: Android\n", ""))
        with self.assertRaisesRegex(ValueError, "Canonical Unity metadata hash mismatch"):
            content.sprite_meta("Sprites/wall.png", templates)


if __name__ == "__main__":
    unittest.main(verbosity=2)
