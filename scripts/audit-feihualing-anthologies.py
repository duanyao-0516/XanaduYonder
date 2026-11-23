#!/usr/bin/env python3
"""Compare the two locked anthologies to an installed offline catalog."""
import argparse
from collections import Counter, defaultdict
import gzip
import importlib.util
import json
from pathlib import Path
import subprocess
import sys


SCRIPT = Path(__file__).resolve().with_name("import-feihualing.py")
SPEC = importlib.util.spec_from_file_location("feihualing_importer", SCRIPT)
importer = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = importer
SPEC.loader.exec_module(importer)

TANG = "全唐诗/唐诗三百首.json"
RELATED = (TANG, 337)
ADOPTED = (TANG, 122)
EXCLUDED = (TANG, 176)


def index_works(catalog, provenance):
    locations = defaultdict(list)
    same_text = defaultdict(list)
    answers = defaultdict(set)
    works = catalog["works"]
    for work_index, source in enumerate(provenance):
        stable_id, _, path, row = source.split("\t")
        if works[work_index][0] != stable_id:
            raise ValueError(f"catalog and work provenance disagree at row {work_index}")
        locations[path, int(row)].append(work_index)
        same_text[importer.answer_chars(works[work_index][3])].append(work_index)

    bodies = [work[3].encode("utf-16-le") for work in works]
    retired = set(catalog.get("retired", []))
    for line_id, (work, start, size, _, _, aliases) in enumerate(catalog["lines"]):
        if line_id in retired:
            continue
        text = bodies[work][2 * start:2 * (start + size)].decode("utf-16-le")
        answers[work].update(map(importer.normalize_answer, [text, *aliases]))
    return locations, same_text, answers


def audit_edition(rows, path, works, locations, same_text, answers):
    summary = Counter()
    exceptions = []
    for number, row in enumerate(rows):
        coordinate = path, number
        author, title, body = importer.article_fields(row, importer.ANTHOLOGIES[path])
        units = [body[start:stop].strip() for start, stop in importer.token_spans(body)]
        if coordinate in locations:
            status = "direct"
            matches = locations[coordinate]
        elif coordinate == RELATED and ADOPTED in locations:
            status = "alternateEdition"
            matches = locations[ADOPTED]
        elif coordinate == EXCLUDED:
            status = "excludedAttribution"
            matches = []
        else:
            matches = same_text[importer.answer_chars(body)]
            same_author = [index for index in matches if works[index][1] == author]
            if same_author:
                status = "sameAuthorReprint"
                matches = same_author
            else:
                status = "differentAttribution" if matches else "missing"
        known = set().union(*(answers[index] for index in matches))
        absent = [line for line in units if importer.normalize_answer(line) not in known]
        summary[status] += 1
        summary["sourceUnits"] += len(units)
        summary["acceptedSourceUnits"] += len(units) - len(absent)
        if status != "direct" or absent:
            exceptions.append({
                "sourceRow": number, "author": author, "title": title, "status": status,
                "linkedWorks": [works[index][:3] for index in matches],
                "unmatchedSourceUnits": absent,
            })
    return {"sourceRows": len(rows), "counts": dict(summary), "exceptions": exceptions}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-cache", type=Path, required=True)
    parser.add_argument("--uconv", default="uconv")
    parser.add_argument("--catalog", type=Path, default=importer.ASSETS / "catalog.bytes")
    parser.add_argument("--provenance", type=Path, default=importer.DATA / "work-sources.tsv.gz")
    parser.add_argument("--output", type=Path, default=importer.DATA / "anthology-audit.json")
    args = parser.parse_args()

    catalog = json.loads(gzip.decompress(args.catalog.read_bytes()))
    provenance = gzip.decompress(args.provenance.read_bytes()).decode("utf-8").splitlines()[1:]
    if len(provenance) != len(catalog["works"]):
        raise ValueError("catalog and work provenance have different lengths")
    locations, same_text, answers = index_works(catalog, provenance)
    lock = json.loads(importer.SOURCE_LOCK.read_bytes())
    reports = {}
    for spec in lock["files"]:
        if spec["path"] not in importer.ANTHOLOGIES:
            continue
        path, raw = importer.checked_source(spec, lock, args.source_cache, False)
        converted = subprocess.check_output(
            [args.uconv, "-x", "Traditional-Simplified"], input=importer.json_bytes(json.loads(raw))
        )
        reports[path] = audit_edition(json.loads(converted), path, catalog["works"], locations, same_text, answers)
    result = {
        "corpus": catalog["corpus"],
        "method": "Count each pinned source record and punctuation unit. Report source forms absent from linked live answers.",
        "anthologies": reports,
    }
    importer.write_atomic(args.output, importer.json_bytes(result, pretty=True))


if __name__ == "__main__":
    main()
