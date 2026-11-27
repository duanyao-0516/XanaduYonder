#!/usr/bin/env python3
"""Index the real frames seen by XANADU_DIALOG_AUDIT and identify missing settings paths."""
import argparse
from collections import defaultdict
from html import escape
from functools import lru_cache
import gzip
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
    r'\b(?:CreateModal|CreateSheet|Dialog|ShowDialog|OpenPanel|OpenDialog|BeginDialog|ShowMessageDialog|OpenSheet|'
    r'OpenModal|OpenSheetPage|Confirm)\s*\(\s*"([^"\n]+)"\s*[,)]|'
    r'\bDialogTitle\(sheet,\s*width,\s*"([^"\n]+)"\s*\)', re.IGNORECASE)


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
    for (key, path), entry in sorted(pictures.items()):
        if not path.is_file():
            problems.append("Missing PNG " + str(path))
            continue
        with Image.open(path) as image:
            if image.size != (entry["width"], entry["height"]):
                problems.append("Unexpected dimensions in " + str(path))
            if all(low == high for low, high in image.convert("RGB").getextrema()):
                problems.append("Empty frame " + str(path))
        suite, game, overlay, window, kind = key
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
             f"四棋设置分组已核验 {summary['board_settings_verified']}/{required_sections}；源码标题已核验 {len(declared) - len(missing_declared)}/{len(declared)}。</p>"]
    for title, rows in (("源码声明缺少的窗口", missing_declared), ("基线缺少的窗口", missing_known), ("未采到实帧", unrendered), ("四棋缺少的实帧", missing_board), ("样式或文件异常", problems)):
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
    return bool(result["style_or_image_errors"] or result["missing_known_windows"] or (args.require_complete and
                (result["missing_board_settings"] or result["observed_without_frame"] or result["missing_source_windows"])))


if __name__ == "__main__":
    raise SystemExit(main())
