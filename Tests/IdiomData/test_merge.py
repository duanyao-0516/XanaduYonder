import copy
import importlib
import random
import unittest


def record(dataset="jingmo", row=0, **fields):
    data = {"word": "一心一意", "pinyin": "yī xīn yī yì", "explanation": "心意专一。"}
    data.update(fields)
    return {"source": dataset, "file": dataset + ".json", "row": row, "data": data}


FONT = set(range(0x110000))


class MergeTests(unittest.TestCase):
    def setUp(self):
        try:
            self.merge = importlib.import_module("scripts.idiom_pipeline.merge").merge_records
        except (ImportError, AttributeError) as exc:
            self.fail("Merge implementation is missing: %s" % exc)

    def run_merge(self, rows, exclusions=None, font=None):
        return self.merge(rows, exclusions or {}, FONT if font is None else font)

    def test_equivalent_sources_merge_with_jingmo_punctuation_and_field_provenance(self):
        result = self.run_merge([
            record("xinhua", explanation="心意专一", derivation="古书出处。", example="例句～。"),
            record(id=42, explanation="心意：专一。", story=["一段故事。"]),
        ])
        entry = result["entries"][0]
        self.assertEqual(len(entry["senses"]), 1)
        sense = entry["senses"][0]
        self.assertEqual(sense["explanation"]["meaning"], "心意：专一。")
        self.assertEqual(sense["explanation"]["stories"], ["一段故事。"])
        self.assertEqual(sense["explanation"]["origins"], [{"text": "古书出处。", "reference": ""}])
        self.assertEqual(sense["examples"], [{"text": "例句～。", "reference": ""}])
        self.assertEqual(len(sense["provenance"]["explanation.meaning"]), 2)
        self.assertEqual(sense["provenance"]["explanation.origins"][0][0]["source"], "xinhua")
        self.assertTrue(entry["quality"]["eligibleForGame"])
        self.assertEqual(entry["gameSenseId"], sense["id"])
        self.assertEqual(len(entry["sourceRecords"]), 2)
        self.assertNotIn("data", entry["sourceRecords"][0])
        self.assertEqual(result["stats"]["crossSourceSharedWords"], 1)

    def test_same_source_duplicates_are_preserved_and_merged(self):
        result = self.run_merge([record(row=10), record(row=30)])
        entry = result["entries"][0]
        self.assertEqual(len(entry["senses"]), 1)
        self.assertEqual([r["row"] for r in entry["sourceRecords"]], [10, 30])
        self.assertEqual(result["stats"]["withinSourceDuplicateRecords"], 1)
        self.assertEqual(result["stats"]["uniqueWords"], 1)

    def test_story_paragraphs_retain_original_narrative_order(self):
        entry = self.run_merge([record(story=["汉朝发生开端。", "后来迎来结局。"])])["entries"][0]
        self.assertEqual(entry["senses"][0]["explanation"]["stories"], ["汉朝发生开端。", "后来迎来结局。"])

    def test_structured_and_unstructured_citations_merge_only_when_whole_text_matches(self):
        entry = self.run_merge([
            record(source={"book": "《古书》", "text": "正文。"},
                   example={"book": "《作品》", "text": "例句。"}),
            record("xinhua", derivation="《古书》：正文。", example="例句。（《作品》）")])["entries"][0]
        sense = entry["senses"][0]
        self.assertEqual(sense["explanation"]["origins"], [{"text": "正文。", "reference": "《古书》"}])
        self.assertEqual(sense["examples"], [{"text": "例句。", "reference": "《作品》"}])
        self.assertEqual(len(sense["provenance"]["examples"][0]), 2)
        self.assertEqual(len(sense["provenance"]["explanation.origins"][0]), 2)

    def test_example_book_separator_is_compared_only_at_the_known_boundary(self):
        entry = self.run_merge([
            record(example={"book": "《作品》", "text": "例句。"}),
            record("xinhua", example="例句。★《作品》"),
            record("xinhua", row=1, example="例★句。《作品》")])["entries"][0]
        sense = entry["senses"][0]
        self.assertEqual(len(sense["examples"]), 2)
        canonical = next(i for i, item in enumerate(sense["examples"]) if item["reference"] == "《作品》")
        self.assertEqual(len(sense["provenance"]["examples"][canonical]), 2)

    def test_conflicting_meanings_do_not_mix_stories(self):
        result = self.run_merge([record(story=["故事甲"]),
                                 record("xinhua", explanation="另一个意思。", derivation="出处乙")])
        entry = result["entries"][0]
        self.assertEqual(len(entry["senses"]), 2)
        self.assertIsNone(entry["gameSenseId"])
        self.assertIn("meaning_conflict", entry["quality"]["reasons"])
        second = next(s for s in entry["senses"] if s["explanation"]["meaning"] == "另一个意思。")
        self.assertEqual(second["explanation"]["stories"], [])

    def test_tone_only_difference_is_still_a_pronunciation_conflict(self):
        entry = self.run_merge([record(), record("xinhua", pinyin="yì xīn yī yì")])["entries"][0]
        self.assertEqual(len(entry["senses"]), 2)
        self.assertIn("pinyin_conflict", entry["quality"]["reasons"])
        self.assertFalse(entry["quality"]["eligibleForGame"])

    def test_entirely_untoned_pinyin_is_retained_but_not_game_eligible(self):
        entry = self.run_merge([record(word="四时充美", pinyin="si shi chong mei")])["entries"][0]
        self.assertIn("missing_pinyin_tones", entry["quality"]["reasons"])
        self.assertFalse(entry["quality"]["eligibleForGame"])
        self.assertIsNone(entry["gameSenseId"])
        self.assertEqual(entry["senses"][0]["pinyin"]["display"], "si shi chong mei")
        self.assertEqual(entry["senses"][0]["pinyin"]["keys"], ["si", "shi", "chong", "mei"])

    def test_neutral_tone_syllable_does_not_block_otherwise_toned_pinyin(self):
        entry = self.run_merge([record(word="老实巴交", pinyin="lǎo shi bā jiāo")])["entries"][0]
        self.assertNotIn("missing_pinyin_tones", entry["quality"]["reasons"])
        self.assertTrue(entry["quality"]["eligibleForGame"])
        self.assertIsNotNone(entry["gameSenseId"])

    def test_same_source_conflicts_are_not_overwritten(self):
        entry = self.run_merge([record(row=1), record(row=2, explanation="另义")])["entries"][0]
        self.assertEqual(len(entry["sourceRecords"]), 2)
        self.assertEqual(len(entry["senses"]), 2)
        self.assertIsNone(entry["gameSenseId"])

    def test_missing_meaning_can_be_filled_from_unique_matching_reading(self):
        entry = self.run_merge([record(explanation=None, story=["保留故事"]), record("xinhua")])["entries"][0]
        self.assertEqual(len(entry["senses"]), 1)
        sense = entry["senses"][0]
        self.assertEqual(sense["explanation"]["meaning"], "心意专一。")
        self.assertEqual(sense["explanation"]["stories"], ["保留故事"])
        self.assertEqual([r["source"] for r in sense["provenance"]["explanation.meaning"]], ["xinhua"])
        self.assertTrue(entry["quality"]["eligibleForGame"])

    def test_missing_meaning_does_not_choose_between_conflicting_senses(self):
        entry = self.run_merge([record(), record(row=1, explanation="另一义"),
                               record("xinhua", explanation=None, derivation="未归属出处")])["entries"][0]
        self.assertEqual(len(entry["senses"]), 3)
        incomplete = next(s for s in entry["senses"] if s["explanation"]["meaning"] is None)
        self.assertEqual(incomplete["explanation"]["origins"][0]["text"], "未归属出处")

    def test_invalid_pinyin_remains_separate_from_good_source(self):
        entry = self.run_merge([record(), record("xinhua", pinyin="yī xīn yī yì)")])["entries"][0]
        self.assertEqual(len(entry["senses"]), 2)
        self.assertEqual(len(entry["sourceRecords"]), 2)
        self.assertIn("invalid_pinyin", entry["quality"]["reasons"])
        self.assertIsNone(entry["gameSenseId"])

    def test_non_four_character_words_are_retained_without_game_eligibility(self):
        entry = self.run_merge([record(word="九回肠", pinyin="jiǔ huí cháng")])["entries"][0]
        self.assertEqual(entry["word"], "九回肠")
        self.assertIn("not_four_han", entry["quality"]["reasons"])
        self.assertFalse(entry["quality"]["eligibleForGame"])

    def test_placeholders_are_not_definitions_origins_or_examples(self):
        entry = self.run_merge([record("xinhua", explanation="无", derivation="无", example="暂无")])["entries"][0]
        sense = entry["senses"][0]
        self.assertIsNone(sense["explanation"]["meaning"])
        self.assertEqual(sense["explanation"]["origins"], [])
        self.assertEqual(sense["examples"], [])
        self.assertIn("missing_meaning", entry["quality"]["reasons"])

    def test_quote_is_example_and_text_reference_are_retained(self):
        entry = self.run_merge([record(source={"text": "出处", "book": "古书"},
                                      quote={"text": "例句～", "book": "作品"}, story=["故事"] )])["entries"][0]
        sense = entry["senses"][0]
        self.assertEqual(sense["examples"], [{"text": "例句～", "reference": "作品"}])
        self.assertEqual(sense["explanation"]["origins"], [{"text": "出处", "reference": "古书"}])
        self.assertEqual(sense["explanation"]["stories"], ["故事"])

    def test_optional_field_wrong_types_are_reported_not_stringified(self):
        entry = self.run_merge([record(story="wrong", source=[], example={"text": 3})])["entries"][0]
        self.assertFalse(entry["quality"]["eligibleForGame"])
        self.assertIn("invalid_field:story", entry["quality"]["reasons"])
        self.assertEqual(entry["senses"][0]["explanation"]["stories"], [])

    def test_malformed_records_are_accounted_for_without_throwing(self):
        rows = [record(), record(row=2, word=None), {"source": "xinhua", "file": "x.json", "row": 4, "data": []}]
        result = self.run_merge(rows)
        self.assertEqual(len(result["invalidRecords"]), 2)
        self.assertEqual(result["stats"]["inputRecords"], 3)
        self.assertEqual(result["stats"]["sourceRecords"] + result["stats"]["invalidRecords"], 3)

    def test_core_wrong_types_are_preserved_as_ineligible_candidates(self):
        entry = self.run_merge([record(explanation=[], pinyin=5)])["entries"][0]
        self.assertFalse(entry["quality"]["eligibleForGame"])
        self.assertIn("invalid_field:explanation", entry["quality"]["reasons"])
        self.assertIn("invalid_pinyin", entry["quality"]["reasons"])

    def test_exclusions_and_font_coverage_both_block_eligibility(self):
        entry = self.run_merge([record()], {"一心一意": "requires source review"}, {ord(c) for c in "一心一意y x "})["entries"][0]
        self.assertIn("excluded:requires source review", entry["quality"]["reasons"])
        self.assertIn("font_missing_pinyin", entry["quality"]["reasons"])
        other = self.run_merge([record()], font=set())["entries"][0]
        self.assertIn("font_missing_word", other["quality"]["reasons"])

    def test_entire_result_is_order_independent_and_input_is_unchanged(self):
        rows = [record(row=0, story=["乙", "甲"], source={"text": "古文", "book": "书"}),
                record("xinhua", explanation="心意专一", derivation="古文"),
                record(row=2, word="九回肠", pinyin="jiǔ huí cháng"), record(row=4, word=None)]
        original = copy.deepcopy(rows)
        expected = self.run_merge(rows)
        for seed in range(8):
            random.Random(seed).shuffle(rows)
            self.assertEqual(self.run_merge(rows), expected)
        self.assertEqual(sorted(rows, key=lambda r: (r["source"], r["row"])),
                         sorted(original, key=lambda r: (r["source"], r["row"])))


if __name__ == "__main__":
    unittest.main()
