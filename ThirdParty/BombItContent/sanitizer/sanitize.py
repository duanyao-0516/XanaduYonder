#!/usr/bin/env python3
"""Remove the pinned game's unused site SDK without recompiling gameplay actions.

This is a structural SWF edit, not a runtime network blocker. Original shape,
sound, timeline and gameplay-action tag bytes are copied exactly. Only the six
reviewed SDK tags and FileAttributes.UseNetwork are changed.
"""
import hashlib
import re
import struct
import zlib

SOURCE_SHA256 = 'a2b4cc65115e4b6eb813962d80e91f1e65746a91f27e91d5e86fa1963ed5b54d'
SOURCE_BYTES = 2167060
REMOVED = {
    9134: (39, 14, '054ce68ffe119413238183010135f0a0994c1842d809131f6d32ddf5409ce331', 'empty SDK display symbol 1'),
    9154: (56, 13, 'a591cde5695a7b7a658c4e0748cc82a4023ab618de2dcb7e004ab153ce0b6253', 'FWAd_AS2 export'),
    215065: (39, 6, '236d039e3419fea0c30a2eb0b0b3cd2bd8863638e64bb65e837eb8dbc3a9712e', 'empty SDK class symbol 603'),
    215077: (56, 24, '2e190bb145df9982db74c62be16d83867ba029a1adad8daaea41082089924a68', '__Packages.FWAd_AS2 export'),
    215107: (59, 8630, '5549e2f6c38eef2ac46a43ee2db1dd8550a9507ce06d5ddb79105dad2354c3db', 'complete advertising/news/framework class'),
    223743: (59, 64, 'b797177a5f8e3a4250d2b74dfe4153ebe7675d37c3742ae61eb46c652ebabd5e', 'SDK Object.registerClass action'),
}
FORBIDDEN = re.compile(rb'FWAd|News\.swf|https?://|loadMovie|loadVariables|loadClip|'
                       rb'LocalConnection|XMLSocket|ExternalInterface|4399|as3\.swf', re.I)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unpack(data):
    require(data[:4] in (b'CWS\x09', b'FWS\x09'), 'Unexpected SWF signature/version')
    body = zlib.decompress(data[8:]) if data[:3] == b'CWS' else data[8:]
    require(len(body) + 8 == struct.unpack_from('<I', data, 4)[0], 'Invalid declared SWF length')
    rect_bytes = (5 + 4 * (body[0] >> 3) + 7) // 8
    require(body[:rect_bytes].hex() == '7800055f00000fa000', 'Unexpected stage geometry')
    require(struct.unpack_from('<HH', body, rect_bytes) == (35 * 256, 12), 'Unexpected frame rate/count')
    return data[:8] + body, 8 + rect_bytes + 4


def tags(data, begin, end=None):
    end = len(data) if end is None else end
    pos = begin
    while pos < end:
        start = pos
        require(pos + 2 <= end, 'Truncated tag header')
        header = struct.unpack_from('<H', data, pos)[0]
        pos += 2
        code, size = header >> 6, header & 63
        if size == 63:
            require(pos + 4 <= end, 'Truncated long tag header')
            size = struct.unpack_from('<I', data, pos)[0]
            pos += 4
        require(pos + size <= end, 'Truncated tag payload')
        yield start, code, pos, data[pos:pos + size], data[start:pos + size]
        pos += size
        if code == 0:
            require(pos == end, 'Trailing bytes after SWF End tag')
            return
    # The pinned root timeline has no physical End tag; its declared length is
    # authoritative. Nested sprites still retain their original End tags.
    require(pos == end, 'Tag stream did not end at its declared boundary')


def sanitize(original):
    require(len(original) == SOURCE_BYTES and sha(original) == SOURCE_SHA256, 'Unreviewed original Bomb It SWF')
    source, start = unpack(original)
    result = [source[8:start]]
    changes, removed, kept, attributes = [], set(), 0, 0
    for offset, code, payload_at, payload, raw in tags(source, start):
        if offset in REMOVED:
            expected_code, size, digest, reason = REMOVED[offset]
            require((code, len(payload), sha(payload)) == (expected_code, size, digest), 'SDK tag identity changed')
            removed.add(offset)
            changes.append({'offset': offset, 'tag': code, 'bytes': len(raw), 'sha256': sha(raw), 'operation': 'remove', 'reason': reason})
            continue
        if code == 69:
            require(payload == b'\x01\x00\x00\x00', 'Unexpected FileAttributes')
            raw = raw[:payload_at - offset] + b'\x00\x00\x00\x00'
            attributes += 1
            changes.append({'offset': offset, 'tag': code, 'operation': 'clear UseNetwork'})
        else:
            kept += 1
        result.append(raw)
    require(removed == set(REMOVED) and attributes == 1, 'Sanitizer did not apply the complete reviewed patch')
    body = b''.join(result)
    require(FORBIDDEN.search(body) is None, 'Network/advertising payload survived the structural edit')
    output = b'CWS\x09' + struct.pack('<I', len(body) + 8) + zlib.compress(body, 9)
    # All retained top-level tags, including their nested actions, are preserved
    # byte for byte except the explicitly documented FileAttributes flag.
    clean, clean_start = unpack(output)
    remaining = [(code, raw) for _, code, _, _, raw in tags(clean, clean_start)]
    expected = [(code, raw if code != 69 else raw[:-4] + b'\x00' * 4)
                for off, code, _, _, raw in tags(source, start) if off not in REMOVED]
    require(remaining == expected, 'Unexpected gameplay/resource/timeline mutation')
    return output, {'schema': 1, 'sourceSha256': SOURCE_SHA256, 'sourceBytes': SOURCE_BYTES,
                    'outputSha256': sha(output), 'outputBytes': len(output),
                    'preservedTopLevelTags': kept, 'gameplayTagsByteIdentical': True,
                    'changes': changes}
