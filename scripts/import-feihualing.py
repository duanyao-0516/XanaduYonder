#!/usr/bin/env python3
"""Build the pinned offline Feihualing catalog. Downloads are a desktop-only opt-in."""
import argparse
from collections import Counter, defaultdict, deque
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import tempfile
import unicodedata
from urllib.parse import quote
from urllib.request import urlopen
import zlib

from idiom_pipeline.font import read_font_codepoints


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Data/Feihualing"
ASSETS = ROOT / "Assets/Resources/Feihualing"
SOURCE_LOCK = DATA / "source-lock.json"
EDITORIAL = DATA / "editorial.json"
THEMES = DATA / "editorial-themes.json"
CANON = DATA / "editorial-canon.json"
SUPPLEMENTS = DATA / "editorial-supplements.json"
CLASSICS = DATA / "editorial-classics-amendments.json"
FONT = ROOT / "Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf"
ANTHOLOGIES = {"全唐诗/唐诗三百首.json": "tang", "宋词/宋词三百首.json": "ci"}
QUOTAS = {"tang": 10_000, "song": 11_000, "ci": 6_000}
CONVERTER_VERSION = "ICU 78.3"
MIN_CARD_GROUPS = 10
MAX_CATALOG_BYTES = 11_000_000
BODY_BOUNDARY = re.compile(r"[，,。！？!?；;\n]+")
WIDTH_MARKS = str.maketrans("，。！？；：．､", ",.!?;:.、")
TERMINAL_MARKS = ",.!?;:"
UNVERIFIED_TITLES = {"句", "残句", "逸句", "断句", "佚句", "失题", "残篇"}
HAN_RANGES = (
    (0x3400, 0x4DBF), (0x4E00, 0x9FFF), (0xF900, 0xFAFF),
    (0x20000, 0x2A6DF), (0x2A700, 0x2B73F), (0x2B740, 0x2B81F),
    (0x2B820, 0x2CEAF), (0x2CEB0, 0x2EBEF), (0x2EBF0, 0x2EE5F),
    (0x2F800, 0x2FA1F), (0x30000, 0x3134F), (0x31350, 0x323AF),
)


@dataclass
class Work:
    kind: str
    path: str
    row: int
    author: str
    title: str
    body: str
    segments: list
    identity: str
    upstream_id: str
    common: bool
    pinned: bool
    priority: int

    @property
    def coordinate(self):
        return self.path, self.row


def digest(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value, pretty=False):
    options = {"indent": 2} if pretty else {"separators": (",", ":")}
    return (json.dumps(value, ensure_ascii=False, **options) + "\n").encode("utf-8")


def gzip_bytes(data):
    output = io.BytesIO()
    with gzip.GzipFile(filename="", fileobj=output, mode="wb", compresslevel=9, mtime=0) as stream:
        stream.write(data)
    return output.getvalue()


def write_atomic(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, prefix="." + path.name, delete=False) as handle:
        temporary = Path(handle.name)
        try:
            handle.write(data)
            handle.close()
            temporary.chmod(0o644)
            temporary.replace(path)
        finally:
            if temporary.exists():
                temporary.unlink()


def checked_source(spec, lock, cache, fetch):
    path = spec["path"]
    target = cache / path
    if target.is_file():
        data = target.read_bytes()
    elif fetch:
        url = f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{quote(path)}'
        with urlopen(url, timeout=40) as response:
            data = response.read()
    else:
        raise ValueError(f"missing pinned source {target}; use --fetch on the desktop")
    if len(data) != spec["bytes"] or digest(data) != spec["sha256"]:
        raise ValueError(f"source hash or size does not match lock: {target}")
    if not target.is_file():
        write_atomic(target, data)
    return path, data


def normalized_text(text):
    return unicodedata.normalize("NFC", text).strip()


def normalize_answer(text):
    key = normalized_text(text).translate(WIDTH_MARKS)
    return key[:-1].rstrip() if key and key[-1] in TERMINAL_MARKS else key


def is_han(char):
    if not char:
        return False
    cp = ord(char)
    return 0x4E00 <= cp <= 0x9FFF or any(first <= cp <= last for first, last in HAN_RANGES)


