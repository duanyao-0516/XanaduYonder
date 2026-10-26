# 成语词库脚本清洗实施计划

> **For agentic workers:** Use subagent-driven development for the independent normalization and font checks; integrate and verify the CLI in this worktree. Only the data preparation part of the game design is authorized in this task.

**Goal:** 在本机用确定性脚本处理两个固定版本的完整成语库，导出统一资料、游戏候选子集和可追溯审计报告。

**Architecture:** Python 3.9+ 标准库完成离线导入、字段归一化、冲突隔离与导出。纯规则和字体覆盖检查分开测试；CLI 校验来源 SHA256，失败不覆盖已有结果。未知内容不由模型改写。

**Tech Stack:** Python 标准库、unittest、固定 JSON 源、仓库现有 Noto Sans CJK 字体。

## 文件与接口

- `Data/Idioms/source-manifest.json`：两个仓库固定 commit、六份数据文件 SHA256 与许可信息，以及当前字体摘要。
- `Data/Idioms/exclusions.json`：已经发现但未能确定正确值的问题词，只有隔离原因，没有猜测修正。
- `scripts/idiom_pipeline/normalize.py`、`merge.py`：规范化、来源保留、同词合并、核心冲突隔离；`merge_records(records, exclusions, font_codepoints)` 返回 `entries/invalidRecords/stats`。
- `scripts/idiom_pipeline/font.py`：`read_font_codepoints(path)` 严格读取字体 cmap 中的实际非零映射。
- `scripts/import-idioms.py`：固定来源校验、可选下载、确定性导出、审计、输出保护。
- `Tests/IdiomData/`：规则、字体、真实 CLI 文件读写测试。
- `docs/idiom-data.md`：命令、产物、统计、限制与后续修正流程。
- 输出只写 `artifacts/idiom-cleaned/`，本轮不写 Unity 资源、不安装或构建游戏。

## 执行步骤

- [x] 1. 写规则失败测试：占位“无”、声调与 ü、多音义冲突、非四字保留、来源完整、无模糊纠错、稳定排序。执行 `python3 -B -m unittest discover -s Tests/IdiomData -p 'test_*.py' -v`，确认缺少实现导致失败，再实现纯函数。
- [x] 2. 写字体失败测试：format 4/12、空 glyph、损坏偏移、截断；实现只读 cmap 检查，并核对仓库实际字体。
- [x] 3. 写 CLI 失败测试：SHA 错误不产生输出、完整微型数据导入、同输入重复导出一致、不覆盖不明目录、审计数量可核对。实现 CLI 和固定来源配置。
- [x] 4. 运行同一入口处理全部 80,534 条源记录；检查每条来源均有去向，统一词条总数与调研结果对照，候选集只含满足规则的记录。
- [x] 5. 在第二个输出目录重复生成，比较所有确定性文件的 SHA256；独立检查候选首尾索引、引用和字形覆盖。
- [x] 6. 做独立代码审查，修复有依据的问题；再次执行受影响测试及完整清洗，更新报告和使用文档。

## 完成标准

脚本运行成功、全量来源可追踪、输出可重现、重要错误有测试、未知冲突有隔离清单。合格子集表示通过已定义自动规则，不表示全部成语已经人工考据。原始数据保留不变；不生成故事、读音或释义来填空。
