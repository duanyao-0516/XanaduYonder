import importlib.util
import gzip
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('common_audit', ROOT / 'scripts/audit-idiom-common.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def row(word, first, last, readings=None):
    pairs = [[first, last]] if readings is None else readings
    return [word, word, '', first, last, '', '', False, pairs]


class CommonAuditTests(unittest.TestCase):
    def test_committed_coverage_report_matches_current_resources(self):
        catalog = json.loads(gzip.decompress((ROOT / 'Assets/Resources/Idioms/catalog.bytes').read_bytes()))
        report = json.loads((ROOT / 'Data/Idioms/common-coverage-audit.json').read_text())
        text = (ROOT / 'Data/Idioms/common-idioms.txt').read_text()
        self.assertEqual(2, report['schemaVersion'])
        self.assertEqual(len(catalog['entries']), report['entryCount'])
        self.assertEqual(catalog['version'], report['dictionaryVersion'])
        self.assertEqual(audit.analyze(catalog['entries'], text), report['current'])

    def test_unchanged_common_list_matches_historical_editorial_audit(self):
        words = audit.pack.parse_common((ROOT / 'Data/Idioms/common-idioms.txt').read_text())
        report = json.loads((ROOT / 'Data/Idioms/common-expansion-audit.json').read_text())
        self.assertIn('一望无际', words)
        self.assertEqual(report['finalCount'], len(words))
        self.assertEqual(report['finalCount'], report['baselineCount'] + report['newlyAcceptedCount'])
        self.assertFalse(words & set(report['editorialExclusions']))
        self.assertFalse(words & {row['word'] for row in report['excludedByPlayableIndex']})

    def test_self_loop_does_not_count_as_its_own_followup(self):
        rows = [row('甲乙丙甲', 'jia', 'jia')]
        result = audit.coverage(rows, {'甲乙丙甲'}, 'character')
        self.assertEqual(0, result['commonWithOrdinaryFollowup'])
        self.assertEqual(0, result['inputsWithCommonReplyAndOrdinaryFollowup'])

    def test_previous_used_word_cannot_be_followup(self):
        rows = [row('甲乙丙乙', 'jia', 'yi'), row('乙丙丁甲', 'yi', 'jia')]
        result = audit.coverage(rows, {r[1] for r in rows}, 'character')
        self.assertEqual(2, result['commonWithOrdinaryFollowup'])
        self.assertEqual(2, result['inputsWithAnyCommonReply'])
        self.assertEqual(0, result['inputsWithCommonReplyAndOrdinaryFollowup'])
        self.assertEqual(2, result['inputsWithSelectableReply'])
        self.assertEqual(0, result['inputsRequiringFullDictionaryFallback'])

    def test_character_and_sound_have_separate_adjacency(self):
        rows = [row('甲甲甲一', 'jia', 'yi'), row('乙乙乙丙', 'yi', 'bing'), row('丙丙丙丁', 'bing', 'ding')]
        common = {r[1] for r in rows}
        self.assertEqual(0, audit.coverage(rows, common, 'character')['inputsWithCommonReplyAndOrdinaryFollowup'])
        self.assertEqual(1, audit.coverage(rows, common, 'sound')['inputsWithCommonReplyAndOrdinaryFollowup'])

    def test_full_dictionary_move_is_selected_when_common_pool_is_empty(self):
        rows = [row('甲甲甲乙', 'jia', 'yi'), row('乙乙乙丙', 'yi', 'bing'), row('丙丙丙丁', 'bing', 'ding')]
        result = audit.coverage(rows, {'丙丙丙丁'}, 'character')
        self.assertEqual(2, result['inputsWithFullDictionaryReply'])
        self.assertEqual(0, result['inputsWithCommonReplyAndOrdinaryFollowup'])
        self.assertEqual(1, result['inputsWithAnyCommonReply'])
        self.assertEqual(1, result['inputsRequiringFullDictionaryFallback'])
        self.assertEqual(2, result['inputsWithSelectableReply'])

    def test_sound_coverage_uses_each_reading_and_deduplicates_words(self):
        rows = [row('甲甲甲乙', 'jia', 'yi'),
                row('乙乙乙丙', 'bing', 'ding', [['bing', 'ding'], ['yi', 'wu'], ['yi', 'ding']]),
                row('戊戊戊己', 'wu', 'ji')]
        result = audit.coverage(rows, {r[1] for r in rows}, 'sound')
        self.assertEqual(2, result['inputsWithFullDictionaryReply'])
        self.assertEqual(2, result['inputsWithAnyCommonReply'])
        self.assertEqual(4, result['commonStartKeys'])

    def test_other_legal_replies_remain_selectable_when_common_reply_exists(self):
        rows = [row('甲甲甲乙', 'jia', 'yi'), row('乙乙乙丙', 'yi', 'bing'),
                row('乙乙乙丁', 'yi', 'ding')]
        result = audit.coverage(rows, {'乙乙乙丙'}, 'character')
        self.assertEqual(1, result['inputsWithAnyCommonReply'])
        self.assertEqual(1, result['inputsWithOtherLegalReply'])
        self.assertEqual(0, result['inputsRequiringFullDictionaryFallback'])
        self.assertEqual(1, result['inputsWithSelectableReply'])

    def test_unparseable_readings_have_no_sound_edges_but_keep_character_edges(self):
        rows = [row('甲甲甲乙', '', '', []), row('乙乙乙丙', '', '', [])]
        common = {r[1] for r in rows}
        self.assertEqual(0, audit.coverage(rows, common, 'sound')['inputsWithFullDictionaryReply'])
        self.assertEqual(1, audit.coverage(rows, common, 'character')['inputsWithFullDictionaryReply'])

    def test_sound_followup_cannot_mix_different_reading_pairs(self):
        rows = [row('甲甲甲乙', 'jia', 'yi'),
                row('乙乙乙丙', 'yi', 'bing', [['yi', 'bing'], ['ding', 'wu']]),
                row('戊戊戊己', 'wu', 'ji')]
        result = audit.coverage(rows, {r[1] for r in rows}, 'sound')
        self.assertEqual(2, result['inputsWithFullDictionaryReply'])
        self.assertEqual(0, result['inputsWithCommonReplyAndOrdinaryFollowup'])


if __name__ == '__main__':
    unittest.main()