def answer_chars(value):
    return "".join(char for char in value if is_han(char))


def utf16_boundaries(text):
    offsets = [0]
    for char in text:
        offsets.append(offsets[-1] + (2 if ord(char) > 0xFFFF else 1))
    return offsets


def token_spans(text):
    start = 0
    for boundary in BODY_BOUNDARY.finditer(text):
        stop = boundary.start()
        if text[start:stop].strip():
            yield start, stop
        start = boundary.end()
    if text[start:].strip():
        yield start, len(text)


def segment_spans(text, kind, internal_breaks=False):
    minimum, maximum = (2, 16) if kind == "ci" else (3, 10)
    for start, stop in token_spans(text):
        raw = text[start:stop]
        segment = raw.strip()
        start += len(raw) - len(raw.lstrip())
        stop = start + len(segment)
        parts = segment.split("、") if kind == "ci" or internal_breaks else [segment]
        if (minimum <= sum(map(len, parts)) <= maximum
                and all(part and all(is_han(char) for char in part) for part in parts)):
            yield start, stop


def literal_match(text, kind, key, second, width, position=0):
    if kind == 0:
        return key in text
    if kind == 1:
        chars = answer_chars(text)
        return len(chars) == width and chars[position] == key
    if kind == 2:
        return key in text and second in text
    raise ValueError(f"literal matching is invalid for kind {kind}")


def resolve_aliases(records, editorial_links):
    # Exact adopted keys are a repeat identity. Other links require an editorial entry.
    parent = {normalize_answer(text): normalize_answer(text) for text, _ in records}

    def root(key):
        while parent[key] != key:
            parent[key] = parent[parent[key]]
            key = parent[key]
        return key

    for left, right in editorial_links:
        left_key = normalize_answer(left)
        right_key = normalize_answer(right)
        if left_key in parent and right_key in parent:
            parent[root(right_key)] = root(left_key)

    owners = defaultdict(set)
    for text, aliases in records:
        key = normalize_answer(text)
        owner = root(key)
        owners[key].add(owner)
        for alias in aliases:
            owners[normalize_answer(alias)].add(owner)

    group_ids = {}
    groups, accepted = [], []
    for text, aliases in records:
        key = normalize_answer(text)
        group = root(key)
        groups.append(group_ids.setdefault(group, len(group_ids)))
        used = {key}
        row = []
        for alias in aliases:
            alternate_key = normalize_answer(alias)
            if alternate_key not in used and owners[alternate_key] == {group}:
                used.add(alternate_key)
                row.append(alias)
        accepted.append(row)
    return groups, accepted


def extend_aliases(records, editorial_links, previous):
    """Append exact answer keys without reassigning an installed key or repeat group."""
    old_count = len(previous)
    known = {}
    parent = {}
    for index, line in enumerate(previous):
        group = line[3]
        parent[group] = group
        for text in [records[index][0], *line[5]]:
            known[normalize_answer(text)] = group
    keys = dict(known)
    for text, _ in records[old_count:]:
        key = normalize_answer(text)
        keys.setdefault(key, key)
        parent.setdefault(keys[key], keys[key])

    def root(item):
        while parent[item] != item:
            parent[item] = parent[parent[item]]
            item = parent[item]
        return item

    for left, right in editorial_links:
        a, b = keys.get(normalize_answer(left)), keys.get(normalize_answer(right))
        if a is None or b is None:
            continue
        first, second = root(a), root(b)
        if first == second:
            continue
        if isinstance(first, int) and isinstance(second, int):
            raise ValueError(f"editorial aliases would combine two published groups: {left} / {right}")
        if isinstance(second, int):
            first, second = second, first
        parent[second] = first

    next_group = max(parent_item for parent_item in parent if isinstance(parent_item, int)) + 1
    assigned = {}
    groups = [row[3] for row in previous]
    accepted = [row[5] for row in previous]
    owners = {key: {value} for key, value in known.items()}
    for text, aliases in records[old_count:]:
        owner = root(keys[normalize_answer(text)])
        if isinstance(owner, int):
            group = owner
        else:
            group = assigned.setdefault(owner, next_group + len(assigned))
        groups.append(group)
        for form in [text, *aliases]:
            owners.setdefault(normalize_answer(form), set()).add(group)
    for index in range(old_count, len(records)):
        text, forms = records[index]
        seen = {normalize_answer(text)}
        selected = []
        for form in forms:
            key = normalize_answer(form)
            same_owner = known[key] == groups[index] if key in known else owners[key] == {groups[index]}
            if key not in seen and same_owner:
                seen.add(key)
                selected.append(form)
        accepted.append(selected)
    return groups, accepted


