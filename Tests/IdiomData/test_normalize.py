import importlib
import unittest


class NormalizeTests(unittest.TestCase):
    def setUp(self):
        try:
            self.n = importlib.import_module("scripts.idiom_pipeline.normalize")
        except ImportError as exc:
            self.fail("Normalization module has not been implemented: %s" % exc)

    def test_text_normalizes_nfc_and_preserves_punctuation(self):
        self.assertEqual(self.n.clean_text("  吕：lu\u0308；意义。  "), "吕：lü；意义。")
        self.assertEqual(self.n.clean_text("Ａ字"), "Ａ字")

    def test_placeholders_are_missing(self):
        for value in (None, "", "  ", "无", "暂无", "无。", "暂无。", "不详"):
            with self.subTest(value=value):
                self.assertIsNone(self.n.clean_text(value))

    def test_non_text_types_are_not_coerced(self):
        for value in (False, 10, [], {}):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    self.n.clean_text(value)

    def test_equivalence_ignores_punctuation_but_not_content_or_symbols(self):
        self.assertEqual(self.n.equivalence_key("株：露出的树根。"),
                         self.n.equivalence_key("株露出的树根"))
        self.assertNotEqual(self.n.equivalence_key("后来"), self.n.equivalence_key("◇来"))

    def test_pinyin_normalizes_spaces_and_script_g(self):
        result = self.n.parse_pinyin("  bì\u3000cōnɡ   sè mínɡ  ")
        self.assertEqual(result, {"display": "bì cōng sè míng",
                                  "syllables": ["bì", "cōng", "sè", "míng"],
                                  "keys": ["bi", "cong", "se", "ming"]})

    def test_pinyin_retains_u_umlaut_distinction(self):
        result = self.n.parse_pinyin("lǜ lín hǎo hàn")
        self.assertEqual(result["keys"], ["lv", "lin", "hao", "han"])
        self.assertNotEqual(result["keys"][0], self.n.parse_pinyin("lù bù shí yí")["keys"][0])

    def test_explicit_commas_are_syllable_separators(self):
        self.assertEqual(self.n.parse_pinyin("yī,xīn，yī,yì")["display"], "yī xīn yī yì")

    def test_tone_marks_must_be_on_standard_vowels(self):
        for value in ("ýi xīn yī yì", "b\u0301a xīn yī yì", "haǒ xīn yī yì", "rùi xīn yī yì"):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    self.n.parse_pinyin(value)
        self.assertEqual(self.n.parse_pinyin("máo liú huì lüè")["keys"], ["mao", "liu", "hui", "lve"])

    def test_invalid_pinyin_is_rejected_without_guessing(self):
        for value in (None, 1, "", "liǒo shēn tuō mìng", "xīn fán jì yyang",
                      "diu1 hún sàng dǎn", "sì qī tóu jiāo)", "diē dàng fēngliú",
                      "xī fēng yǐn lù extra", "bǐng qì níng", "nǚǚ nǚ nǚ nǚ"):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    self.n.parse_pinyin(value)

    def test_four_han_requires_exactly_four_characters(self):
        self.assertTrue(self.n.is_four_han("一心一意"))
        self.assertTrue(self.n.is_four_han("𠮷祥如意"))
        for value in ("一心一", "一心一意。", "一心A意", "一心一意多"):
            self.assertFalse(self.n.is_four_han(value))


if __name__ == "__main__":
    unittest.main()
