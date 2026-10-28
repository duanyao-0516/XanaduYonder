"""Read default Unicode coverage from standalone OpenType/TrueType SFNT files.

Only cmap formats 4 and 12 are supported. Prefer format 12, then Windows over
Unicode platform records, rather than unioning incompatible mappings. This is
a coverage reader, not an outline validator or a variation-sequence renderer.
Specification: https://learn.microsoft.com/en-us/typography/opentype/spec/cmap
"""

from pathlib import Path
import struct


def _check(condition, message):
    if not condition:
        raise ValueError(message)


def _slice(data, offset, size, label):
    _check(0 <= offset <= len(data) and 0 <= size <= len(data) - offset,
           "truncated or out-of-bounds " + label)
    return data[offset:offset + size]


def _unpack(fmt, data, offset=0):
    return struct.unpack(fmt, _slice(data, offset, struct.calcsize(fmt), "font field"))


def _format4(data):
    _, length, language, doubled = _unpack(">4H", data)
    _check(doubled > 0 and doubled % 2 == 0, "invalid format 4 segment count")
    count = doubled // 2
    glyph_start = 16 + 8 * count
    _check(length >= glyph_start and length % 2 == 0, "invalid format 4 length")
    table = _slice(data, 0, length, "format 4 subtable")
    _check(language == 0, "invalid Unicode format 4 language")
    ends = _unpack(">" + "H" * count, table, 14)
    _check(_unpack(">H", table, 14 + 2 * count)[0] == 0,
           "invalid format 4 reserved padding")
    starts = _unpack(">" + "H" * count, table, 16 + 2 * count)
    deltas = _unpack(">" + "h" * count, table, 16 + 4 * count)
    offsets = _unpack(">" + "H" * count, table, 16 + 6 * count)
    _check(starts[-1] == ends[-1] == 0xFFFF, "missing format 4 sentinel")
    result = set()
    previous_end = -1
    for i, (start, end, delta, offset) in enumerate(zip(starts, ends, deltas, offsets)):
        _check(previous_end < start <= end, "invalid or overlapping format 4 segments")
        previous_end = end
        if offset:
            first_glyph = 16 + 6 * count + 2 * i + offset
            _check(offset % 2 == 0 and first_glyph >= glyph_start,
                   "invalid format 4 glyph offset")
            _slice(table, first_glyph, 2 * (end - start + 1), "format 4 glyph array")
        for codepoint in range(start, end + 1):
            if offset:
                glyph = _unpack(">H", table, first_glyph + 2 * (codepoint - start))[0]
                glyph = (glyph + delta) & 0xFFFF if glyph else 0
            else:
                glyph = (codepoint + delta) & 0xFFFF
            if glyph:
                result.add(codepoint)
    return result


def _format12(data):
    _, reserved, length, language, count = _unpack(">HHIII", data)
    _check(reserved == 0 and language == 0, "invalid Unicode format 12 header")
    _check(length == 16 + 12 * count, "invalid format 12 group length")
    table = _slice(data, 0, length, "format 12 subtable")
    result = set()
    previous_end = -1
    for i in range(count):
        start, end, glyph = _unpack(">III", table, 16 + 12 * i)
        _check(previous_end < start <= end <= 0x10FFFF,
               "invalid or overlapping format 12 groups")
        _check(glyph + end - start <= 0xFFFF, "invalid format 12 glyph index")
        previous_end = end
        # A sequential group starting at glyph zero only lacks its first glyph.
        result.update(range(start + (glyph == 0), end + 1))
    return result


def read_font_codepoints(path) -> set[int]:
    """Return codepoints mapped to nonzero glyphs; reject malformed font data.

    ValueError reports missing/unsupported cmap, bad SFNT data or invalid
    offsets. OSError propagates filesystem failures. TTC/WOFF are unsupported.
    All supported Unicode subtables are checked before selecting coverage.
    """
    data = memoryview(Path(path).read_bytes())
    _slice(data, 0, 12, "SFNT header")
    _check(bytes(data[:4]) in (b"OTTO", b"\0\1\0\0"), "unsupported SFNT signature")
    count = _unpack(">H", data, 4)[0]
    directory_end = 12 + 16 * count
    _slice(data, 12, 16 * count, "SFNT directory")
    cmap_table = None
    tags = set()
    for i in range(count):
        tag, _, offset, length = _unpack(">4sIII", data, 12 + 16 * i)
        _check(tag not in tags, "duplicate SFNT table tag")
        tags.add(tag)
        _check(offset >= directory_end, "SFNT table offset overlaps directory bounds")
        table = _slice(data, offset, length, "SFNT table")
        if tag == b"cmap":
            cmap_table = table
    _check(cmap_table is not None, "cmap table is missing")
    version, count = _unpack(">HH", cmap_table)
    _check(version == 0, "unsupported cmap version")
    directory_end = 4 + 8 * count
    _slice(cmap_table, 4, 8 * count, "cmap directory")
    candidates = []
    parsed = {}
    for i in range(count):
        platform, encoding, offset = _unpack(">HHI", cmap_table, 4 + 8 * i)
        _check(offset >= directory_end, "cmap subtable offset overlaps directory bounds")
        fmt = _unpack(">H", cmap_table, offset)[0]
        unicode_record = platform == 0 or (platform == 3 and encoding in (1, 10))
        if not unicode_record or fmt not in (4, 12):
            continue
        if offset not in parsed:
            parser = _format12 if fmt == 12 else _format4
            parsed[offset] = parser(cmap_table[offset:])
        candidates.append(((fmt == 12, platform == 3, encoding, -i), parsed[offset]))
    _check(bool(candidates), "no supported Unicode format 4/12 cmap")
    return max(candidates, key=lambda candidate: candidate[0])[1]
