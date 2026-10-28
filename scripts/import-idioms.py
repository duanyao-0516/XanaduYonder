#!/usr/bin/env python3
"""Reproducible, offline-first idiom import. Never rewrites dictionary prose."""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
from urllib.request import urlopen

from idiom_pipeline.font import read_font_codepoints
from idiom_pipeline.merge import merge_records


ROOT = Path(__file__).resolve().parents[1]


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def read_json(path):
    def unique_keys(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_keys)


def cached_file(spec, source_dir, fetch):
    name = spec["cacheName"]
    if not isinstance(name, str) or Path(name).name != name or name in ("", ".", ".."):
        raise ValueError("cacheName must be a plain file name")
    target = source_dir / name
    if not target.exists():
        if not fetch:
            raise ValueError("missing source: %s; use --fetch to download pinned files" % target)
        url = spec["url"]
        if not url.startswith("https://"):
            raise ValueError("source download requires HTTPS")
        with urlopen(url, timeout=60) as response:
            content = response.read(64 * 1024 * 1024 + 1)
        if len(content) > 64 * 1024 * 1024:
            raise ValueError("source exceeds 64 MiB: " + name)
        if sha256(content) != spec["sha256"]:
            raise ValueError("SHA256 mismatch for downloaded " + name)
        source_dir.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=source_dir, prefix=".idiom-download-", delete=False) as handle:
            temporary = Path(handle.name)
            handle.write(content)
        try:
            os.replace(str(temporary), str(target))
        finally:
            temporary.unlink(missing_ok=True)
    if sha256(target.read_bytes()) != spec["sha256"]:
        raise ValueError("SHA256 mismatch: " + name)
    return target


def load_inputs(manifest, source_dir, fetch):
    if manifest.get("schemaVersion") != 1 or not isinstance(manifest.get("ruleVersion"), str):
        raise ValueError("unsupported source manifest")
    records, seen_files = [], set()
    for spec in manifest["files"]:
        if spec["cacheName"] in seen_files:
            raise ValueError("duplicate source file in manifest")
        seen_files.add(spec["cacheName"])
        path = cached_file(spec, source_dir, fetch)
        content = read_json(path)
        source = spec["source"]
        if source == "jingmo":
            if not isinstance(content, dict) or not isinstance(content.get("data"), list):
                raise ValueError("invalid jingmo envelope: " + path.name)
            rows = content["data"]
        elif source == "xinhua":
            if not isinstance(content, list):
                raise ValueError("invalid xinhua envelope: " + path.name)
            rows = content
        else:
            raise ValueError("unknown source: " + str(source))
        if len(rows) != spec["expectedRecords"]:
            raise ValueError("record count mismatch: " + path.name)
        records.extend({"source": source, "file": path.name, "row": i, "data": row} for i, row in enumerate(rows))
    licenses = [(spec, cached_file(spec, source_dir, fetch)) for spec in manifest["licenses"]]
    return records, licenses


def load_exclusions(path):
    document = read_json(path)
    if document.get("schemaVersion") != 1 or not isinstance(document.get("entries"), list):
        raise ValueError("unsupported exclusions document")
    entries = {}
    for item in document["entries"]:
        word, reason = item["word"], item["reason"]
        if not isinstance(word, str) or not word.strip() or not isinstance(reason, str) or not reason.strip():
            raise ValueError("invalid exclusion")
        if word in entries:
            raise ValueError("duplicate exclusion: " + word)
        entries[word] = reason
    return entries


def validate_result(records, result):
    entries, invalid = result["entries"], result["invalidRecords"]
    def key(ref):
        return ref["source"], ref["file"], ref["row"]
    expected = Counter(key(record) for record in records)
    actual = Counter(key(ref) for entry in entries for ref in entry["sourceRecords"])
    actual.update(key(ref) for ref in invalid)
    if actual != expected:
        raise ValueError("input lineage mismatch: lost or duplicated source rows")
    ids = [entry["id"] for entry in entries]
    words = [entry["word"] for entry in entries]
    if len(set(ids)) != len(ids) or len(set(words)) != len(words):
        raise ValueError("duplicate output entry")
    for entry in entries:
        senses = {sense["id"]: sense for sense in entry["senses"]}
        if len(senses) != len(entry["senses"]):
            raise ValueError("duplicate sense: " + entry["word"])
        if entry["quality"]["eligibleForGame"]:
            if entry["quality"]["reasons"] or entry["gameSenseId"] not in senses:
                raise ValueError("invalid playable status: " + entry["word"])
            sense = senses[entry["gameSenseId"]]
            if len(entry["word"]) != 4 or len(sense["pinyin"]["keys"]) != 4 or not sense["explanation"]["meaning"]:
                raise ValueError("incomplete playable record: " + entry["word"])
        elif entry["gameSenseId"] is not None:
            raise ValueError("quarantined record has gameSenseId: " + entry["word"])


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(json_bytes(value))


