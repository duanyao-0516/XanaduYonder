#!/usr/bin/env python3
"""Structurally remove the pinned SDK and install the locked offline host bridge.

Original gameplay functions are not recompiled: six action blocks receive
append-only bridge code; only the two pause-button actions are replaced.
"""
import hashlib
import json
from pathlib import Path
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
# These original-tag identities are pinned independently of generated output.
PATCHED = {
    8304: (12, 824, '627f9ec0e8106d0108f160685f791b4e7895e0f94c6b5aad72991d27fa31be88', 'root-init'),
    2052680: (12, 268, '119eafc3457c0b316989b5502f7101f49cc81eb74063ed0648ea594b41625168', 'menu-ready'),
    2177985: (12, 2709, '312016d5209fb3f0879f4c8d5653d158b1cae4af2ec0cd2d4f14730c63fd553a', 'restore-select'),
    2190538: (12, 721, '35d689965c1fbd6b51fbcd71299f8b9c26760d6e442e4107c719421af95d94ef', 'restore-game'),
    2238325: (39, 32339, '43a573c834012f9fdf8c24d16da161f2025eec19cb3b5993c44a871158e3435f', 'game-checkpoint'),
    2221230: (39, 840, '90b86d098a90777eb23d1e28b6c25c8f553014492a3d68cf4f86aba8eec22328', 'timer'),
    2224930: (34, 246, 'cdcdb896eb0911dc0cd29344c58ab32f6c56e2c85239b7ab050febad600dd63d', 'pause-button'),
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


def load_bridge():
    folder = Path(__file__).resolve().parent
    lock = json.loads((folder / 'bridge/lock.json').read_text())
    require(lock['schema'] == 1 and lock['compiler'] == 'JPEXS FFDec 26.4.0', 'Unreviewed bridge format/compiler')
    require(sha((folder / 'CompileBridge.java').read_bytes()) == lock['compilerSourceSha256'], 'Bridge compiler source is stale')
    result = {}
    for item in lock['files']:
        name = item['name']
        require(name in {entry[3] for entry in PATCHED.values()} and name not in result, 'Unexpected bridge action')
        source = (folder / 'bridge' / (name + '.as')).read_bytes()
        data = (folder / 'bridge' / (name + '.bin')).read_bytes()
        require(sha(source) == item['sourceSha256'], 'Bridge source changed without regeneration: ' + name)
        require(len(data) == item['bytes'] and sha(data) == item['sha256'] and data[-1:] == b'\0', 'Unreviewed compiled bridge: ' + name)
        # ExternalInterface is permitted ONLY in the hash-locked root bridge.
        # Every other prohibited network/advertising token remains forbidden.
        scanned = data.replace(b'ExternalInterface', b'') if name == 'root-init' else data
        require(FORBIDDEN.search(scanned) is None, 'Unexpected network code in bridge: ' + name)
        result[name] = data
    require(set(result) == {entry[3] for entry in PATCHED.values()}, 'Incomplete host bridge')
    return result, lock


def tag_bytes(code, payload):
    if len(payload) < 63:
        return struct.pack('<H', (code << 6) | len(payload)) + payload
    return struct.pack('<HI', (code << 6) | 63, len(payload)) + payload


def append_actions(original, extra):
    # Each original block identity is already checked. Remove only its terminal
    # ActionEnd; existing function bodies/branches/constant-pool bytes survive.
    require(original[-1:] == b'\0', 'Missing original ActionEnd')
    return original[:-1] + extra


def patch_payload(name, payload, extra):
    if name in ('root-init', 'menu-ready', 'restore-select', 'restore-game'):
        return append_actions(payload, extra)
    if name in ('timer', 'game-checkpoint'):
        output = [payload[:4]]
        patched = 0
        for offset, code, _, block, raw in tags(payload, 4):
            if code == 12 and offset == 10:
                digest = '2d064bca7a114746a7e9e72bdd76770114dc565af7723c23aa99de052b64f066' if name == 'timer' else 'd33fd976a76c1d51d7e31d2ed3c4da5012f5f87191cf605c745c362de152aa22'
                require(sha(block) == digest, 'Unexpected original sprite action')
                raw = tag_bytes(code, append_actions(block, extra))
                patched += 1
            output.append(raw)
        require(patched == 1, 'Sprite action patch count differs')
        return b''.join(output)
    require(name == 'pause-button', 'Unknown bridge patch')
    begin = 3 + struct.unpack_from('<H', payload, 3)[0]
    require(begin == 34, 'Pause button records changed')
    result = [payload[:begin]]
    count = 0
    while begin < len(payload):
        size, condition = struct.unpack_from('<HH', payload, begin)
        end = begin + size if size else len(payload)
        require(condition in (0xe008, 0xa000), 'Unexpected pause button condition')
        require(sha(payload[begin + 4:end]) == '7470dc940ae9599033de20fb2c62931d83164ad749d7282aa9ba57e928d27e85', 'Pause button action changed')
        result.append(struct.pack('<HH', len(extra) + 4 if size else 0, condition) + extra)
        begin = end
        count += 1
    require(count == 2, 'Pause button action count differs')
    return b''.join(result)


def sanitize(original):
    require(len(original) == SOURCE_BYTES and sha(original) == SOURCE_SHA256, 'Unreviewed original Bomb It SWF')
    bridge, lock = load_bridge()
    source, start = unpack(original)
    result, unbridged = [source[8:start]], [source[8:start]]
    changes, removed, patched, kept, attributes = [], set(), set(), 0, 0
    expected = []
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
        unbridged.append(raw)
        if offset in PATCHED:
            expected_code, size, digest, name = PATCHED[offset]
            require((code, len(payload), sha(payload)) == (expected_code, size, digest), 'Original bridge target changed: ' + name)
            updated = patch_payload(name, payload, bridge[name])
            raw = tag_bytes(code, updated)
            patched.add(offset)
            changes.append({'offset': offset, 'tag': code, 'operation': 'replace pause actions' if name == 'pause-button' else 'append bridge actions',
                            'bridge': name, 'originalPayloadSha256': digest, 'outputPayloadSha256': sha(updated),
                            'bridgeSha256': sha(bridge[name])})
        elif code != 69:
            kept += 1
        result.append(raw)
        expected.append((code, raw))
    require(removed == set(REMOVED) and patched == set(PATCHED) and attributes == 1, 'Incomplete reviewed patch')
    # Keep the original full denylist, including ExternalInterface, against all
    # retained source bytes. Only separately hash-reviewed bridge bytes can add
    # the six fixed callbacks and the single fixed FSCommand notification.
    require(FORBIDDEN.search(b''.join(unbridged)) is None, 'Network/advertising payload survived the structural edit')
    body = b''.join(result)
    output = b'CWS\x09' + struct.pack('<I', len(body) + 8) + zlib.compress(body, 9)
    clean, clean_start = unpack(output)
    remaining = [(code, raw) for _, code, _, _, raw in tags(clean, clean_start)]
    require(remaining == expected, 'Unexpected gameplay/resource/timeline mutation')
    return output, {'schema': 2, 'sourceSha256': SOURCE_SHA256, 'sourceBytes': SOURCE_BYTES,
                    'outputSha256': sha(output), 'outputBytes': len(output),
                    'preservedTopLevelTags': kept, 'gameplayTagsByteIdentical': False,
                    'bridgeVersion': 1, 'bridgeCompiler': lock['compiler'],
                    'bridgeActions': lock['files'], 'changes': changes}
