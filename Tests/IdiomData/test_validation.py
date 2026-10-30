import copy
import importlib.util
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
spec = importlib.util.spec_from_file_location('import_idioms', ROOT / 'scripts/import-idioms.py')
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)


class SourceValidationTests(unittest.TestCase):
    def setUp(self):
        self.records = [{'source': 'jingmo', 'file': 'j.json', 'row': 0, 'data': {
            'word': '当仁不让', 'pinyin': ' Dāng rén bù ràng ', 'explanation': ' 原义。 ',
            'story': [' 原故事。 '], 'source': {'text': ' 原出处 ', 'book': ' 书名 '}}}]
        self.result = importer.merge_records(self.records, {'当仁不让': 'audit only'}, set())

    def test_diagnostics_do_not_invalidate_playable_entries(self):
        importer.validate_result(self.records, self.result)

    def test_validation_rejects_any_rewritten_source_text(self):
        for field in ('pinyin', 'meaning', 'stories', 'origins'):
            result = copy.deepcopy(self.result)
            source = result['entries'][0]['sourceVariants'][0]
            source[field] = source[field].strip() if isinstance(source[field], str) else []
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'source variant content'):
                importer.validate_result(self.records, result)

    def test_validation_rejects_lost_source_variant(self):
        self.result['entries'][0]['sourceVariants'] = []
        with self.assertRaisesRegex(ValueError, 'lineage'):
            importer.validate_result(self.records, self.result)


if __name__ == '__main__':
    unittest.main()
