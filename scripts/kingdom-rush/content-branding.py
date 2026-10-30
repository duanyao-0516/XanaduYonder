"""Player-facing first-generation branding; applied after content-patches.py.

Only exact, reviewed source anchors are accepted. All anchors are validated
before any file is written. Reapplying the module is an idempotent no-op.
Original source attribution and immutable save/version fields are documented in
ThirdParty/KingdomRushContent/branding-audit.md, not renamed by this module.
"""
from pathlib import Path
import argparse
import hashlib
import io
import json
import re
import zipfile


TITLE = "王国保卫战 · 初代"
REMOVED_ATLAS_FRAMES = {
    "kr3-desktop/assets/images/fullhd/screen_slots.lua": [
        "main_menu_bg", "main_menu_subtitle_cn",
    ],
}

_SUBTITLE = '''\t\t\t\t{
\t\t\t\t\tid = "main_menu_subtitle_cn",
\t\t\t\t\thidden = true,
\t\t\t\t\tclass = "KImageView",
\t\t\t\t\timage_name = "main_menu_subtitle_cn"
\t\t\t\t},'''
_FIRSTGEN_MENU_LAYERS = '''\t\t\t\t{
\t\t\t\t\tid = "firstgen_menu_shade",
\t\t\t\t\tclass = "KView",
\t\t\t\t\tsize = {x = 1920, y = 1080},
\t\t\t\t\tcolors = {background = {8, 20, 14, 165}},
\t\t\t\t\tpropagate_on_click = true,
\t\t\t\t\tpropagate_on_down = true,
\t\t\t\t\tpropagate_on_up = true,
\t\t\t\t\tfocus_nav_ignore = true
\t\t\t\t},
\t\t\t\t{
\t\t\t\t\tid = "firstgen_menu_title",
\t\t\t\t\tclass = "GGLabel",
\t\t\t\t\ttext = "王国保卫战 · 初代",
\t\t\t\t\tfont_name = "sans_bold",
\t\t\t\t\tfont_size = 60,
\t\t\t\t\ttext_align = "center",
\t\t\t\t\ttext_shadow = true,
\t\t\t\t\tfit_lines = 1,
\t\t\t\t\tpos = {x = 410, y = 110},
\t\t\t\t\tsize = {x = 1100, y = 140},
\t\t\t\t\tcolors = {text = {250, 244, 213, 255}},
\t\t\t\t\tpropagate_on_click = true,
\t\t\t\t\tpropagate_on_down = true,
\t\t\t\t\tpropagate_on_up = true,
\t\t\t\t\tfocus_nav_ignore = true
\t\t\t\t},'''