def position_schedule(pools, turns):
    """Find a distinct repeat group for each planned turn by augmenting paths."""
    ordered = [sorted(pool) for pool in pools]
    assigned_turn = {}
    schedule = [None] * turns

    def assign(turn, seen):
        for group in ordered[turn % len(ordered)]:
            if group not in seen:
                seen.add(group)
                if group not in assigned_turn or assign(assigned_turn[group], seen):
                    assigned_turn[group] = turn
                    schedule[turn] = group
                    return True
        return False

    if not pools or any(not assign(turn, set()) for turn in range(turns)):
        return None
    return schedule


def source_kind(path):
    if path in ANTHOLOGIES:
        return ANTHOLOGIES[path]
    if path.startswith("全唐诗/poet.tang."):
        return "tang"
    if path.startswith("全唐诗/poet.song."):
        return "song"
    if path.startswith("宋词/ci.song."):
        return "ci"
    raise ValueError(f"unrecognized poem source: {path}")


def article_fields(row, kind):
    title = row.get("rhythmic" if kind == "ci" else "title")
    if not isinstance(title, str) or not isinstance(row.get("author"), str):
        return None
    paragraphs = row.get("paragraphs")
    if not isinstance(paragraphs, list) or not paragraphs or not all(isinstance(p, str) for p in paragraphs):
        return None
    return (
        normalized_text(row["author"]),
        normalized_text(title),
        "\n".join(normalized_text(paragraph) for paragraph in paragraphs if paragraph.strip()),
    )


def split_body(original, display, kind, resegment=False):
    original_tokens = list(token_spans(original))
    display_tokens = list(token_spans(display))
    if resegment:
        # The editorial record defines the case and registers approved answers explicitly.
        return [(start, stop, display[start:stop]) for start, stop in segment_spans(display, kind, True)]
    if len(original_tokens) != len(display_tokens):
        raise ValueError("traditional-to-simplified conversion changed text boundaries")
    originals = {}
    for (original_start, original_stop), (start, stop) in zip(original_tokens, display_tokens):
        raw = display[start:stop]
        start += len(raw) - len(raw.lstrip())
        stop = start + len(raw.strip())
        originals[start, stop] = original[original_start:original_stop].strip()
    segments = []
    for start, stop in segment_spans(display, kind):
        source = originals.get((start, stop))
        if source is None:
            raise ValueError("inconsistent token and segment boundaries")
        segments.append((start, stop, source))
    return segments


