#!/usr/bin/env python3
"""Check repository Markdown file links, images, link definitions and heading fragments."""

from functools import lru_cache
import html
import os
from pathlib import Path
import posixpath
import re
import subprocess
import sys
import unicodedata
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})(.*)$")
CODE_SPAN = re.compile(r"(?<!`)(`+)(?!`)(.+?)(?<!`)\1(?!`)")
HEADING = re.compile(r"^ {0,3}#{1,6}(?:\s+|$)(.*?)\s*$")
EXPLICIT_ID = re.compile(r"""<a\s+(?:id|name)\s*=\s*["']([^"']+)["']""", re.I)
LINK_START = re.compile(r"(?<!\\)\]\(\s*|^ {0,3}\[[^\]]+\]:\s*")
ANGLE_TARGET = re.compile(r"<((?:\\.|[^<>])*)>")


@lru_cache(maxsize=None)
def prose_lines(path):
    """Retain source line numbers. Fence closers must match the opening marker and length."""
    result = []
    fence = ""
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = re.sub(r"^(?: {0,3}> ?)+", "", line)
        marker = FENCE.match(line)
        if fence:
            if marker and marker[1].startswith(fence) and not marker[2].strip():
                fence = ""
        elif marker:
            fence = marker[1]
        else:
            result.append((number, line))
    return result


def heading_slug(title):
    title = re.sub(r"!\[[^\]]*\]\([^)]*\)", "", title)
    title = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", title)
    title = re.sub(r"<[^>]*>|`+|\*+|~+", "", title)
    title = re.sub(r"(?<!\w)_+(.+?)_+(?!\w)", r"\1", title)
    title = html.unescape(title).strip().lower()
    return "".join(
        c for c in title if c in " -_" or unicodedata.category(c)[0] not in "PSC"
    ).replace(" ", "-")


@lru_cache(maxsize=None)
def anchors(path):
    ids = set()
    previous = ""
    for _, line in prose_lines(path):
        ids.update(EXPLICIT_ID.findall(CODE_SPAN.sub("", line)))
        heading = HEADING.match(line)
        if heading:
            title = re.sub(r"\s+#+\s*$", "", heading[1])
        elif previous and re.fullmatch(r" {0,3}(?:=+|-+)\s*", line):
            title = previous
        else:
            previous = line.strip()
            continue
        slug = heading_slug(title)
        unique = slug
        suffix = 0
        while unique in ids:
            suffix += 1
            unique = f"{slug}-{suffix}"
        ids.add(unique)
        previous = ""
    return ids


def destinations(line):
    visible = CODE_SPAN.sub("", line)
    for marker in LINK_START.finditer(visible):
        start = marker.end()
        if visible[start:start + 1] == "<":
            angle = ANGLE_TARGET.match(visible, start)
            if not angle:
                continue
            value = angle[1]
        else:
            # An unescaped ')' ends a bare target after its own parentheses balance.
            depth = 0
            escaped = False
            end = start
            while end < len(visible):
                char = visible[end]
                if not escaped:
                    if char.isspace() or (char == ")" and depth == 0):
                        break
                    if char == "(":
                        depth += 1
                    elif char == ")":
                        depth -= 1
                escaped = not escaped and char == "\\"
                end += 1
            value = visible[start:end]
        yield html.unescape(re.sub(r"\\([^\w\s])", r"\1", value))


def main():
    output = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT
    )
    files = {Path(os.fsdecode(raw)) for raw in output.split(b"\0") if raw}
    files = {path for path in files if (ROOT / path).is_file()}
    known = files | {parent for path in files for parent in path.parents}
    documents = sorted(path for path in files if path.suffix.lower() == ".md")
    checked_paths = checked_fragments = errors = 0
    for document in documents:
        for number, line in prose_lines(ROOT / document):
            for destination in destinations(line):
                url = urlsplit(destination)
                if url.scheme or url.netloc:
                    continue
                link_path = unquote(url.path)
                target = document
                if link_path:
                    checked_paths += 1
                    base = Path(".") if link_path.startswith("/") else document.parent
                    target = Path(posixpath.normpath(base / link_path.lstrip("/")))
                reason = ""
                if target not in known:
                    reason = "missing repository path"
                elif url.fragment and target in files and target.suffix.lower() == ".md":
                    checked_fragments += 1
                    if unquote(url.fragment) not in anchors(ROOT / target):
                        reason = "missing Markdown heading or anchor"
                if reason:
                    print(f"{document}:{number}: {reason}: {destination}", file=sys.stderr)
                    errors += 1
    print(f"{len(documents)} Markdown files, {checked_paths} local paths, "
          f"{checked_fragments} fragments, {errors} errors")
    return int(errors != 0)


if __name__ == "__main__":
    sys.exit(main())
