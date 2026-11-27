#!/usr/bin/env python3
"""Index the real frames seen by XANADU_DIALOG_AUDIT and identify missing settings paths."""
import argparse
from collections import defaultdict
from html import escape
from functools import lru_cache
import gzip
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import quote

from PIL import Image


BOARD_SECTIONS = {"落子与显示", "声音与触感", "对局管理", "继续其他对局", "本局棋谱"}
BOARDS = {"Army", "Chess", "Xiangqi", "Checkers"}
BOARD_SIZES = {(360, 720), (320, 568)}
BASELINE = Path(__file__).with_name("dialog-audit-baseline.json")


@lru_cache(maxsize=1)
def idiom_titles():
    path = Path(__file__).resolve().parents[1] / "Assets/Resources/Idioms/catalog.bytes"
    with gzip.open(path, "rt", encoding="utf-8") as catalog:
        return {row[1] for row in json.load(catalog)["entries"]}


def semantic_key(entry):
    """Use route/role names when the visible headline includes a score, level, topic or answer."""
    game, overlay, title = (entry[k] for k in ("game", "overlay", "window"))
    if game in ("Fixture canvas", "XanaduYonder UI"):
        game = "Host"
    elif game == "2048 host":
        game = "2048"
    if game == "Sheep" and overlay in ("新局道具次数", "旸了个旸 · 设置", "第二关难度", "选择第二关难度"):
        title = overlay
    elif game == "Idiom":
        if "次提示" in title:
            title = "提示线索"
        elif entry.get("role") == "成语释义" or title in idiom_titles():
            title = "成语释义"
    elif game == "Sudoku" and entry.get("role"):
        title = entry["role"]
    elif game == "Snake" and re.match(r"^\d+\s", title):
        title = "关卡说明"
    elif game == "Tetris" and overlay == "Tetris arena result":
        title = "本局结束"
    elif game == "HappyBall" and re.fullmatch(r"本局\s*\d+", title):
        title = "本局成绩"
    elif game == "LinkMatch":
        title = re.sub(r"第\s*\d+\s*关", "关卡", title)
    elif game == "GoldMiner" and re.fullmatch(r"第\s*\d+\s*关", title):
        title = "关卡目标"
    elif game == "Gomoku":
        title = re.sub(r"玩家\s*\d+\s*获胜", "本地棋局胜负", title)
    return game, title



RUNTIME = Path(__file__).resolve().parents[1] / "Assets/Scripts/Runtime"
SOURCE_CALLS = re.compile(
    r'\b(?:CreateModal|CreateReadingModal|CreateSheet|Dialog|ShowDialog|OpenPanel|OpenDialog|BeginDialog|ShowMessageDialog|OpenSheet|'
    r'OpenModal|OpenSheetPage|Confirm)\s*\(\s*"([^"\n]+)"\s*[,)]|'
    r'\bDialogTitle\s*\(\s*sheet\s*,\s*width\s*,\s*"([^"\n]+)"\s*[,)]', re.IGNORECASE)


def source_windows(rendered):
    """Find literal product dialog headings, including delegates. Dynamic titles are in the role baseline."""
    locations = defaultdict(list)
    for file in sorted(RUNTIME.rglob("*.cs")):
        source = file.read_text(encoding="utf-8")
        relative = file.relative_to(RUNTIME)
        for match in SOURCE_CALLS.finditer(source):
            title = match.group(1) or match.group(2)
            if not re.search(r"[\u3400-\u9fff]", title):
                continue  # Internal route identifiers such as LinkMatch's "restart" are not headings.
            game = relative.parts[0]
            if file.name == "AppController.cs":
                game = "Host" if title == "退出应用？" else "2048"
            elif game == "App":
                game = "Bakery" if file.name == "AppController.BakeryBasket.cs" else "Host"
            locations[game, title].append(str(relative) + ":" + str(source.count("\n", 0, match.start()) + 1))
    return [{"game": game, "window": window, "locations": where,
             "verified": any((owner, window) in rendered for owner in (BOARDS if game == "BoardGames" else {game}))}
            for (game, window), where in sorted(locations.items())]