def read_works(source_files, editorial, converter, font_coverage, rejected):
    overrides = {tuple(edit["source"]): edit for edit in editorial["edits"]}
    pinned = {tuple(source) for source in editorial_citations(editorial)}
    excluded = {tuple(item["source"]) for item in editorial["exclude"]}

    for path, data in source_files:
        if path == "LICENSE":
            continue
        kind = source_kind(path)
        original_rows = json.loads(data)
        converted_rows = json.loads(subprocess.check_output(
            [converter, "-x", "Traditional-Simplified"], input=json_bytes(original_rows)
        ))
        if len(original_rows) != len(converted_rows):
            raise ValueError(f"unexpected converter row count for {path}")
        for row_index, (raw, converted) in enumerate(zip(original_rows, converted_rows)):
            coordinate = path, row_index
            if coordinate in excluded:
                rejected["editorialExclusion"] += 1
                continue
            original = article_fields(raw, kind)
            converted_fields = article_fields(converted, kind)
            if not original or not converted_fields:
                rejected["missingMetadata"] += 1
                continue
            author, title, body = converted_fields
            if not author or not title or title in UNVERIFIED_TITLES or not body:
                rejected["incompleteWork"] += 1
                continue
            edit = overrides.get(coordinate, {})
            if edit:
                if author != edit["author"] or title != edit["title"]:
                    raise ValueError(f"stale editorial source {coordinate}: {(author, title)}")
                body = edit.get("body", body)
                title = edit.get("displayTitle", title)
            if any(ord(char) not in font_coverage for value in (author, title, body) for char in value):
                rejected["missingFontGlyph"] += 1
                if coordinate in pinned:
                    raise ValueError(f"editorial work contains missing font glyphs: {coordinate}")
                continue
            segments = split_body(original[2], body, kind, edit.get("resegment", False))
            if len(segments) < 4 or len(segments) != sum(1 for _ in token_spans(body)):
                rejected["uncertainBoundaries"] += 1
                if coordinate in pinned:
                    raise ValueError(f"editorial work failed segmentation: {coordinate}")
                continue
            identity = answer_chars(body)
            # Short regular verse and structurally complete lyrics rank before longer works.
            regular = sum(stop - start in (5, 7) for start, stop, _ in segments)
            priority = 0 if (kind == "ci" or regular == len(segments)) else 1
            common = path in ANTHOLOGIES or coordinate in pinned
            yield Work(kind, path, row_index, author, title, body, segments, identity,
                       raw.get("id") or "", common, coordinate in pinned, priority)


def editorial_citations(editorial):
    yield from editorial["include"]
    for item in editorial["edits"] + editorial["aliases"]:
        yield item["source"]
    for entry in editorial["canon"]:
        yield entry["source"]
    for theme in editorial["themes"]:
        for item in theme["answers"] + theme["rejected"]:
            yield item["source"]


def select_works(eligible, editorial):
    selected = []
    bodies = set()
    upstream_ids = set()
    quota = dict(QUOTAS)

    def accept(work):
        if (quota[work.kind] <= 0 or work.identity in bodies
                or (work.upstream_id and work.upstream_id in upstream_ids)):
            return
        bodies.add(work.identity)
        if work.upstream_id:
            upstream_ids.add(work.upstream_id)
        quota[work.kind] -= 1
        selected.append(work)

    # Exact editorial citations take precedence over equivalent reprints, then anthologies.
    for work in sorted((work for work in eligible if work.common),
                       key=lambda work: (not work.pinned, work.path not in ANTHOLOGIES, work.path, work.row)):
        accept(work)
    by_author = defaultdict(list)
    for work in eligible:
        if not work.common:
            by_author[work.kind, work.author].append(work)

    # Within each kind, select in rounds over authors instead of letting a prolific author
    # or one upstream shard use the entire quota. Hash order is stable across input ordering.
    for kind in quota:
        queues = deque(
            iter(sorted(works, key=lambda work: (work.priority, digest(work.identity.encode()))))
            for (entry_kind, _), works in sorted(by_author.items()) if entry_kind == kind
        )
        while quota[kind] > 0 and queues:
            author_works = queues.popleft()
            for work in author_works:
                remaining = quota[kind]
                accept(work)
                if quota[kind] < remaining:
                    queues.append(author_works)
                    break
        if quota[kind] > 0:
            raise ValueError(f"{kind} quota is short by {quota[kind]}")
    coordinates = {work.coordinate for work in selected}
    for source in editorial_citations(editorial):
        if tuple(source) not in coordinates:
            raise ValueError(f"required editorial work was not selected: {source}")
    return selected


