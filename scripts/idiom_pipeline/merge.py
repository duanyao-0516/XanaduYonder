"""Merge source rows without silently resolving disputed meanings or readings."""

from collections import Counter, defaultdict
import hashlib
import json
import unicodedata

from .normalize import clean_text, equivalence_key, is_four_han, parse_pinyin, pinyin_display


def _canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def _id(kind, value):
    return kind + "_" + hashlib.sha256(_canonical(value).encode("utf-8")).hexdigest()[:24]


def _ref(record):
    if not isinstance(record, dict):
        return {"source": None, "file": None, "row": None, "sourceId": None}
    data = record.get("data")
    source_id = data.get("id") if isinstance(data, dict) else None
    if not isinstance(source_id, (str, int)) or isinstance(source_id, bool):
        source_id = None
    return {"source": record.get("source"), "file": record.get("file"),
            "row": record.get("row"), "sourceId": source_id}


def _ref_key(ref):
    # Numeric rows sort naturally; other malformed envelope values use JSON order.
    row = ref.get("row")
    return (_canonical(ref.get("source")), _canonical(ref.get("file")),
            row if type(row) is int else -1, _canonical(ref.get("sourceId")), _canonical(ref))


def _provenance(ref, field):
    return dict(ref, field=field)


def _text(data, field, issues):
    try:
        return clean_text(data.get(field))
    except ValueError:
        issues.add("invalid_field:" + field)
        return None


def _citation(value, field, issues, structured):
    if value is None:
        return None
    if not structured:
        try:
            text = clean_text(value)
        except ValueError:
            issues.add("invalid_field:" + field)
            return None
        return {"text": text, "reference": ""} if text else None
    if not isinstance(value, dict):
        issues.add("invalid_field:" + field)
        return None
    try:
        text = clean_text(value.get("text"))
        reference = clean_text(value.get("book"))
    except ValueError:
        issues.add("invalid_field:" + field)
        return None
    if text is None:
        if reference is not None:
            issues.add("invalid_field:" + field)
        return None
    return {"text": text, "reference": reference or ""}


def _parse(record):
    ref = _ref(record)
    if (not isinstance(record, dict) or record.get("source") not in ("jingmo", "xinhua")
            or not isinstance(record.get("file"), str) or not record["file"].strip()
            or type(record.get("row")) is not int or record["row"] < 0
            or not isinstance(record.get("data"), dict)):
        return None, dict(ref, reasons=["invalid_record_schema"])
    data = record["data"]
    try:
        word = clean_text(data.get("word"))
    except ValueError:
        word = None
    if not word:
        return None, dict(ref, reasons=["missing_or_invalid_word"])
    issues = set()
    meaning = _text(data, "explanation", issues)
    if meaning and not equivalence_key(meaning):
        meaning = None
    try:
        pinyin = parse_pinyin(data.get("pinyin"))
        if not any(c in "\u0300\u0301\u0304\u030c"
                   for c in unicodedata.normalize("NFD", pinyin["display"])):
            issues.add("missing_pinyin_tones")
    except ValueError:
        issues.add("invalid_pinyin")
        try:
            display = pinyin_display(data.get("pinyin"))
        except ValueError:
            display = None
        pinyin = {"display": display, "syllables": [], "keys": []}
    parsed = {"word": word, "ref": ref, "meaning": meaning, "pinyin": pinyin,
              "issues": issues, "stories": [], "origins": [], "examples": []}
    structured = ref["source"] == "jingmo"
    for source_field, target in (("source" if structured else "derivation", "origins"),
                                 ("example", "examples")):
        citation = _citation(data.get(source_field), source_field, issues, structured)
        if citation:
            parsed[target].append((citation, _provenance(ref, source_field)))
    if structured:
        quote = _citation(data.get("quote"), "quote", issues, True)
        if quote:
            parsed["examples"].append((quote, _provenance(ref, "quote")))
        stories = data.get("story")
        if stories is not None:
            if not isinstance(stories, list):
                issues.add("invalid_field:story")
            else:
                for index, value in enumerate(stories):
                    try:
                        story = clean_text(value)
                    except ValueError:
                        issues.add("invalid_field:story")
                        continue
                    if story:
                        parsed["stories"].append((story, _provenance(ref, "story[%d]" % index)))
    return parsed, None


def _priority(parsed):
    return (0 if parsed["ref"]["source"] == "jingmo" else 1, _ref_key(parsed["ref"]))


def _signature(parsed):
    return ("invalid_pinyin" not in parsed["issues"], parsed["pinyin"]["display"],
            equivalence_key(parsed["meaning"]))


def _groups(rows):
    complete = defaultdict(list)
    incomplete = []
    for row in rows:
        if row["meaning"] is not None:
            complete[_signature(row)].append(row)
        else:
            incomplete.append(row)
    # Missing definitions may inherit only one unambiguous, valid reading's sense.
    # Decide against the complete set, never against earlier input order.
    groups = dict(complete)
    for row in incomplete:
        sig = _signature(row)
        candidates = [key for key in complete if sig[0] and key[0]
                      and key[1] == sig[1] and "invalid_field:explanation" not in row["issues"]]
        target = candidates[0] if len(candidates) == 1 else sig
        groups.setdefault(target, []).append(row)
    return groups