def write_lines(path, rows):
    with path.open("wb") as handle:
        for row in rows:
            handle.write(json_bytes(row))


def export(stage, result, manifest, manifest_hash, exclusions_hash, licenses, font_hash):
    entries = sorted(result["entries"], key=lambda entry: entry["id"])
    invalid = sorted(result["invalidRecords"], key=lambda row: (row["source"], row["file"], row["row"]))
    playable = [entry for entry in entries if entry["quality"]["eligibleForGame"]]
    review = [entry for entry in entries if not entry["quality"]["eligibleForGame"]]
    write_lines(stage / "catalog.jsonl", entries)
    write_lines(stage / "review-required.jsonl", review)
    write_lines(stage / "invalid-records.jsonl", invalid)
    index, head_chars, head_keys = [], {}, {}
    for start in range(0, len(playable), 256):
        detail_file = "details/%04d.json" % (start // 256)
        details = []
        for entry in playable[start:start + 256]:
            sense = next(s for s in entry["senses"] if s["id"] == entry["gameSenseId"])
            word = entry["word"]
            keys = sense["pinyin"]["keys"]
            index.append({"id": entry["id"], "word": word, "senseId": sense["id"],
                          "pinyin": sense["pinyin"], "firstChar": word[0], "lastChar": word[-1],
                          "firstKey": keys[0], "lastKey": keys[-1], "detailFile": detail_file})
            head_chars.setdefault(word[0], []).append(entry["id"])
            head_keys.setdefault(keys[0], []).append(entry["id"])
            details.append({"id": entry["id"], "word": word, "sense": sense})
        write_json(stage / detail_file, {"schemaVersion": 1, "entries": details})
    write_json(stage / "game-index.json", {"schemaVersion": 1, "entries": index,
               "byFirstChar": head_chars, "byFirstSyllable": head_keys})
    source_rows = sum(len(entry["sourceRecords"]) for entry in entries)
    within = sum(sum(n - 1 for n in Counter(ref["source"] for ref in entry["sourceRecords"]).values()) for entry in entries)
    cross = sum(len(set(ref["source"] for ref in entry["sourceRecords"])) > 1 for entry in entries)
    counts = {"inputRecords": source_rows + len(invalid), "uniqueWords": len(entries),
              "playableWords": len(playable), "reviewWords": len(review), "invalidRecords": len(invalid),
              "mergedDuplicateRecords": source_rows - len(entries), "withinSourceDuplicateRecords": within,
              "crossSourceSharedWords": cross}
    reasons = Counter(reason for entry in review for reason in entry["quality"]["reasons"])
    script_files = [Path(__file__)] + sorted((ROOT / "scripts/idiom_pipeline").glob("*.py"))
    rules_digest = sha256(b"".join(p.name.encode() + b"\0" + p.read_bytes() + b"\0" for p in script_files))
    audit = {"schemaVersion": 1, "ruleVersion": manifest["ruleVersion"], "rulesSha256": rules_digest,
             "sourceManifestSha256": manifest_hash, "exclusionsSha256": exclusions_hash, "fontSha256": font_hash,
             "counts": counts, "exclusionReasonCounts": dict(sorted(reasons.items())),
             "normalizationStats": result["stats"], "sources": manifest["files"],
             "assurance": "Deterministic structural checks and known-conflict isolation; not a human linguistic review."}
    write_json(stage / "audit.json", audit)
    for spec, path in licenses:
        destination = stage / "licenses" / spec["cacheName"]
        destination.parent.mkdir(exist_ok=True)
        shutil.copyfile(path, destination)
    lines = ["# 成语词库脚本清洗报告", "", "本报告由脚本生成；未使用大模型改写、补义或猜读音。", "",
             "| 项目 | 数量 |", "| --- | ---: |"]
    labels = {"inputRecords": "源记录", "uniqueWords": "统一词条", "playableWords": "通过自动检查的游戏候选",
              "reviewWords": "保留但不进入游戏", "invalidRecords": "无法归并的源记录",
              "mergedDuplicateRecords": "归并的重复源记录", "withinSourceDuplicateRecords": "同库内重复源记录",
              "crossSourceSharedWords": "跨库同名词条"}
    lines += ["| %s | %s |" % (labels[key], value) for key, value in counts.items()]
    lines += ["", "## 隔离原因", "", "一条词可有多个原因，下列数量不能直接相加。", ""]
    lines += ["- `%s`：%s" % (key, value) for key, value in sorted(reasons.items())]
    lines += ["", "## 产物", "", "- `catalog.jsonl`：全量统一资料，保留来源及未解决版本。",
              "- `game-index.json` 与 `details/`：通过规则的四字候选及首字/首音索引。",
              "- `review-required.jsonl`：不进入游戏的记录和原因；非四字也保留在这里。",
              "- `invalid-records.jsonl`：无法取得合法词条的输入记录。",
              "- `audit.json`：源摘要、规则摘要、字体摘要和精确统计。",
              "- `checksums.json`：所有产物（自身除外）的 SHA256。", "",
              "## 结论边界", "", "通过自动检查不代表每个词都已人工考据。无法确定的读音或释义冲突保持隔离，原始文件保持不变。",
              "当前产物尚未接入 Unity，也未验证真机文字渲染。", ""]
    (stage / "report.md").write_text("\n".join(lines), encoding="utf-8")
    checksums = {p.relative_to(stage).as_posix(): sha256(p.read_bytes()) for p in sorted(stage.rglob("*")) if p.is_file()}
    write_json(stage / "checksums.json", checksums)
    return counts


def publish(stage, output):
    if not output.exists():
        os.replace(str(stage), str(output))
        return
    expected = read_json(stage / "checksums.json")
    actual_files = {p.relative_to(output).as_posix() for p in output.rglob("*") if p.is_file()}
    if actual_files != set(expected) | {"checksums.json"}:
        raise ValueError("output already exists with different contents; choose a new --output directory")
    if (output / "checksums.json").read_bytes() != (stage / "checksums.json").read_bytes():
        raise ValueError("output already exists for different inputs/rules; choose a new --output directory")
    for name, digest in expected.items():
        if sha256((output / name).read_bytes()) != digest:
            raise ValueError("existing output checksum mismatch: " + name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=ROOT / "Data/Idioms/source-manifest.json")
    parser.add_argument("--exclusions", type=Path, default=ROOT / "Data/Idioms/exclusions.json")
    parser.add_argument("--source-dir", type=Path, default=ROOT / "artifacts/idiom-research")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/idiom-cleaned")
    parser.add_argument("--fetch", action="store_true", help="download missing pinned sources; existing mismatches are never replaced")
    args = parser.parse_args()
    stage = None
    try:
        manifest = read_json(args.manifest)
        exclusions = load_exclusions(args.exclusions)
        records, licenses = load_inputs(manifest, args.source_dir, args.fetch)
        font = Path(manifest["font"]["path"])
        if not font.is_absolute():
            font = ROOT / font
        font_hash = sha256(font.read_bytes())
        if font_hash != manifest["font"]["sha256"]:
            raise ValueError("font SHA256 mismatch")
        coverage = read_font_codepoints(font)
        result = merge_records(records, exclusions, coverage)
        validate_result(records, result)
        output = args.output.resolve()
        if output.exists() and not (output / "checksums.json").is_file():
            raise ValueError("refusing to overwrite unknown output directory: " + str(output))
        output.parent.mkdir(parents=True, exist_ok=True)
        stage = Path(tempfile.mkdtemp(prefix=".idiom-import-", dir=output.parent))
        counts = export(stage, result, manifest, sha256(args.manifest.read_bytes()),
                        sha256(args.exclusions.read_bytes()), licenses, font_hash)
        publish(stage, output)
        print(json.dumps({"output": str(output), "counts": counts}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print("idiom import failed: " + str(error), file=sys.stderr)
        return 1
    finally:
        if stage is not None and stage.exists():
            shutil.rmtree(stage)


if __name__ == "__main__":
    sys.exit(main())