def build_rows(works, editorial, font_coverage, previous=None):
    explicit = {}
    for item in editorial["aliases"]:
        key = (*item["source"], item["answer"])
        if key in explicit:
            raise ValueError(f"duplicate editorial aliases: {key}")
        explicit[key] = item["accept"]
    used_explicit = set()
    adopted = []
    work_rows, line_rows, citations = [], [], []
    line_by_citation = {}
    for work_index, work in enumerate(works):
        stable_id = work_id(work)
        work_rows.append([stable_id, work.author, work.title, work.body])
        citations.append((stable_id, work.kind, work.path, str(work.row)))
        offsets = utf16_boundaries(work.body)
        for start, stop, original in work.segments:
            answer = work.body[start:stop]
            key = (*work.coordinate, answer)
            if key in explicit:
                used_explicit.add(key)
            candidates = explicit.get(key, [])
            if all(ord(char) in font_coverage for char in original):
                candidates = [original, *candidates]
            if any(ord(char) not in font_coverage for candidate in candidates for char in candidate):
                raise ValueError(f"editorial alias contains missing font glyphs: {key}")
            line_id = len(line_rows)
            line_by_citation.setdefault(key, line_id)
            adopted.append((answer, candidates))
            line_rows.append([work_index, offsets[start], offsets[stop] - offsets[start], -1,
                              int(work.common), []])
    if used_explicit != explicit.keys():
        raise ValueError(f"unresolved editorial aliases: {explicit.keys() - used_explicit}")
    reviewed_links = editorial["repeatLinks"] + [
        (citation[-1], alternate) for citation, variants in explicit.items() for alternate in variants
    ]
    groups, accepted = (resolve_aliases(adopted, reviewed_links) if previous is None
                        else extend_aliases(adopted, reviewed_links, previous))
    for index, line in enumerate(line_rows):
        line[3] = groups[index]
        line[5] = accepted[index]
    for key, aliases in explicit.items():
        line = line_rows[line_by_citation[key]]
        missing = set(map(normalize_answer, aliases)) - {
            normalize_answer(key[-1]), *map(normalize_answer, line[5])
        }
        if missing:
            raise ValueError(f"conflicting editorial aliases for {key}: {missing}")
    alias_count = sum(len(aliases) for _, aliases in adopted) - sum(len(row) for row in accepted)
    return work_rows, line_rows, [text for text, _ in adopted], line_by_citation, citations, alias_count


def build_cards(works, lines, display, by_citation, editorial, retired=()):
    cards, audit = [], []
    # Build one desktop literal lookup from every explicit realized answer form.
    by_char = defaultdict(set)
    by_position = defaultdict(set)
    by_pair = defaultdict(set)
    wanted_chars = {card["key"] for card in editorial["cards"] if card["kind"] == 0}
    wanted_positions = {(card["width"], card["key"]) for card in editorial["cards"] if card["kind"] == 1}
    wanted_pairs = tuple((card["key"], card["secondKey"]) for card in editorial["cards"] if card["kind"] == 2)
    for index, (line, text) in enumerate(zip(lines, display)):
        for accepted in {text, *line[5]}:
            chars = answer_chars(accepted)
            for char in wanted_chars.intersection(chars):
                by_char[char].add(index)
            if len(chars) in (5, 7):
                for position, char in enumerate(chars):
                    if (len(chars), char) in wanted_positions:
                        by_position[len(chars), position, char].add(index)
            for first, second in wanted_pairs:
                if first in chars and second in chars:
                    by_pair[first, second].add(index)

    for card in editorial["cards"] + [{"kind": 3, **theme} for theme in editorial["themes"]]:
        card_id, kind, title = card["id"], card["kind"], card["title"]
        key, second, width = card.get("key", ""), card.get("secondKey", ""), card.get("width", 0)
        reasons = []
        if kind == 0:
            pools = [by_char[key]]
        elif kind == 1:
            pools = [by_position[width, position, key] for position in range(width)]
        elif kind == 2:
            pools = [by_pair[key, second]]
        else:
            for entry in card["answers"]:
                path, row = entry["source"]
                answer_key = path, row, entry["line"]
                index = by_citation.get(answer_key)
                if index is None or works[lines[index][0]][1] != entry["author"]:
                    raise ValueError(f"stale theme citation: {answer_key}")
                reasons.append([index, entry["reason"]])
            pools = [{line for line, _ in reasons}]
        playable = [pool.difference(retired) for pool in pools]
        group_sets = [{lines[index][3] for index in pool} for pool in playable]
        source_works = {lines[index][0] for pool in playable for index in pool}
        common_groups = {lines[index][3] for pool in playable for index in pool if lines[index][4]}
        if not group_sets or any(len(groups) < MIN_CARD_GROUPS for groups in group_sets):
            raise ValueError(f"card {card_id} lacks distinct answer groups")
        if len(common_groups) < 5 or len({works[i][1] for i in source_works}) < 3:
            raise ValueError(f"card {card_id} lacks familiar answers or author variety")
        if kind == 3:
            for item in card["rejected"]:
                citation = (*item["source"], item["line"])
                index = by_citation.get(citation)
                if index is None or lines[index][3] in group_sets[0]:
                    raise ValueError(f"stale or admitted theme exclusion: {citation}")
        if kind == 1:
            witness = position_schedule(group_sets, 2 * width)
            if witness is None or len(set(witness)) != len(witness) or any(
                    group not in group_sets[turn % width] for turn, group in enumerate(witness)):
                raise ValueError(f"card {card_id} failed its two-cycle witness")
        else:
            witness = sorted(group_sets[0])[:MIN_CARD_GROUPS]
        candidates = [sorted(pool) for pool in pools]
        cards.append([card_id, kind, title, key, second, width, candidates, reasons])
        audit.append({"id": card_id, "candidateCounts": list(map(len, playable)),
                      "uniqueGroups": list(map(len, group_sets)), "commonGroups": len(common_groups),
                      "workCount": len(source_works), "distinctGroupWitness": witness})
    counts = Counter(card[1] for card in cards)
    if any(counts[kind] < 11 for kind in range(4)):
        raise ValueError(f"each rule requires at least eleven cards: {counts}")
    return cards, audit