def alignment_issues(entry):
    """Compare the text column inside one rendered surface, normalized to a width of 360."""
    labels = entry.get("alignmentLabels")
    if not labels:
        return ["缺少本帧的文字布局测量"]
    errors = []
    title = next((label for label in labels if label["role"] == "heading"), None)
    sections = [label for label in labels if label["role"] == "section"]
    sheet = entry["presentation"] == "sheet"
    rows = [label for label in labels if label["role"] in ("disclosure", "switch") and not label["compact"]]
    navigation = [label for label in rows if label["role"] == "disclosure"]
    prose = [label for label in labels if label["role"] == "label" and label["width"] >= 215
             and any(char.isalpha() for char in label["text"])]
    # Map paragraphs, full-width control captions, and section headers to separate columns.
    # A compact two-column choice, numeric stepper or confirmation button has its own internal alignment.
    def left(label):
        return label["alignment"].endswith("Left")

    def snippet(label):
        return repr(label["text"].splitlines()[0][:16])

    def need_left(label, category):
        if not left(label):
            errors.append(f"{category} {snippet(label)} 使用 {label['alignment']}")

    def same_column(category, items):
        if len(items) < 2:
            return
        lo, hi = min(items, key=lambda item: item["left"]), max(items, key=lambda item: item["left"])
        # Approximately one pixel on a 320-wide capture with an inset rounded independently at both ends.
        if hi["left"] - lo["left"] > 2:
            errors.append(f"{category} {snippet(lo)} 和 {snippet(hi)} 的左边线为 {lo['left']:.1f} 和 {hi['left']:.1f}")

    # A numbered directory is identifiable even if every item was built as a plain action button.
    numbered_rows = [label for label in labels if label["role"] in ("control", "disclosure") and
                     not label["compact"] and re.match(r"^\s*\d+(?:[.．、]\s*|\s+)\D", label["text"])]
    if len(numbered_rows) >= 2:
        for row in numbered_rows:
            need_left(row, "编号目录项")
        same_column("编号目录项", numbered_rows)

    controls = defaultdict(list)
    for label in labels:
        if label["role"] == "control" and not label["compact"] and label["width"] >= 215 and label.get("control"):
            controls[label.get("controlId") or label["control"]].append(label)
    descriptions = []
    for group in controls.values():
        # Two stacked captions of similar width are a row title and description, including
        # custom navigation/choice rows implemented without the shared Disclosure component.
        if len(group) == 2 and 2 < abs(group[0]["top"] - group[1]["top"]) <= 40 and abs(group[0]["width"] - group[1]["width"]) < 8:
            for label in group:
                need_left(label, "菜单项标题和说明")
            descriptions.extend(group)
    same_column("菜单项标题和说明", descriptions)

    for row in rows:
        need_left(row, "通栏设置/菜单项")
    same_column("同列菜单标签", navigation)
    same_column("同列开关标签", [label for label in rows if label["role"] == "switch"])
    nav_groups = defaultdict(list)
    for row in navigation:
        nav_groups[row.get("group")].append(row)
    for group, siblings in nav_groups.items():
        if len(siblings) < 2:
            continue
        top, bottom = min(row["top"] for row in siblings), max(row["top"] for row in siblings)
        for label in labels:
            if (label["role"] == "control" and label.get("group") == group and not label["compact"] and top < label["top"] < bottom
                    and any(abs(label["controlLeft"] - row["controlLeft"]) < 2 and
                            abs(label["controlWidth"] - row["controlWidth"]) < 2 for row in siblings)):
                need_left(label, "同一组菜单项")

    if title is None:
        return errors  # Label-only HUD covers still have an inventory of every rendered text/control.
    # Numbered instructions use hanging indents. Match each leading number against
    # the adjacent paragraph in its row and validate both columns independently.
    markers = [label for label in labels if label["role"] == "label" and label["text"].isdigit() and label["width"] <= 45]
    numbered = [(marker, body) for body in prose for marker in markers
                if abs(body["top"] - marker["top"]) <= 3 and
                0 <= body["left"] - marker["left"] - marker["width"] <= 32]
    if len({marker["text"] for marker, _ in numbered}) < 2:
        numbered = []
    numbered_bodies = [body for _, body in numbered]
    numbered_markers = [marker for marker, _ in numbered]
    for body in numbered_bodies:
        need_left(body, "编号条目正文")
    same_column("编号条目正文", numbered_bodies)
    same_column("编号列", numbered_markers)
    read = []
    for label in prose:
        if label in numbered_bodies:
            continue
        paragraph = label["text"]
        detailed = len(paragraph) >= 55 and (paragraph.count("。") >= 2 or
                    ("\n\n" in paragraph and any(sum(c.isalpha() for c in line) >= 28 for line in paragraph.splitlines())))
        sentence = any(char in paragraph for char in "。！？")
        if ((label["reading"] and (left(label) or detailed or left(title))) or
                (label["direct"] and (detailed or (left(title) and sentence)))):
            read.append(label)
    # Only actual reading groups (including compact informational cards with a left-aligned message)
    # bind the heading to the paragraph. A centered score and its short centered status remain a valid result card.
    plain = [label for label in prose if label["direct"] and left(label) and label not in numbered_bodies]
    if sheet or read or plain or numbered or descriptions or len(navigation) >= 2 or len(numbered_rows) >= 2:
        need_left(title, "阅读/设置标题")
        for label in sections + read:
            need_left(label, "分组或正文")
        peers = [*sections, *numbered_markers]
        if not sheet and len(navigation) >= 2:
            peers.extend(navigation)
        cards = defaultdict(list)
        for label in read:
            if label.get("card"):
                cards[label["card"]].append(label)
            else:
                peers.append(label)
        for card in cards.values():
            same_column("卡片内正文", card)
        if sheet:
            for label in prose:
                if label["direct"] and label not in peers and label not in numbered_bodies:
                    need_left(label, "设置说明")
                    peers.append(label)
        else:
            peers.extend(label for label in plain if label not in peers)
        same_column("标题与正文", [title, *peers])
    return errors


