import importlib.util
import gzip
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('pack', ROOT / 'scripts/pack-idioms.py')
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)

class PackTests(unittest.TestCase):
    def test_common_list_rejects_duplicates_instead_of_silently_merging(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            pack.parse_common('# edited\n一心一意\n一心一意\n')

    def test_common_list_requires_exact_four_han_characters(self):
        for text in ('abc', '一心 一意', '一心一意!', '一心一意\u200b'):
            with self.subTest(text=text), self.assertRaises(ValueError):
                pack.parse_common(text)

    def test_common_list_ignores_comments_and_blank_lines(self):
        self.assertEqual({'一心一意', '一望无际'}, pack.parse_common('  # comment\n\n一心一意\n一望无际\n'))

    def test_common_resource_exactly_matches_curated_list(self):
        words = pack.parse_common((ROOT / 'Data/Idioms/common-idioms.txt').read_text())
        catalog = json.loads(gzip.decompress((ROOT / 'Assets/Resources/Idioms/catalog.bytes').read_bytes()))
        self.assertEqual(words, {e[1] for e in catalog['entries'] if e[7]})

    def test_gzip_is_deterministic_and_round_trips(self):
        value = {'word': '一心一意'}
        encoded = pack.encode(value)
        self.assertEqual(encoded, pack.encode(value))
        self.assertEqual(value, json.loads(gzip.decompress(encoded)))

    def test_projection_keeps_all_source_text_and_does_not_mix_learning_fields(self):
        row = {'id': 'a', 'word': '当仁不让', 'senseId': 'collection',
            'pinyin': {'display': ' dāng rén bù ràng  / dāng rén bù ránɡ'},
            'sourceVariants': [
                {'sourceKey': 'jingmo', 'sourceLabel': '景墨词库', 'sourceRecordId': 'jingmo:a.json:0',
                 'pinyin': ' dāng rén bù ràng ', 'meaning': ' 原指以仁为任。 ', 'stories': [' 故事甲 '],
                 'origins': [{'text': '原文甲', 'reference': '书名甲'}],
                 'examples': [{'text': '例句甲', 'reference': ''}]},
                {'sourceKey': 'xinhua', 'sourceLabel': '新华成语词库', 'sourceRecordId': 'xinhua:b.json:2',
                 'pinyin': 'dāng rén bù ránɡ', 'meaning': '遇到应该做的事就积极主动去做，不推让。',
                 'stories': [], 'origins': [{'text': '原文乙', 'reference': ''}], 'examples': []}]}
        result = pack.project_detail(row)
        self.assertEqual('collection', result['SenseId'])
        self.assertEqual('景墨词库 · 新华成语词库', result['SourceLabel'])
        self.assertEqual('【景墨词库】\n 原指以仁为任。 \n\n【新华成语词库】\n遇到应该做的事就积极主动去做，不推让。', result['Meaning'])
        self.assertEqual([], result['Stories'])
        self.assertEqual([], result['Origins'])
        self.assertEqual([], result['Examples'])
        self.assertEqual(2, len(result['Sources']))
        for actual, source in zip(result['Sources'], row['sourceVariants']):
            for key in ('sourceKey', 'sourceLabel', 'sourceRecordId', 'pinyin', 'meaning', 'stories'):
                self.assertEqual(source[key], actual[key[0].upper()+key[1:]])
        self.assertEqual({'Text': '原文甲', 'Reference': '书名甲'}, result['Sources'][0]['Origins'][0])
        self.assertEqual('例句甲', result['Sources'][0]['Examples'][0]['Text'])

    def test_projection_preserves_repeated_source_labels_as_separate_records(self):
        variants = [{'sourceKey': 'jingmo', 'sourceLabel': '景墨词库',
                     'sourceRecordId': 'jingmo:a.json:' + str(i), 'pinyin': '', 'meaning': meaning,
                     'stories': [], 'origins': [], 'examples': []} for i, meaning in enumerate(['甲义', '乙义'])]
        result = pack.project_detail({'id': 'a', 'word': '当仁不让', 'senseId': 'collection',
                                      'pinyin': {'display': ''}, 'sourceVariants': variants})
        self.assertEqual(2, len(result.get('Sources', [])))
        self.assertEqual(['甲义', '乙义'], [v['Meaning'] for v in result['Sources']])
        self.assertEqual('【景墨词库】\n甲义\n\n【景墨词库】\n乙义', result['Meaning'])
        self.assertEqual('景墨词库', result['SourceLabel'])

    def test_packed_details_use_correct_dictionary_display_names(self):
        resources = ROOT / 'Assets/Resources/Idioms'
        manifest = json.loads((resources / 'manifest.json').read_text())
        expected_labels = {'景墨词库', '新华成语词库', '景墨词库 · 新华成语词库'}
        observed_labels = set()
        for relative in manifest['resources']:
            if not relative.startswith('details/'):
                continue
            rows = json.loads(gzip.decompress((resources / relative).read_bytes()))
            for row in rows:
                self.assertIn(row['SourceLabel'], expected_labels, (relative, row['Word']))
                observed_labels.add(row['SourceLabel'])
        self.assertTrue(any('景墨词库' in label for label in observed_labels))

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
            'firstKey': 'yi', 'lastKey': 'yi', 'senseId': 's', 'detailFile': 'details/0001.json',
            'readings': [{'firstKey': 'yi', 'lastKey': 'yi'}, {'firstKey': 'yi', 'lastKey': 'yue'}]}
        self.assertEqual([['a','一心一意','yī xīn yī yì','yi','yi','s','0001',True,[['yi','yi'],['yi','yue']]]],
                         pack.build_rows([row], {'一心一意'}))

if __name__ == '__main__': unittest.main()
