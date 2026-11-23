"""Desktop contract checks. Run python3 -m unittest discover -s Data/Feihualing."""
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import unittest


ROOT = Path(__file__).resolve().parents[2]
IMPORTER_PATH = ROOT / "scripts/import-feihualing.py"
CATALOG_PATH = Path(os.environ.get("FEIHUALING_TEST_CATALOG", ROOT / "Assets/Resources/Feihualing/catalog.bytes"))
AUDIT_OUTPUT = Path(os.environ.get("FEIHUALING_TEST_AUDIT", ROOT / "Data/Feihualing"))


def importer():
    spec = importlib.util.spec_from_file_location("import_feihualing", IMPORTER_PATH)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    sys.path.insert(0, str(IMPORTER_PATH.parent))
    try:
        spec.loader.exec_module(module)
    finally:
        sys.path.pop(0)
    return module


class ImportRules(unittest.TestCase):
    def setUp(self):
        self.assertTrue(IMPORTER_PATH.is_file(), "desktop importer is missing")
        self.pipeline = importer()

    def test_poem_splits_couplet_at_written_boundaries(self):
        text = "海上生明月，天涯共此时。\n情人怨遥夜，竟夕起相思。"
        self.assertEqual(
            [text[start:end] for start, end in self.pipeline.segment_spans(text, "tang")],
            ["海上生明月", "天涯共此时", "情人怨遥夜", "竟夕起相思"],
        )

    def test_lyric_retains_internal_break_and_explicit_equivalence(self):
        text = "寒蝉凄切，对长亭晚，骤雨初歇。念去去、千里烟波，暮霭沉沉楚天阔。"
        self.assertIn(
            "念去去、千里烟波",
            [text[start:end] for start, end in self.pipeline.segment_spans(text, "ci")],
        )
        self.assertEqual(
            self.pipeline.normalize_answer(" 念去去､千里烟波。 "),
            "念去去、千里烟波",
        )
        self.assertNotEqual(
            self.pipeline.normalize_answer("念去去千里烟波"),
            self.pipeline.normalize_answer("念去去、千里烟波"),
        )

    def test_rejects_uncertain_fragment_but_keeps_intact_neighbor(self):
        text = "山中相送罢，□□□□□。春草明年绿，王孙归不归。"
        self.assertEqual(
            [text[start:end] for start, end in self.pipeline.segment_spans(text, "tang")],
            ["山中相送罢", "春草明年绿", "王孙归不归"],
        )

    def test_utf16_counts_supplementary_han_before_spans(self):
        self.assertEqual(self.pipeline.utf16_boundaries("𠮷木逢春"), [0, 2, 3, 4, 5])

    def test_alias_conflicts_are_quarantined_unless_explicitly_linked(self):
        records = [("寒山石径斜", ["寒山石徑斜"]), ("远上寒山石径斜", ["寒山石径斜"])]
        groups, aliases = self.pipeline.resolve_aliases(records, [])
        self.assertEqual(groups, [0, 1])
        self.assertEqual(aliases, [["寒山石徑斜"], []])
        groups, aliases = self.pipeline.resolve_aliases(records, [("寒山石径斜", "远上寒山石径斜")])
        self.assertEqual(groups, [0, 0])
        self.assertEqual(aliases, [["寒山石徑斜"], ["寒山石径斜"]])

    def test_append_only_alias_resolution_keeps_previous_owners(self):
        previous = [[0, 0, 4, 0, 1, ["旧异文"]], [1, 0, 4, 1, 1, []]]
        rows = [("旧原文", ["旧异文"]), ("第二句", []),
                ("新异文", ["旧异文"]), ("另一句", ["旧异文"])]
        groups, aliases = self.pipeline.extend_aliases(rows, [("新异文", "旧异文")], previous)
        self.assertEqual(groups, [0, 1, 0, 2])
        self.assertEqual(aliases, [["旧异文"], [], ["旧异文"], []])
        with self.assertRaisesRegex(ValueError, "published groups"):
            self.pipeline.extend_aliases(rows, [("第二句", "旧异文")], previous)

    def test_capacity_proves_distinct_groups_over_two_cycles(self):
        self.assertIsNone(self.pipeline.position_schedule([{1, 2}, {1, 2}], 4))
        pools = [{1, 2, 3}, {1, 3, 4}]
        witness = self.pipeline.position_schedule(pools, 4)
        self.assertEqual(len(set(witness)), 4)
        self.assertTrue(all(group in pools[index % len(pools)] for index, group in enumerate(witness)))


class CatalogChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not CATALOG_PATH.is_file():
            raise AssertionError("generated Unity catalog is missing")
        cls.payload = CATALOG_PATH.read_bytes()
        cls.catalog = json.loads(gzip.decompress(cls.payload))
        cls.manifest = json.loads((AUDIT_OUTPUT / "manifest.json").read_bytes())
        cls.normalizer = importer().normalize_answer

    def test_payload_is_pinned_and_within_budget(self):
        self.assertLess(len(self.payload), 11_000_000)
        self.assertEqual(self.catalog["version"], 1)
        self.assertTrue(self.catalog["corpus"].startswith("feihualing-v2-"))
        self.assertEqual(hashlib.sha256(self.payload).hexdigest(), self.manifest["catalogSha256"])
        self.assertEqual(self.catalog["corpus"], self.manifest["corpus"])

    def test_every_display_and_accepted_answer_has_bundled_font_coverage(self):
        pipeline = importer()
        covered = pipeline.read_font_codepoints(pipeline.FONT)
        covered.update((ord("\n"), ord("\r"), ord("\t")))
        texts = [text for work in self.catalog["works"] for text in work[1:]]
        texts += [text for line in self.catalog["lines"] for text in line[5]]
        texts += [card[2] for card in self.catalog["cards"]]
        texts += [reason for card in self.catalog["cards"] for _, reason in card[7]]
        missing = {char for text in texts for char in text if ord(char) not in covered}
        self.assertEqual(missing, set())

    def test_distinct_works_and_source_coordinates(self):
        works = self.catalog["works"]
        self.assertTrue(25_000 <= len(works) <= 30_000)
        self.assertEqual(len(works), len({row[0] for row in works}))
        self.assertTrue(all(all(work) for work in works))
        sources = AUDIT_OUTPUT / "work-sources.tsv.gz"
        rows = gzip.decompress(sources.read_bytes()).decode("utf-8").splitlines()[1:]
        self.assertEqual(len(rows), len(works))
        for work, attribution in zip(works, rows):
            stable_id, kind, path, record = attribution.split("\t")
            self.assertEqual(stable_id, work[0])
            self.assertIn(kind, {"tang", "song", "ci"})
            self.assertTrue(path.endswith(".json"))
            self.assertGreaterEqual(int(record), 0)

    def test_common_song_poetry_spans_multiple_authors(self):
        known = {(author, title) for stable_id, author, title, body in self.catalog["works"]
                 if stable_id.startswith("song-")}
        self.assertTrue({
            ("苏轼", "题西林壁"), ("朱熹", "春日"), ("叶绍翁", "游园不值"),
            ("王安石", "梅花"), ("陆游", "游山西村"), ("杨万里", "小池"),
        }.issubset(known))

    def test_all_spans_and_aliases_are_unambiguous_by_group(self):
        bodies = [work[3].encode("utf-16-le") for work in self.catalog["works"]]
        seen = {}
        for work, start, size, group, common, aliases in self.catalog["lines"]:
            self.assertTrue(0 <= start < start + size <= len(bodies[work]) // 2)
            self.assertIn(common, (0, 1))
            text = bodies[work][start * 2:(start + size) * 2].decode("utf-16-le")
            for answer in [text] + aliases:
                key = type(self).normalizer(answer)
                self.assertTrue(key)
                other = seen.setdefault(key, group)
                self.assertEqual(group, other, key)

    def test_cards_cover_all_modes_with_playable_literal_candidates(self):
        pipeline = importer()
        works = self.catalog["works"]
        lines = self.catalog["lines"]
        cards = self.catalog["cards"]
        self.assertEqual(cards[0][:4], ["char-moon", 0, "月", "月"])
        self.assertEqual(len(cards), len({row[0] for row in cards}))
        for kind in range(4):
            self.assertGreaterEqual(sum(row[1] == kind for row in cards), 11)
        theme_ledger = json.loads((ROOT / "Data/Feihualing/editorial.json").read_bytes())["themes"]
        theme_ledger += json.loads((ROOT / "Data/Feihualing/editorial-themes.json").read_bytes())
        themes = {item["id"]: item for item in theme_ledger}
        audits = {card["id"]: card for card in self.manifest["cardAudit"]}
        bodies = [work[3].encode("utf-16-le") for work in works]
        for card_id, kind, title, key, second, width, sets, reasons in cards:
            with self.subTest(card=card_id):
                self.assertEqual(len(sets), width if kind == 1 else 1)
                retired = set(self.catalog["retired"])
                groups = [{lines[i][3] for i in item if i not in retired} for item in sets]
                if kind == 1:
                    witness = audits[card_id]["distinctGroupWitness"]
                    self.assertEqual(len(witness), 2 * width)
                    self.assertEqual(len(set(witness)), 2 * width)
                    self.assertTrue(all(group in groups[i % width] for i, group in enumerate(witness)))
                else:
                    self.assertGreaterEqual(len(groups[0]), 5)
                if kind == 3:
                    self.assertEqual(set(sets[0]), {row[0] for row in reasons})
                    self.assertGreaterEqual(len(groups[0]), 10)
                    self.assertGreaterEqual(len({works[lines[i][0]][1] for i in sets[0]}), 3)
                    self.assertEqual(len(themes[card_id]["answers"]), len(reasons))
                    self.assertTrue(all(len(row[1]) >= 8 for row in reasons))
                    continue
                self.assertEqual(reasons, [])
                for position, candidates in enumerate(sets):
                    self.assertEqual(len(candidates), len(set(candidates)))
                    for index in candidates:
                        work, start, size, _, _, aliases = lines[index]
                        display = bodies[work][start * 2:(start + size) * 2].decode("utf-16-le")
                        self.assertTrue(any(
                            pipeline.literal_match(value, kind, key, second, width, position)
                            for value in [display] + aliases
                        ))

    def test_every_named_classic_and_theme_reference_uses_its_cited_line(self):
        data = ROOT / "Data/Feihualing"
        classics = json.loads((data / "editorial-canon.json").read_bytes())["works"]
        classics += json.loads((data / "editorial-supplements.json").read_bytes())["works"]
        self.assertGreaterEqual(len(classics), 70)
        old_themes = json.loads((data / "editorial.json").read_bytes())["themes"]
        new_themes = json.loads((data / "editorial-themes.json").read_bytes())
        source_rows = gzip.decompress((AUDIT_OUTPUT / "work-sources.tsv.gz").read_bytes()).decode().splitlines()[1:]
        coordinates = [(path, int(row)) for _, _, path, row in [record.split("\t") for record in source_rows]]
        works = self.catalog["works"]
        bodies = [row[3].encode("utf-16-le") for row in works]
        lines = self.catalog["lines"]
        by_citation = {}
        for index, (work, start, size, _, common, _) in enumerate(lines):
            if common:
                text = bodies[work][start * 2:(start + size) * 2].decode("utf-16-le")
                by_citation.setdefault((*coordinates[work], text), index)
        for entry in classics:
            for line in entry["lines"]:
                index = by_citation[(*entry["source"], line)]
                self.assertEqual(works[lines[index][0]][1:3], [entry["author"], entry["title"]])
        cards = {card[0]: card for card in self.catalog["cards"]}
        for theme in old_themes + new_themes:
            card = cards[theme["id"]]
            accepted = {lines[index][3] for index in card[6][0]}
            cards_reasons = dict(card[7])
            self.assertTrue(theme["rejected"])
            for entry in theme["answers"]:
                index = by_citation[(*entry["source"], entry["line"])]
                self.assertEqual(cards_reasons[index], entry["reason"])
            for entry in theme["rejected"]:
                index = by_citation[(*entry["source"], entry["line"])]
                self.assertNotIn(lines[index][3], accepted)

    def test_full_source_tree_is_locked(self):
        files = {item["path"] for item in json.loads((ROOT / "Data/Feihualing/source-lock.json").read_bytes())["files"]}
        for folder, prefix, count in [
            ("全唐诗", "poet.tang", 58), ("全唐诗", "poet.song", 255), ("宋词", "ci.song", 22),
        ]:
            expected = {f"{folder}/{prefix}.{index * 1000}.json" for index in range(count)}
            self.assertTrue(expected.issubset(files), sorted(expected - files)[:5])
        self.assertEqual(len(files), 340)
        self.assertIn("水墨唐诗/shuimotangshi.json", files)
        self.assertIn("五代诗词/nantang/poetrys.json", files)

    def test_bai_juyis_dalinsi_is_playable_in_related_cards(self):
        works, lines = self.catalog["works"], self.catalog["lines"]
        work_index = next((i for i, work in enumerate(works) if work[1:3] == ["白居易", "大林寺桃花"]), None)
        self.assertIsNotNone(work_index, "白居易《大林寺桃花》应完整收录")
        body = works[work_index][3].encode("utf-16-le")
        lines_by_text = {body[start*2:(start+size)*2].decode("utf-16-le"): i
                         for i, (work, start, size, group, common, aliases) in enumerate(lines) if work == work_index}
        self.assertEqual(set(lines_by_text), {"人间四月芳菲尽", "山寺桃花始盛开", "长恨春归无觅处", "不知转入此中来"})
        cards = {card[0]: card for card in self.catalog["cards"]}
        for key in ("char-mountain", "char-flower"):
            self.assertIn(lines_by_text["山寺桃花始盛开"], cards[key][6][0])

    def test_every_scoped_curriculum_work_has_all_audited_playable_lines(self):
        curriculum = json.loads((ROOT / "Data/Feihualing/curriculum-2022.json").read_bytes())
        entries = curriculum["entries"]
        self.assertEqual(len(entries), 96)
        definitions = {("primary", n) for n in range(4, 70)} | {("middle", n) for n in range(7, 37)}
        self.assertEqual({(entry["phase"], entry["number"]) for entry in entries}, definitions)
        self.assertGreaterEqual(sum(len(entry["expected"]) for entry in entries), 570)
        indices = {row[0]: index for index, row in enumerate(self.catalog["works"])}
        indexed = {indices[entry["workId"]] for entry in entries}
        provenance = gzip.decompress((AUDIT_OUTPUT / "work-sources.tsv.gz").read_bytes()).decode().splitlines()[1:]
        works = self.catalog["works"]
        bodies = {index: works[index][3].encode("utf-16-le") for index in indexed}
        keys = {index: set() for index in indexed}
        retired = set(self.catalog["retired"])
        normalize = type(self).normalizer
        for line_id, (index, start, size, _, _, aliases) in enumerate(self.catalog["lines"]):
            if index in keys and line_id not in retired:
                display = bodies[index][start * 2:(start + size) * 2].decode("utf-16-le")
                keys[index].update(map(normalize, [display, *aliases]))
        for entry in entries:
            with self.subTest(author=entry["author"], title=entry["title"]):
                index = indices[entry["workId"]]
                self.assertEqual(works[index][1:3], [entry["author"], entry["workTitle"]])
                _, _, path, record = provenance[index].split("\t")
                self.assertEqual([path, int(record)], entry["source"])
                self.assertIn(normalize(entry["anchor"]), keys[index])
                self.assertEqual(set(map(normalize, entry["expected"])) - keys[index], set())

    def test_approved_ci_boundaries_and_retired_false_splits(self):
        works = self.catalog["works"]
        lines = self.catalog["lines"]
        retired = self.catalog["retired"]
        self.assertEqual(retired, [5537, 5538])
        bodies = [row[3].encode("utf-16-le") for row in works]
        retired_words = []
        exposed_forms = set()
        for number, (index, start, size, group, common, aliases) in enumerate(lines):
            text = bodies[index][start * 2:(start + size) * 2].decode("utf-16-le")
            if number in retired:
                self.assertEqual(works[index][1], "岳飞")
                retired_words.append(text)
                continue
            exposed_forms.update((text, *aliases))
        self.assertEqual(retired_words, ["驾长车踏破", "贺兰山缺"])
        for text in ["驾长车、踏破贺兰山缺", "驾长车踏破贺兰山缺", "莫等闲、白了少年头",
                     "浪淘尽、千古风流人物", "谈笑间、樯橹灰飞烟灭", "别是一般滋味在心头"]:
            self.assertIn(text, exposed_forms)
        for text in retired_words + ["浪淘尽", "千古风流人物", "莫等闲", "白了少年头"]:
            self.assertNotIn(text, exposed_forms)

    def test_recovered_whole_work_and_long_verse_are_playable(self):
        required = {
            "清明时节雨纷纷", "路上行人欲断魂", "借问酒家何处有", "牧童遥指杏花村",
            "弃我去者昨日之日不可留", "乱我心者今日之日多烦忧", "蜀道之难难于上青天",
        }
        works = self.catalog["works"]
        lines = self.catalog["lines"]
        keys = set()
        for work, start, size, _, _, aliases in lines:
            body = works[work][3].encode("utf-16-le")
            keys.add(body[start * 2:(start + size) * 2].decode("utf-16-le"))
            keys.update(aliases)
        self.assertEqual(required - keys, set())

    def test_supplemented_classics_accept_published_and_source_variants(self):
        works, lines = self.catalog["works"], self.catalog["lines"]
        cases = [
            ("欧阳修", "生查子·元夕", "月上柳梢头", "月到柳梢头",
             {"char-moon": 0, "char-willow": 0, "position-moon-5": 0}),
            ("李煜", "虞美人·春花秋月何时了", "春花秋月何时了", "春花秋月何時了",
             {"char-moon": 0, "char-flower": 0, "pair-flower-moon": 0, "position-moon-7": 3}),
        ]
        cards = {card[0]: card for card in self.catalog["cards"]}
        for author, title, text, variant, expected in cases:
            with self.subTest(title=title):
                work_id = next((i for i, w in enumerate(works) if w[1:3] == [author, title]), None)
                self.assertIsNotNone(work_id)
                body = works[work_id][3].encode("utf-16-le")
                by_text = {body[start * 2:(start + size) * 2].decode("utf-16-le"): number
                           for number, (work, start, size, _, _, _) in enumerate(lines) if work == work_id}
                self.assertEqual(len(by_text), 8)
                line_id = by_text[text]
                self.assertIn(variant, lines[line_id][5])
                for card_id, position in expected.items():
                    self.assertIn(line_id, cards[card_id][6][position])
                if author == "李煜":
                    self.assertIn("恰似一江春水向东流", by_text)

    def test_default_and_punctuation_examples_ship(self):
        works = self.catalog["works"]
        lines = self.catalog["lines"]
        matches = {}
        for work, start, size, group, _, aliases in lines:
            body = works[work][3].encode("utf-16-le")
            text = body[start * 2:(start + size) * 2].decode("utf-16-le")
            if text in {
                "床前明月光", "低头思故乡", "念去去、千里烟波",
                "明月松间照", "月出惊山鸟", "夜来风雨声", "谁家玉笛暗飞声",
            }:
                old = matches.get(text)
                matches[text] = (work, group, aliases + (old[2] if old else []))
        self.assertEqual(len(matches), 7)
        self.assertIn("念去去千里烟波", matches["念去去、千里烟波"][2])
        homesick = next(card for card in self.catalog["cards"] if card[0] == "topic-homesick")
        groups = {lines[index][3] for index in homesick[6][0]}
        self.assertIn(matches["低头思故乡"][1], groups)
        self.assertNotIn(matches["谁家玉笛暗飞声"][1], groups)


if __name__ == "__main__":
    unittest.main()