def image_issues(path, entry, root, pixel_owners):
    errors = []
    with Image.open(path) as image:
        if image.size != (entry["width"], entry["height"]):
            errors.append("Unexpected dimensions in " + str(path))
        pixels = image.convert("RGB")
        if all(low == high for low, high in pixels.getextrema()):
            errors.append("Empty frame " + str(path))
        digest = (image.size, hashlib.sha256(pixels.tobytes()).digest())
        owner, first_path = pixel_owners.setdefault(digest, (entry["window"], path))
        if owner != entry["window"]:
            errors.append(f"不同标题共享完全相同的实帧：{first_path.relative_to(root)} / {path.relative_to(root)}")
    return errors


def review(root):
    pictures = {}
    observed = set()
    observed_roles = set()
    diagnostics = set()
    unpresented = set()
    for manifest in sorted(root.glob("*/windows.jsonl")):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            entry = json.loads(line)
            key = tuple(entry[k] for k in ("suite", "game", "overlay", "window", "presentation"))
            # Tetris constructs its reusable overlay with a blank title and a placeholder record
            # before hiding it; every presented state subsequently sets its actual heading.
            empty_tetris = entry["game"] == "Tetris" and entry["overlay"] == "Tetris overlay" and re.fullmatch(r"最高纪录\s*\d+", entry["window"])
            empty_sheep = entry["game"] == "Sheep" and entry["overlay"] == "Round result backdrop" and entry["window"] == "重新开始"
            if not entry.get("png") and (empty_tetris or empty_sheep):
                unpresented.add(key)
                continue
            if entry["suite"] == "host-save-isolation" or entry["game"] == "Outside tap fixture" or (entry["game"] == "Fixture canvas" and entry["window"] in ("提示", "说明")) or entry["window"] in ("真实外侧点击", "真实成语弹窗", "确认操作？", "普通确认", "点击范围检查"):
                diagnostics.add(key)
                continue
            observed.add(key)
            observed_roles.add(semantic_key(entry))
            if entry.get("png"):
                path = manifest.parent / entry["png"]
                pictures[(key, path)] = entry
    if not observed:
        raise SystemExit("No dialog manifests in " + str(root))

    confirmed = set()
    roles = set()
    raw_windows = set()
    evidence = defaultdict(list)
    problems = []
    alignment_frames = 0
    alignment_failures = []
    pixel_owners = {}
    for (key, path), entry in sorted(pictures.items()):
        if not path.is_file():
            problems.append("Missing PNG " + str(path))
            continue
        problems.extend(image_issues(path, entry, root, pixel_owners))
        suite, game, overlay, window, kind = key
        issues = alignment_issues(entry)
        if not issues:
            alignment_frames += 1
        else:
            alignment_failures.extend(str(path.relative_to(root)) + " · " + issue for issue in issues)
        roles.add(semantic_key(entry))
        owner, _ = semantic_key(entry)
        raw_windows.add((owner, window))
        if kind == "sheet":
            if not entry["bottomAligned"]:
                problems.append(f"{suite} / {game} / {window} has a gap at the canvas edge")
            if entry["scrimAlpha"] <= 0:
                problems.append(f"{suite} / {game} / {window} has no dimming")
            if suite == "classic-boards-ui" and window == "对局设置" and entry.get("section"):
                confirmed.add((game, entry["section"], entry["width"], entry["height"]))
        evidence[(suite, game)].append((entry, path.relative_to(root).as_posix()))
    required_sections = len(BOARDS) * len(BOARD_SECTIONS) * len(BOARD_SIZES)
    missing_board = sorted(f"{game} / {window} / {w}x{h}"
                           for game in BOARDS for window in BOARD_SECTIONS for w, h in BOARD_SIZES
                           if (game, window, w, h) not in confirmed)
    # The same production screen is often opened by both a state-only and a visual suite.
    unrendered = [" / ".join(key) for key in sorted(observed_roles - roles)]
    baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
    known = {(game, window) for game, windows in baseline.items() for window in windows}
    missing_known = [" / ".join(key) for key in sorted(known - roles)]
    declared = source_windows(raw_windows)
    missing_declared = [row["game"] + " / " + row["window"] + " (" + ", ".join(row["locations"]) + ")"
                        for row in declared if not row["verified"]]
    (root / "source-declarations.json").write_text(json.dumps(declared, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    summary = {"source_windows_required": len(declared), "source_windows_verified": len(declared) - len(missing_declared),
               "missing_source_windows": missing_declared, "unpresented_templates": [" / ".join(key) for key in sorted(unpresented)], "interaction_fixture_windows": len(diagnostics), "observed_windows": len(observed_roles), "rendered_windows": len(roles),
               "known_windows_required": len(known), "known_windows_verified": len(known & roles),
               "missing_known_windows": missing_known,
               "alignment_verified_frames": alignment_frames, "alignment_errors": alignment_failures,
               "screenshots": sum(map(len, evidence.values())), "board_settings_required": required_sections,
               "board_settings_verified": required_sections - len(missing_board), "missing_board_settings": missing_board,
               "observed_without_frame": unrendered, "style_or_image_errors": sorted(set(problems))}
    (root / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    cards = ["<!doctype html><html lang='zh'><meta charset='utf-8'><title>弹窗画面核验</title>"
             "<style>body{font:16px system-ui;margin:24px;background:#fafaf7;color:#202b23}"
             "section{display:grid;grid-template-columns:repeat(auto-fill,minmax(215px,1fr));gap:16px}"
             "figure{margin:0;padding:12px;border:1px solid #d8ddd8;border-radius:12px;background:white}"
             "img{width:100%;height:310px;object-fit:contain}small{color:#506055}summary{cursor:pointer}</style>",
             f"<h1>弹窗画面核验</h1><p>记录 {len(observed_roles)} 类窗口，{len(roles)} 类有实帧，共 {summary['screenshots']} 张。"
             f"文字对齐通过 {alignment_frames}/{summary['screenshots']} 帧，四棋设置分组已核验 {summary['board_settings_verified']}/{required_sections}；源码标题已核验 {len(declared) - len(missing_declared)}/{len(declared)}。</p>"]
    for title, rows in (("源码声明缺少的窗口", missing_declared), ("基线缺少的窗口", missing_known), ("未采到实帧", unrendered), ("四棋缺少的实帧", missing_board), ("样式或文件异常", problems), ("文字对齐异常", alignment_failures)):
        if rows:
            cards.append(f"<details><summary>{title} · {len(rows)}</summary><ul>")
            cards.extend("<li>" + escape(row) + "</li>" for row in rows)
            cards.append("</ul></details>")
    for (suite, game), entries in sorted(evidence.items()):
        cards.append(f"<h2>{escape(game)} <small>{escape(suite)}</small></h2><section>")
        for entry, path in entries:
            url = quote(path, safe="/")
            cards.append(f"<figure><a href='{url}'><img loading='lazy' src='{url}'></a>"
                         f"<figcaption>{escape(entry['window'] + (' · ' + entry['section'] if entry.get('section') else ''))}<br><small>{entry['width']} × {entry['height']} · "
                         f"{'底部面板' if entry['presentation'] == 'sheet' else '对话框'}</small></figcaption></figure>")
        cards.append("</section>")
    (root / "index.html").write_text("\n".join(cards), encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if not isinstance(v, list)}, ensure_ascii=False))
    print("Open", root / "index.html")
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--require-complete", action="store_true", help="also require a real frame for every observed window and board setting")
    args = parser.parse_args()
    result = review(args.directory.resolve())
    return bool(result["alignment_errors"] or result["style_or_image_errors"] or result["missing_known_windows"] or (args.require_complete and
                (result["missing_board_settings"] or result["observed_without_frame"] or result["missing_source_windows"])))


if __name__ == "__main__":
    raise SystemExit(main())