_WINDOW_TITLE = 'love.window.setTitle(string.format(_("GAME_TITLE_" .. string.upper(KR_GAME)), KR_FL_VERSION))'
_EDITS = [
    ("conf.lua", 't.window.title = "王国保卫战"', f't.window.title = "{TITLE}"'),
    ("main.lua", _WINDOW_TITLE, f'love.window.setTitle("{TITLE}")'),
    ("all/director.lua", _WINDOW_TITLE, f'love.window.setTitle("{TITLE}")'),
    ("version.lua", 'version.title = "KRFL"', f'version.title = "{TITLE}"'),
    ("all-desktop/screen_settings.lua",
     'l_ver.text = string.format("FL ver. %s modby liuhui349", KR_FL_VERSION or "NA")',
     f'l_ver.text = "{TITLE}"'),
    ("all/screen_credits.lua", '\t\tvl.font_name = "numbers_italic"\n\t\tvl.text = version.string_short',
     f'\t\tvl.font_name = "sans"\n\t\tvl.text = "{TITLE}"'),
    ("all/screen_credits.lua", '\t\tvl.font_name = "numbers_italic"\n\t\tvl.text = version.string .. (version.bundle_keywords or "")',
     '\t\tvl.font_name = "sans"\n\t\tvl.text = version.title'),
    ("kr3-desktop/assets/strings/zh-Hans_3.lua",
     'GAME_TITLE_KR3 = "王国保卫战 六世同堂版(FluidLight8.0/FL8.0) 版本号：%s 作者：流辉一枪349",',
     f'GAME_TITLE_KR3 = "{TITLE}",'),
    ("kr3-desktop/assets/strings/en_3.lua",
     'GAME_TITLE_KR3 = "Kingdom Rush FluidLight v%s MODBY a shot of fluidlight causes 349 points of damage on Elaina",',
     f'GAME_TITLE_KR3 = "{TITLE}",'),
    ("main.lua", '''\tlocal pt = string.format(
\t\t"Version %s\\n\\n出了一些错误。如果你不想被嘲讽连中文都不认识的话，请将此截图发到作者的发布贴或发布视频，并详细描述局内的情况。",
\t\tKR_FL_VERSION)''',
     f'\tlocal pt = "{TITLE}\\n\\n出现错误，请保存此页面截图并记录触发步骤。"'),
    ("kr3-desktop/assets/strings/zh-Hans_5sp.lua",
     '在邪恶的力量卷土重来之前！本关来自KRDove版，在此表示感谢。",',
     '在邪恶的力量卷土重来之前！",'),
    ("all-desktop/screen_map.lua",
     'local SHARED_TECH_TREE_HELP_TEXT = "1、防御塔到了任意关卡，都能吃且只能吃防御塔自身所在代的科技。例如使用T200战斗机甲玩3代关卡，吃2代的震慑科技，但不会因为玩的是3代而吃3代的巨石震撼科技，也不会吃1代的智能瞄准科技。\\n\\n2、不同代的援军和天灾都有各自独立的科技树，需要分别升级。4/5代援军均存在2选1的分支，进入关卡后会根据选择的分支来使用对应的军种。"',
     'local SHARED_TECH_TREE_HELP_TEXT = "完成关卡可获得星星，用于提升初代的六类科技：弓箭塔、兵营、法师塔、火炮塔、呼叫援军和天降火雨。\\n\\n每项科技的说明会显示其效果与星星消耗。请根据战术分配星星，已购买的科技将在后续战斗中生效。"'),
    ("all-desktop/screen_slots.lua", '''screen.required_textures = {
\t"screen_slots",
\t"view_options"
}''', '''screen.required_textures = {
\t"screen_slots",
\t"screen_map_123",
\t"view_options"
}'''),
    ("all-desktop/screen_slots.lua", '''\tif i18n.current_locale == "zh-Hans" and KR_GAME == "kr2" then
\t\twindow:get_child_by_id("main_menu_subtitle_cn").hidden = false
\tend''', '\t-- firstgen: the foreign-edition subtitle is not constructed.'),
    ("kr3-desktop/data/kui_templates/screen_slots.lua", '''\t\t\tid = "bg_view",
\t\t\tclass = "KImageView",
\t\t\timage_name = "main_menu_bg",''', '''\t\t\tid = "bg_view",
\t\t\tclass = "KImageView",
\t\t\timage_name = "map_background1",'''),
    ("kr3-desktop/data/kui_templates/screen_slots.lua", _SUBTITLE, _FIRSTGEN_MENU_LAYERS),
]


def _replace_one(text, old, new, path):
    """Accept the exact original or exact already-patched anchor, never drift."""
    originals = [(old, new), (old.replace("\n", "\r\n"), new.replace("\n", "\r\n"))]
    originals = list(dict.fromkeys(originals))
    matches = [(a, b, text.count(a)) for a, b in originals]
    count = sum(n for _, _, n in matches)
    patched = list(dict.fromkeys([new, new.replace("\n", "\r\n")]))
    patched_count = sum(text.count(a) for a in patched)
    if count == 1 and patched_count == 0:
        a, b, _ = next(item for item in matches if item[2])
        return text.replace(a, b, 1)
    if count == 0 and patched_count == 1:
        return text
    raise ValueError(f"Branding source drift in {path}: original={count}, patched={patched_count}: {old[:100]!r}")


def patch_tree(root: Path):
    root = Path(root)
    originals = {}
    pending = {}
    for path, old, new in _EDITS:
        if path not in pending:
            originals[path] = (root / path).read_bytes()
            pending[path] = originals[path].decode("utf-8")
        pending[path] = _replace_one(pending[path], old, new, path)
    # The physical atlas pruner may now delete these two records and pixel areas.
    for path in ("all-desktop/screen_slots.lua", "kr3-desktop/data/kui_templates/screen_slots.lua"):
        for frame in REMOVED_ATLAS_FRAMES["kr3-desktop/assets/images/fullhd/screen_slots.lua"]:
            if '"' + frame + '"' in pending[path]:
                raise ValueError(f"Foreign menu frame still referenced: {path}: {frame}")
    changed = {}
    for path, text in pending.items():
        data = text.encode("utf-8")
        if data != originals[path]:
            changed[path] = {
                "before_sha256": hashlib.sha256(originals[path]).hexdigest(),
                "after_sha256": hashlib.sha256(data).hexdigest(),
            }
    # No writes occur until every source anchor and remaining-reference check passes.
    for path in changed:
        (root / path).write_bytes(pending[path].encode("utf-8"))
    return changed