def check_canon(entries, works, lines, by_citation):
    for entry in entries:
        for text in entry["lines"]:
            index = by_citation.get((*entry["source"], text))
            if index is None or works[lines[index][0]][1:3] != [entry["author"], entry["title"]]:
                raise ValueError(f"stale reference work or canonical line: {entry}")


def catalog_rows(selected, editorial, font_coverage, previous=None, retire_entries=()):
    works, lines, display, by_citation, citations, discarded_aliases = build_rows(
        selected, editorial, font_coverage, previous
    )
    check_canon(editorial["canon"], works, lines, by_citation)
    retired = retired_lines(works, lines, display, retire_entries)
    cards, card_audit = build_cards(works, lines, display, by_citation, editorial, retired)
    content = {"works": works, "lines": lines, "cards": cards}
    if retired:
        content["retired"] = sorted(retired)
    return content, citations, discarded_aliases, card_audit


def retired_lines(works, lines, display, entries):
    markers = {(item["workId"], item["line"]) for item in entries}
    rows = {i for i, (line, text) in enumerate(zip(lines, display))
            if (works[line[0]][0], text) in markers}
    if len(rows) != len(markers):
        raise ValueError("stale editorial retirement marker")
    return rows


def corpus_id(content):
    return "feihualing-v2-" + digest(json_bytes(content))[:24]


def reviewed_spans(body, expected):
    """Read explicit scoreable spans from the adopted, complete source display."""
    separators = "，,。.!！？?；;：:\n\r\t "
    start = 0
    segments = []
    for text in expected:
        while start < len(body) and body[start] in separators:
            start += 1
        end = start + len(text)
        if (not text or text[-1] in separators or not body.startswith(text, start)
                or not 2 <= len(answer_chars(text)) <= 45):
            raise ValueError(f"invalid editorial verse at offset {start}: {text}")
        segments.append((start, end, text))
        start = end
    if any(char not in separators for char in body[start:]):
        raise ValueError("editorial segmentation leaves part of the body unaccounted for")
    return segments


def work_id(work):
    return f"{work.kind}-" + digest("\x00".join(
        (work.author, work.title, work.identity)
    ).encode("utf-8"))[:24]


