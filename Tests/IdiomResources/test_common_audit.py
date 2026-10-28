import importlib.util
import gzip
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('common_audit', ROOT / 'scripts/audit-idiom-common.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def row(word, first, last):
    return [word, word, '', first, last, '', '', False]


class CommonAuditTests(unittest.TestCase):
    def test_committed_coverage_report_matches_current_resources(self):
        catalog = json.loads(gzip.decompress((ROOT / 'Assets/Resources/Idioms/catalog.bytes').read_bytes()))
        report = json.loads((ROOT / 'Data/Idioms/common-coverage-audit.json').read_text())
        text = (ROOT / 'Data/Idioms/common-idioms.txt').read_text()
        self.assertEqual(catalog['version'], report['dictionaryVersion'])
        self.assertEqual(audit.analyze(catalog['entries'], text), report['current'])

    def test_expansion_has_requested_word_and_no_rejected_candidates(self):
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
        self.assertEqual(0, result['inputsWithQualifiedReply'])

    def test_previous_used_word_cannot_be_followup(self):
        rows = [row('甲乙丙乙', 'jia', 'yi'), row('乙丙丁甲', 'yi', 'jia')]
        result = audit.coverage(rows, {r[1] for r in rows}, 'character')
        self.assertEqual(2, result['commonWithOrdinaryFollowup'])
        self.assertEqual(2, result['inputsWithAnyCommonReply'])
        self.assertEqual(0, result['inputsWithQualifiedReply'])

    def test_character_and_sound_have_separate_adjacency(self):
        rows = [row('甲甲甲一', 'jia', 'yi'), row('乙乙乙丙', 'yi', 'bing'), row('丙丙丙丁', 'bing', 'ding')]
        common = {r[1] for r in rows}
        self.assertEqual(0, audit.coverage(rows, common, 'character')['inputsWithQualifiedReply'])
        self.assertEqual(1, audit.coverage(rows, common, 'sound')['inputsWithQualifiedReply'])

    def test_full_dictionary_move_is_not_a_recommendation(self):
        rows = [row('甲甲甲乙', 'jia', 'yi'), row('乙乙乙丙', 'yi', 'bing'), row('丙丙丙丁', 'bing', 'ding')]
        result = audit.coverage(rows, {'丙丙丙丁'}, 'character')
        self.assertEqual(2, result['inputsWithFullDictionaryReply'])
        self.assertEqual(0, result['inputsWithQualifiedReply'])
        self.assertEqual(2, result['inputsWithFullReplyButNoQualifiedReply'])


if __name__ == '__main__':
    unittest.main()
