"""Coverage-reader contract, using tiny SFNT fixtures and the shipping font."""

import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / "scripts/idiom_pipeline/font.py"


def format4(segments, glyphs=()):
    """Segments are (start, end, delta, range_offset), including the sentinel."""
    count = len(segments)
    body = struct.pack(">7H", 4, 16 + 8 * count + 2 * len(glyphs), 0,
                       2 * count, 0, 0, 0)
    body += struct.pack(">" + "H" * count, *(s[1] for s in segments))
    body += b"\0\0"
    body += struct.pack(">" + "H" * count, *(s[0] for s in segments))
    body += struct.pack(">" + "h" * count, *(s[2] for s in segments))
    body += struct.pack(">" + "H" * count, *(s[3] for s in segments))
    return body + struct.pack(">" + "H" * len(glyphs), *glyphs)


def format12(groups):
    return struct.pack(">HHIII", 12, 0, 16 + 12 * len(groups), 0, len(groups)) + b"".join(
        struct.pack(">III", *group) for group in groups)


def cmap(records):
    offset = 4 + 8 * len(records)
    header = struct.pack(">HH", 0, len(records))
    body = b""
    for platform, encoding, subtable in records:
        header += struct.pack(">HHI", platform, encoding, offset)
        body += subtable
        offset += len(subtable)
    return header + body


def sfnt(cmap_data, signature=b"OTTO", tag=b"cmap"):
    return (signature + struct.pack(">4H", 1, 0, 0, 0)
            + struct.pack(">4sIII", tag, 0, 28, len(cmap_data)) + cmap_data)


SENTINEL = (0xFFFF, 0xFFFF, 1, 0)


class FontCoverageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.reader = None
        if MODULE.is_file():
            spec = importlib.util.spec_from_file_location("idiom_font_under_test", MODULE)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            cls.reader = staticmethod(module.read_font_codepoints)

    def setUp(self):
        self.assertIsNotNone(self.reader, "read_font_codepoints is not implemented")
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "font.otf"

    def read(self, data):
        self.path.write_bytes(data)
        return self.reader(self.path)

    def test_format4_delta_wraps_and_excludes_zero_glyph(self):
        subtable = format4([(0x40, 0x42, -0x41, 0), SENTINEL])
        self.assertEqual({0x40, 0x42}, self.read(sfnt(cmap([(3, 1, subtable)]))))

    def test_format4_range_offsets_are_relative_and_zero_stays_zero(self):
        # Offset word 0 is four bytes before glyphIdArray: [0, 1, 2].
        subtable = format4([(0x41, 0x43, -1, 4), SENTINEL], [0, 1, 2])
        self.assertEqual({0x43}, self.read(sfnt(cmap([(0, 3, subtable)]))))

    def test_format12_includes_supplementary_and_excludes_zero_glyph(self):
        subtable = format12([(0x41, 0x42, 0), (0x20000, 0x20002, 40)])
        self.assertEqual({0x42, 0x20000, 0x20001, 0x20002},
                         self.read(sfnt(cmap([(3, 10, subtable)]))))

    def test_prefers_full_unicode_subtable_instead_of_union(self):
        bmp = format4([(0x41, 0x42, 1, 0), SENTINEL])
        full = format12([(0x42, 0x42, 5), (0x20000, 0x20000, 10)])
        self.assertEqual({0x42, 0x20000}, self.read(sfnt(cmap([(3, 1, bmp), (3, 10, full)]))))

    def test_accepts_truetype_sfnt(self):
        subtable = format12([(0x4E00, 0x4E00, 1)])
        self.assertEqual({0x4E00}, self.read(sfnt(cmap([(0, 4, subtable)]), b"\0\1\0\0")))

    def test_non_unicode_mapping_does_not_claim_coverage(self):
        subtable = format4([(0x41, 0x41, 1, 0), SENTINEL])
        with self.assertRaisesRegex(ValueError, "Unicode.*cmap"):
            self.read(sfnt(cmap([(3, 0, subtable)])))

    def test_missing_cmap_has_clear_error(self):
        with self.assertRaisesRegex(ValueError, "cmap.*missing"):
            self.read(sfnt(b"anything", tag=b"name"))

    def test_unsupported_container_and_unicode_format_fail_closed(self):
        for signature in (b"ttcf", b"wOFF", b"wOF2", b"xxxx"):
            with self.subTest(signature=signature), self.assertRaisesRegex(ValueError, "SFNT"):
                self.read(sfnt(cmap([]), signature))
        with self.assertRaisesRegex(ValueError, "Unicode.*cmap"):
            self.read(sfnt(cmap([(0, 4, struct.pack(">5H", 6, 10, 0, 0, 0))])))

    def test_truncated_headers_and_directories_fail_closed(self):
        valid = sfnt(cmap([(3, 10, format12([(0x41, 0x41, 1)]))]))
        for size in (0, 4, 11, 12, 27, len(valid) - 1):
            with self.subTest(size=size), self.assertRaisesRegex(ValueError, "truncated|bounds"):
                self.read(valid[:size])

    def test_table_and_subtable_offsets_cannot_escape_bounds(self):
        valid = bytearray(sfnt(cmap([(3, 10, format12([(0x41, 0x41, 1)]))])))
        for offset, value in ((20, len(valid) + 1), (20, 12), (36, 0), (36, len(valid))):
            changed = bytearray(valid)
            struct.pack_into(">I", changed, offset, value)
            with self.subTest(offset=offset, value=value), self.assertRaises(ValueError):
                self.read(changed)

    def test_format4_invalid_range_offsets_fail_closed(self):
        for offset in (1, 2, 6, 65534):
            with self.subTest(offset=offset), self.assertRaises(ValueError):
                self.read(sfnt(cmap([(3, 1, format4([(0x41, 0x41, 0, offset), SENTINEL]))])))

    def test_format4_rejects_invalid_segments_and_lengths(self):
        for segments in ([(0x42, 0x41, 0, 0), SENTINEL],
                         [(0x41, 0x43, 0, 0), (0x43, 0x44, 0, 0), SENTINEL],
                         [(0x41, 0x43, 0, 0)]):
            with self.subTest(segments=segments), self.assertRaises(ValueError):
                self.read(sfnt(cmap([(3, 1, format4(segments))])))
        valid = bytearray(format4([SENTINEL]))
        for offset, value in ((2, 15), (2, len(valid) + 2), (6, 0), (6, 3), (16, 1)):
            changed = bytearray(valid)
            struct.pack_into(">H", changed, offset, value)
            with self.subTest(offset=offset, value=value), self.assertRaises(ValueError):
                self.read(sfnt(cmap([(3, 1, changed)])))

    def test_format12_rejects_invalid_groups_and_lengths(self):
        for groups in ([(0x42, 0x41, 1)], [(0x110000, 0x110000, 1)],
                       [(0x41, 0x43, 1), (0x43, 0x44, 5)], [(0x41, 0x42, 65535)]):
            with self.subTest(groups=groups), self.assertRaises(ValueError):
                self.read(sfnt(cmap([(3, 10, format12(groups))])))
        valid = bytearray(format12([(0x41, 0x41, 1)]))
        for offset, value in ((4, 15), (4, 100), (12, 2)):
            changed = bytearray(valid)
            struct.pack_into(">I", changed, offset, value)
            with self.subTest(offset=offset, value=value), self.assertRaises(ValueError):
                self.read(sfnt(cmap([(3, 10, changed)])))

    def test_malformed_supported_secondary_table_is_not_silently_ignored(self):
        good = format12([(0x41, 0x41, 1)])
        bad = format4([(0x41, 0x41, 0, 65534), SENTINEL])
        with self.assertRaises(ValueError):
            self.read(sfnt(cmap([(3, 10, good), (3, 1, bad)])))

    def test_repository_font_covers_chinese_and_toned_pinyin(self):
        result = self.reader(ROOT / "Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf")
        self.assertIsInstance(result, set)
        self.assertTrue(set(map(ord, "成语接龙旸一心意āáǎàüǜ")) <= result)
        self.assertGreater(len(result), 30000)
        self.assertNotIn(0x10FFFF, result)


if __name__ == "__main__":
    unittest.main()