def read_supplements(source_files, selected, supplement, converter, font_coverage):
    source_rows = dict(source_files)
    by_id = {work_id(work): work for work in selected}
    used_citations = {work.coordinate for work in selected}
    used_bodies = {work.identity for work in selected}
    for entry in supplement["works"]:
        path, row = entry["source"]
        raw = json.loads(source_rows[path])[row]
        converted = json.loads(subprocess.check_output(
            [converter, "-x", "Traditional-Simplified"], input=json_bytes(raw)
        ))
        kind = entry["kind"]
        source_title = converted.get("title") or converted.get("rhythmic")
        original = article_fields(raw, kind)
        normalized = article_fields(converted, kind)
        if (original is None or normalized is None or source_title != entry["sourceTitle"]
                or normalized[0] != entry.get("sourceAuthor", entry["author"])):
            raise ValueError(f"stale supplemental source: {(path, row)}")
        author = entry["author"]
        title = entry["title"]
        body = entry.get("body", normalized[2])
        segments = (reviewed_spans(body, entry["lines"]) if entry.get("reviewedBounds")
                    else split_body(original[2], body, kind))
        if [body[a:b] for a, b, _ in segments] != entry["lines"]:
            raise ValueError(f"supplemental segmentation does not match approved lines: {(path, row)}")
        if any(ord(char) not in font_coverage for value in (author, title, body) for char in value):
            raise ValueError(f"supplemental work contains unsupported glyphs: {(path, row)}")
        identity = answer_chars(body)
        edition_of = entry.get("editionOf")
        if edition_of and (edition_of not in by_id or by_id[edition_of].author != author):
            raise ValueError(f"invalid prior edition for supplemental work: {(path, row)}")
        if ((path, row) in used_citations or identity in used_bodies) and not edition_of:
            raise ValueError(f"supplemental duplicate in the base catalog: {(path, row)}")
        work = Work(kind, path, row, author, title, body, segments, identity, raw.get("id") or "", True, True, 0)
        stable_id = work_id(work)
        if stable_id in by_id:
            raise ValueError(f"supplement reuses a stable edition ID: {(path, row)}")
        used_citations.add((path, row))
        used_bodies.add(identity)
        by_id[stable_id] = work
        yield work


def require_base_prefix(base, extended):
    for table in ("works", "lines"):
        if extended[table][:len(base[table])] != base[table]:
            raise ValueError(f"supplement changes existing {table} identities or rows")
    if len(base["cards"]) != len(extended["cards"]):
        raise ValueError("supplement changes the number of existing cards")
    for old, new in zip(base["cards"], extended["cards"]):
        if old[:6] != new[:6] or old[7] != new[7] or len(old[6]) != len(new[6]):
            raise ValueError(f"supplement changes existing card metadata: {old[0]}")
        if any(new_ids[:len(old_ids)] != old_ids for old_ids, new_ids in zip(old[6], new[6])):
            raise ValueError(f"supplement changes existing card candidates: {old[0]}")


