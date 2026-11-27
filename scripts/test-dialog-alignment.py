#!/usr/bin/env python3
"""Exercise the dialog report rules, including the mixed menu regression."""
import runpy
import tempfile
import unittest
from pathlib import Path

from PIL import Image

report = runpy.run_path(str(Path(__file__).with_name("dialog-audit-report.py")))
check = report["alignment_issues"]


def label(role, text, left=20, alignment="MiddleLeft", top=0, width=320, **overrides):
    return dict(dict(role=role, text=text, left=left, alignment=alignment, top=top, width=width,
                     compact=False, controlLeft=10, controlWidth=340, direct=role in ("heading", "label", "section"),
                     reading=False), **overrides)


class AlignmentChecks(unittest.TestCase):
    def check_card(self, *labels, sheet=False):
        return check({"presentation": "sheet" if sheet else "dialog", "alignmentLabels": labels})

    def test_board_source_title_accepts_optional_layout_arguments(self):
        calls = report["SOURCE_CALLS"]
        source = ('DialogTitle(sheet, width, "玩法说明");\n'
                  'DialogTitle(sheet, width, "教程目录", margin + DialogUi.DisclosureInset(listWidth, labelSize));')
        self.assertEqual(["玩法说明", "教程目录"], [match.group(2) for match in calls.finditer(source)])

    def test_centered_option_among_navigation_rows_fails(self):
        rows = [label("heading", "模式"), label("disclosure", "挑战", top=54),
                label("control", "热身", alignment="MiddleCenter", top=112),
                label("disclosure", "玩法设置", top=174), label("control", "返回", alignment="MiddleCenter", top=235)]
        self.assertTrue(any("热身" in error for error in self.check_card(*rows)))
        rows[2]["role"] = "disclosure"
        rows[2]["alignment"] = "MiddleLeft"
        self.assertEqual([], self.check_card(*rows))
        for row in rows[1:4]:
            row["group"] = 91
        rows[-1].update(group=92, top=125)
        self.assertEqual([], self.check_card(*rows))

    def test_heading_and_reading_paragraphs_measure_the_same_column(self):
        title = label("heading", "玩法", alignment="MiddleCenter")
        body = label("label", "局面的操作说明。", reading=True, direct=False)
        self.assertTrue(self.check_card(title, body))
        title["alignment"] = "UpperLeft"
        title["left"] += 6
        self.assertTrue(self.check_card(title, body))
        title["left"] = body["left"]
        self.assertEqual([], self.check_card(title, body))
        body["alignment"] = "UpperCenter"
        self.assertTrue(self.check_card(title, body))

    def test_all_centered_numbered_directory_fails_even_with_no_disclosures(self):
        title = label("heading", "教程目录")
        items = [label("control", f"{i}. 课程", alignment="MiddleCenter", top=i*48) for i in range(1, 4)]
        footer = label("control", "返回", alignment="MiddleCenter", top=250)
        self.assertEqual(3, len(self.check_card(title, *items, footer)))
        for item in items:
            item["alignment"] = "MiddleLeft"
        self.assertEqual([], self.check_card(title, *items, footer))

    def test_custom_navigation_caption_and_description_are_checked_together(self):
        title = label("heading", "展示方式")
        row = label("control", "双列卡片", top=62, control="layout")
        description = label("control", "图标与名称同行", alignment="MiddleCenter", top=87, control="layout")
        self.assertTrue(self.check_card(title, row, description))
        description["alignment"] = "MiddleLeft"
        self.assertEqual([], self.check_card(title, row, description))

    def test_sheet_field_and_section_alignment(self):
        rows = [label("heading", "设置"), label("section", "难度"), label("section", "显示", left=14),
                label("switch", "显示坐标"), label("switch", "显示棋子", left=30)]
        self.assertEqual(2, len(self.check_card(*rows, sheet=True)))
        rows[2]["left"] = 20
        rows[4]["left"] = 20
        self.assertEqual([], self.check_card(*rows, sheet=True))

    def test_nested_reading_cards_and_numbered_instructions_keep_their_own_columns(self):
        title = label("heading", "操作")
        first = label("label", "第一句。", left=40, card="child", reading=True, direct=False)
        second = label("label", "第二句。", left=40, card="child", reading=True, direct=False)
        self.assertEqual([], self.check_card(title, first, second))
        second["left"] += 8
        self.assertTrue(self.check_card(title, first, second))
        numbered = [label("label", str(i), width=20, top=i*60, alignment="MiddleCenter") for i in (1, 2)]
        instructions = [label("label", "轻点屏幕。", width=260, left=52, top=i*60) for i in (1, 2)]
        self.assertEqual([], self.check_card(title, *numbered, *instructions))
        instructions[1]["left"] += 5
        self.assertTrue(self.check_card(title, *numbered, *instructions))

    def test_numeric_choice_and_footer_keep_their_own_alignment(self):
        rows = [label("heading", "设置"), label("section", "难度"),
                label("control", "困难", width=60, alignment="MiddleCenter", compact=True),
                label("label", "3", width=42, alignment="MiddleCenter"),
                label("control", "完成", alignment="MiddleCenter")]
        self.assertEqual([], self.check_card(*rows, sheet=True))
        self.assertEqual([], self.check_card(label("heading", "退出应用？", alignment="MiddleCenter"),
                                           label("control", "取消", alignment="MiddleCenter")))
        self.assertEqual([], self.check_card(label("heading", "成绩", alignment="MiddleCenter"),
                                           label("label", "100%\n480 / 480 可行走格\n\n得分 4760    果实 476\n用时 02:00\n\n标准 · 四边相通", alignment="MiddleCenter")))
        self.assertTrue(self.check_card())

    def test_different_headings_cannot_claim_one_rendered_image(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            image = Image.new("RGB", (3, 3))
            image.putpixel((1, 1), (18, 30, 48))
            original, duplicate = root / "original.png", root / "duplicate.png"
            image.save(original, compress_level=0)
            image.save(duplicate, compress_level=9)
            owners = {}
            entry = dict(width=3, height=3, window="玩法")
            self.assertEqual([], report["image_issues"](original, entry, root, owners))
            self.assertEqual([], report["image_issues"](duplicate, entry, root, owners))
            self.assertTrue(report["image_issues"](duplicate, dict(entry, window="暂停"), root, owners))
            image.putpixel((2, 1), (0, 34, 0))
            image.save(duplicate)
            self.assertEqual([], report["image_issues"](duplicate, dict(entry, window="暂停"), root, owners))


if __name__ == "__main__":
    unittest.main()