def _merge_items(rows, field):
    items = {}
    aliases = {}
    for row in sorted(rows, key=_priority):
        for value, provenance in row[field]:
            if isinstance(value, str):
                key = (equivalence_key(value),)
                keys = [key]
            else:
                # Match only the entire citation, with the known field's ordering.
                # Do not infer a book title from unstructured text or rewrite it.
                whole = (value["reference"] + value["text"] if field == "origins"
                         else value["text"] + value["reference"])
                key = (equivalence_key(whole),)
                keys = [key]
                if field == "examples" and value["reference"]:
                    # Xinhua uses these literal markers before an appended source.
                    # Match only the known boundary, never strip symbols in prose.
                    keys.extend((equivalence_key(value["text"] + marker + value["reference"]),)
                                for marker in ("★", "◎"))
            key = next((aliases[k] for k in keys if k in aliases), key)
            if key not in items:
                items[key] = [value, []]
            items[key][1].append(provenance)
            for alias in keys:
                aliases[alias] = key
    # Ranked source order is deterministic; retain a story's paragraph order.
    ordered = list(items.values()) if field == "stories" else [items[key] for key in sorted(items)]
    return ([item[0] for item in ordered],
            [sorted(item[1], key=_ref_key) for item in ordered])


def _sense(word, signature, rows):
    ranked = sorted(rows, key=_priority)
    meaning_rows = [r for r in ranked if r["meaning"]]
    meaning = meaning_rows[0]["meaning"] if meaning_rows else None
    stories, story_refs = _merge_items(ranked, "stories")
    origins, origin_refs = _merge_items(ranked, "origins")
    examples, example_refs = _merge_items(ranked, "examples")
    return {"id": _id("sense", [word, signature]), "pinyin": ranked[0]["pinyin"],
            "explanation": {"meaning": meaning, "stories": stories, "origins": origins},
            "examples": examples,
            "provenance": {
                "pinyin": sorted([_provenance(r["ref"], "pinyin") for r in ranked], key=_ref_key),
                "explanation.meaning": sorted([_provenance(r["ref"], "explanation")
                                               for r in meaning_rows], key=_ref_key),
                "explanation.stories": story_refs,
                "explanation.origins": origin_refs,
                "examples": example_refs}}


def merge_records(records, exclusions, font_codepoints):
    """Account for every input row and return deterministic entries and diagnostics.

    Invalid envelopes/words go to invalidRecords. A recognizable word with malformed
    content remains a source-referenced candidate and cannot enter the game index.
    """
    by_word = defaultdict(list)
    invalid = []
    input_count = 0
    for record in records:
        input_count += 1
        parsed, error = _parse(record)
        if error:
            invalid.append(error)
        else:
            by_word[parsed["word"]].append(parsed)
    entries = []
    duplicates = 0
    shared = 0
    for word in sorted(by_word):
        rows = by_word[word]
        source_counts = Counter(r["ref"]["source"] for r in rows)
        duplicates += sum(n - 1 for n in source_counts.values())
        shared += len(source_counts) > 1
        senses = sorted([_sense(word, key, group) for key, group in _groups(rows).items()],
                        key=lambda sense: sense["id"])
        reasons = set().union(*(r["issues"] for r in rows))
        if not is_four_han(word):
            reasons.add("not_four_han")
        if any(s["explanation"]["meaning"] is None for s in senses):
            reasons.add("missing_meaning")
        if len({equivalence_key(s["explanation"]["meaning"]) for s in senses
                if s["explanation"]["meaning"]}) > 1:
            reasons.add("meaning_conflict")
        if len({s["pinyin"]["display"] for s in senses}) > 1:
            reasons.add("pinyin_conflict")
        if len(senses) > 1:
            reasons.add("unresolved_senses")
        if word in exclusions:
            reasons.add("excluded:" + exclusions[word])
        if any(ord(c) not in font_codepoints for c in word):
            reasons.add("font_missing_word")
        if any(ord(c) not in font_codepoints for s in senses for c in (s["pinyin"]["display"] or "")):
            reasons.add("font_missing_pinyin")
        eligible = not reasons
        entries.append({"id": _id("idiom", word), "word": word, "senses": senses,
                        "gameSenseId": senses[0]["id"] if eligible else None,
                        "sourceRecords": sorted([r["ref"] for r in rows], key=_ref_key),
                        "quality": {"eligibleForGame": eligible, "reasons": sorted(reasons)}})
    reason_counts = Counter(reason for entry in entries for reason in entry["quality"]["reasons"])
    eligible_count = sum(e["quality"]["eligibleForGame"] for e in entries)
    stats = {"inputRecords": input_count, "sourceRecords": input_count - len(invalid),
             "invalidRecords": len(invalid), "entries": len(entries), "uniqueWords": len(entries),
             "senses": sum(len(e["senses"]) for e in entries), "eligibleForGame": eligible_count,
             "ineligibleForGame": len(entries) - eligible_count,
             "withinSourceDuplicateRecords": duplicates, "crossSourceSharedWords": shared,
             "reasonCounts": dict(sorted(reason_counts.items()))}
    return {"entries": entries, "invalidRecords": sorted(invalid, key=_ref_key), "stats": stats}