def apply_amendments(source_files, selected, editorial, block, converter, font_coverage, base, preserve_aliases=False):
    if corpus_id(base) != block["baseCorpus"]:
        raise ValueError("the verified base catalog identity has changed")
    additions = list(read_supplements(source_files, selected, block, converter, font_coverage))
    selected += additions
    editorial["aliases"] += block["aliases"]
    editorial["repeatLinks"] += block.get("repeatLinks", [])
    editorial["canon"] += [entry for entry in block["works"] if not entry.get("editionOf")]
    content, citations, discarded, cards = catalog_rows(
        selected, editorial, font_coverage,
        base["lines"] if preserve_aliases else None, block.get("retire", ())
    )
    require_base_prefix(base, content)
    return content, citations, discarded, cards, len(additions)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-cache", type=Path, required=True, help="desktop cache outside Unity Assets")
    parser.add_argument("--fetch", action="store_true", help="download missing pinned source files")
    parser.add_argument("--uconv", default="uconv", help=f"path to uconv ({CONVERTER_VERSION})")
    parser.add_argument("--assets-output", type=Path, default=ASSETS)
    parser.add_argument("--audit-output", type=Path, default=DATA)
    args = parser.parse_args()
    found_version = subprocess.check_output([args.uconv, "-V"]).decode("utf-8").strip().split()[-1]
    if f"ICU {found_version}" != CONVERTER_VERSION:
        parser.error(f"expected {CONVERTER_VERSION}, found ICU {found_version}")

    lock_bytes = SOURCE_LOCK.read_bytes()
    editorial_bytes = {path.name: path.read_bytes() for path in (EDITORIAL, THEMES, CANON, SUPPLEMENTS, CLASSICS)}
    supplement = json.loads(editorial_bytes[SUPPLEMENTS.name])
    classics = json.loads(editorial_bytes[CLASSICS.name])
    lock, editorial = json.loads(lock_bytes), json.loads(editorial_bytes[EDITORIAL.name])
    editorial["themes"] += json.loads(editorial_bytes[THEMES.name])
    editorial["canon"] = json.loads(editorial_bytes[CANON.name])["works"]
    with ThreadPoolExecutor(max_workers=8) as pool:
        source_files = list(pool.map(lambda spec: checked_source(spec, lock, args.source_cache, args.fetch),
                                     lock["files"]))
    font_coverage = read_font_codepoints(FONT)
    font_coverage.update(map(ord, "\n\r\t"))
    rejected = Counter()
    supplemental_paths = {item["path"] for item in lock["files"] if item.get("supplementOnly")}
    base_sources = [(path, data) for path, data in source_files if path not in supplemental_paths]
    eligible = list(read_works(base_sources, editorial, args.uconv, font_coverage, rejected))
    selected = select_works(eligible, editorial)
    base, _, _, _ = catalog_rows(selected, editorial, font_coverage)
    previous, _, _, _, first_count = apply_amendments(
        source_files, selected, editorial, supplement, args.uconv, font_coverage, base
    )
    content, citations, redundant_aliases, card_audit, classics_count = apply_amendments(
        source_files, selected, editorial, classics, args.uconv, font_coverage, previous, preserve_aliases=True
    )
    works, lines, cards = content["works"], content["lines"], content["cards"]
    version = corpus_id(content)
    catalog = gzip_bytes(json_bytes({"version": 1, "corpus": version, **content}))
    if len(catalog) >= MAX_CATALOG_BYTES:
        raise ValueError(f"catalog exceeds reserved asset budget: {len(catalog)} bytes")
    license_data = dict(source_files)["LICENSE"]
    provenance = "stable_id\tkind\tupstream_path\tupstream_record\n" + "".join(
        "\t".join(row) + "\n" for row in citations
    )
    provenance_gzip = gzip_bytes(provenance.encode("utf-8"))
    manifest = {
        "format": 1, "corpus": version, "upstream": lock["repository"],
        "upstreamCommit": lock["commit"], "sourceLockSha256": digest(lock_bytes),
        "editorialSha256": {name: digest(data) for name, data in editorial_bytes.items()},
        "catalogSha256": digest(catalog),
        "provenanceSha256": digest(provenance_gzip), "licenseSha256": digest(license_data),
        "catalogBytes": len(catalog), "converter": CONVERTER_VERSION,
        "zlib": zlib.ZLIB_RUNTIME_VERSION, "fontSha256": digest(FONT.read_bytes()),
        "eligibleWorks": dict(Counter(work.kind for work in eligible)),
        "excludedInputRecords": dict(sorted(rejected.items())),
        "works": dict(Counter(work.kind for work in selected)), "lines": len(lines),
        "groups": len({line[3] for line in lines}), "aliasCount": sum(len(line[5]) for line in lines),
        "aliasesDiscardedAsRedundantOrAmbiguous": redundant_aliases,
        "commonWorks": dict(Counter(work.kind for work in selected if work.common)),
        "sourceFiles": len(lock["files"]), "sourceBytes": sum(spec["bytes"] for spec in lock["files"]),
        "canonWorks": len(editorial["canon"]) + sum(bool(entry.get("editionOf")) for entry in classics["works"]), "cardsByKind": dict(Counter(card[1] for card in cards)),
        "baseCorpus": supplement["baseCorpus"],
        "previousCorpus": classics["baseCorpus"],
        "retiredRows": content.get("retired", []),
        "supplementWorks": first_count + classics_count,
        "cardAudit": card_audit,
    }
    write_atomic(args.assets_output / "catalog.bytes", catalog)
    write_atomic(args.assets_output / "Licenses/Chinese-Poetry-MIT.txt", license_data)
    write_atomic(args.audit_output / "work-sources.tsv.gz", provenance_gzip)
    write_atomic(args.audit_output / "manifest.json", json_bytes(manifest, pretty=True))
    print(f"{version}: {len(works):,} works, {len(lines):,} answer lines, {len(cards)} cards, {len(catalog):,} catalog bytes")


if __name__ == "__main__":
    main()
