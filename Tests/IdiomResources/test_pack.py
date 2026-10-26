import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('pack', ROOT / 'scripts/pack-idioms.py')
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)

class PackTests(unittest.TestCase):
    def test_gzip_is_deterministic_and_round_trips(self):
        import gzip
        value = {'word': '一心一意'}
        encoded = pack.encode(value)
        self.assertEqual(encoded, pack.encode(value))
        self.assertEqual(value, json.loads(gzip.decompress(encoded)))

    def test_projection_keeps_exact_sense_and_all_learning_fields(self):
        row = {'id': 'a', 'word': '一心一意', 'sense': {'id': 's',
            'pinyin': {'display': 'yī xīn yī yì'},
            'explanation': {'meaning': '专心。', 'stories': ['故事'],
                'origins': [{'text': '原文', 'reference': '书名'}]},
            'examples': [{'text': '例句', 'reference': ''}],
            'provenance': {'pinyin': [{'source': 'jingmo'}, {'source': 'xinhua'}]}}}
        result = pack.project_detail(row)
        self.assertEqual('s', result['SenseId'])
        self.assertEqual(['故事'], result['Stories'])
        self.assertEqual('书名', result['Origins'][0]['Reference'])
        self.assertEqual('例句', result['Examples'][0]['Text'])
        self.assertIn('静墨', result['SourceLabel'])
        self.assertIn('新华', result['SourceLabel'])
        self.assertNotIn('provenance', result)

    def test_verified_read_rejects_tampered_source(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'game-index.json'
            path.write_text('{}')
            with self.assertRaises(ValueError):
                pack.verified_read(Path(folder), {'game-index.json': '0'*64}, 'game-index.json')

    def test_index_rejects_unknown_recommended_word(self):
        with self.assertRaises(ValueError):
            pack.build_rows([], {'一心一意'})

    def test_index_projection_preserves_pronunciation_keys(self):
        row = {'id': 'a', 'word': '一心一意', 'pinyin': {'display': 'yī xīn yī yì'},
            'firstKey': 'yi', 'lastKey': 'yi', 'senseId': 's', 'detailFile': 'details/0001.json'}
        self.assertEqual([['a','一心一意','yī xīn yī yì','yi','yi','s','0001',True]],
                         pack.build_rows([row], {'一心一意'}))

if __name__ == '__main__': unittest.main()