def validate_archive(archive: Path, expected_game: Path = None, physical_report: Path = None):
    """Verify the actual game ZIP, or the actual assets/game.love inside an APK.

    An APK requires an explicit current game archive, so a stale AAR cannot pass.
    Optionally bind the content owner's physical-removal evidence to these bytes.
    """
    archive = Path(archive)
    outer_bytes = archive.read_bytes()
    with zipfile.ZipFile(io.BytesIO(outer_bytes)) as outer:
        names = outer.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate archive paths")
        is_apk = "assets/game.love" in names
        if is_apk and expected_game is None:
            raise ValueError("APK verification requires --expected-game to exclude a stale content AAR")
        game_bytes = outer.read("assets/game.love") if is_apk else outer_bytes
    game_sha = hashlib.sha256(game_bytes).hexdigest()
    if expected_game is not None and hashlib.sha256(Path(expected_game).read_bytes()).hexdigest() != game_sha:
        raise ValueError("Archive contains a different game.love than --expected-game")
    checked_paths = set()
    with zipfile.ZipFile(io.BytesIO(game_bytes)) as content:
        names = content.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate game.love paths")
        if content.testzip() is not None:
            raise ValueError("game.love ZIP CRC failure")
        for path, old, new in _EDITS:
            text = content.read(path).decode("utf-8").replace("\r\n", "\n")
            if old in text or text.count(new) != 1:
                raise ValueError(f"Missing final branding or old visible text remains: {path}: {old[:100]!r}")
            checked_paths.add(path)
        # The old pixels must have been handled by the separate physical pruner;
        # removing only display references cannot satisfy this archive contract.
        for path, frames in REMOVED_ATLAS_FRAMES.items():
            text = content.read(path).decode("utf-8-sig")
            for frame in frames:
                field = re.escape(frame)
                if re.search(r'(?:\b' + field + r'\s*=|\[\s*["\x27]' + field + r'["\x27]\s*\]\s*=)', text):
                    raise ValueError(f"Foreign menu atlas frame still packaged: {path}: {frame}")
        physical_sha = None
        verified_textures = 0
        if physical_report is not None:
            report_bytes = Path(physical_report).read_bytes()
            physical_sha = hashlib.sha256(report_bytes).hexdigest()
            report = json.loads(report_bytes)
            removals = {(row["group"], row["frame"]) for row in report["removed_frames"]}
            if not {("screen_slots", name) for name in REMOVED_ATLAS_FRAMES["kr3-desktop/assets/images/fullhd/screen_slots.lua"]} <= removals:
                raise ValueError("Physical-removal report does not cover both foreign menu frames")
            for row in report["textures"]:
                if hashlib.sha256(content.read(row["path"])).hexdigest() != row["after_sha256"]:
                    raise ValueError("Physical-removal report does not describe packaged texture: " + row["path"])
                for flag in ("retained_blocks_byte_identical", "retained_decoded_pixels_with_sampling_margin_identical", "cleared_decoded_blocks_transparent"):
                    if row.get(flag) is not True:
                        raise ValueError("Physical-removal verification did not pass: " + flag)
                verified_textures += 1
    return {
        "archive": str(archive.resolve()),
        "archive_sha256": hashlib.sha256(outer_bytes).hexdigest(),
        "archive_is_apk": is_apk,
        "game_sha256": game_sha,
        "expected_game_sha256_matched": expected_game is not None,
        "display_title": TITLE,
        "checked_source_files": sorted(checked_paths),
        "window_title_sites": 2,
        "conf_locale_credits_settings_error_menu_branding_verified": True,
        "foreign_menu_references_and_index_records_removed": True,
        "physical_removal_report_sha256": physical_sha,
        "physical_removal_textures_bound_to_archive": verified_textures,
        "device_or_game_runtime_tested": False,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path, nargs="?")
    parser.add_argument("--verify", type=Path, help="Inspect game.love or APK without writing it")
    parser.add_argument("--expected-game", type=Path, help="Current game.love; required when verifying APK")
    parser.add_argument("--physical-report", type=Path, help="Bind physical-removal-report.json texture hashes")
    parser.add_argument("--report", type=Path, help="Write the verification result JSON")
    args = parser.parse_args()
    if bool(args.root) == bool(args.verify):
        parser.error("Choose a staging root to patch OR --verify archive")
    result = validate_archive(args.verify, args.expected_game, args.physical_report) if args.verify else patch_tree(args.root)
    rendered = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
