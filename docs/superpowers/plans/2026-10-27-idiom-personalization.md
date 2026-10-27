# 成语选词与个人学习 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 将已确认的加权随机选词、生词复习和个人熟悉词接入离线成语接龙，扩充公共常用表并完成模拟器与手机安装验证。

**Architecture:** Core 负责纯规则、推荐和学习状态变更；Storage 原子保存成语对局与个人学习状态；Presentation 负责当前回合的释义查看事件和生词本操作。公共常用词为打包数据，个人数据只在本机；其他小游戏不修改存档契约。

**Tech Stack:** Unity 6000.3.27f1、C#、.NET 10 独立测试、Python 词库审计、Android ARM64。

---

## 边界与决策

- 仅修改当前 `20ab/XanaduYonder` 工作树，沿用现有分支与未提交的双人/UI 工作，不切换其他 checkout。
- 用户已批准设计并要求直接开发；不再次请求设计确认。用户明确不需要旧存档兼容。
- 对用户合法输入使用全量词库；APP 普通候选为 `(公共常用 ∪ 个人熟悉) − 生词本`。
- 两类推荐均须有至少一个未用过的普通后继。两池都有词时 80% 普通、20% 生词复习；全部合格词都有正权重。
- 仅人机独立合法输入积累熟悉词。当前回合展示过的整组提示和查看过释义的词均视为辅助输入。
- APP 复习是再次见到，不自动掌握；同词自动复习冷却 24 小时。普通移出不生成熟悉记录；“我记住了”明确转为熟悉。
- APP 与提示随机流分开；提示按回合缓存。历史及已生成结果不会因学习状态变化而重写。
- 公共表按日常学习可理解性人工策划，并用现有完整词库校验；不把人工标签声称为统计词频。

## Task 1：纯规则、学习模型与存储

**Files:** `Assets/Scripts/Core/Idiom/`、`Assets/Scripts/Runtime/Idiom/Storage/`、`Tests/Idiom/Program.cs`、`Tests/IdiomPersistence/Program.cs`。

- [x] 先增加失败测试：单一高分不独占、普通/复习互斥、24h 边界、最近10词降权、双人不学习、提示/释义辅助、移出/记住差异。
- [x] 实现注入的个人状态和时钟、加权推荐、独立提示随机数、新的推荐穷尽结束原因。
- [x] 保存辅助来源和随机状态；把四组对局、生词本和学习记录放入同一成语事务，重复保存不重复记学习事件。
- [x] 执行 `scripts/test-idiom.sh` 和 `scripts/test-idiom-persistence.sh`；覆盖重启、坏档保护、状态范围及事务失败。

## Task 2：公共常用词扩充与审计

**Files:** `Data/Idioms/common-idioms.txt`、`scripts/pack-idioms.py`、`Tests/IdiomResources/`、`Assets/Resources/Idioms/`、`docs/idiom-common-vocabulary.md`。

- [x] 先检查重复、无效词、可接续性；保存旧表指标作为比较。
- [x] 扩充审慎策划的常用候选，纳入“一望无际”，避免冷僻词无差别进入普通池。
- [x] 重新打包并运行 `scripts/test-idiom-resources.sh`；报告总量、唯一性与字/音模式覆盖。

## Task 3：页面接入与真实 UI 检查

**Files:** `Assets/Scripts/Runtime/Idiom/Presentation/IdiomPage.cs`、`IdiomPage.Dialogs.cs`、`IdiomPage.Layout.cs`、`Assets/Editor/IdiomUiChecks.cs`。

- [x] 为生词弹窗的“我记住了”与“移出生词本”、状态文字和实际按钮回调补充可失败的 UI 检查。
- [x] 接入共享学习状态；成功展示释义记录当前回合辅助，独立输入及 APP 展示在规则动作内完成。
- [x] 在生词弹窗提供“我记住了”；列表说明复习与熟悉规则，结算提供推荐用尽的鼓励文案。
- [x] 运行 `scripts/test-idiom-ui.sh`，使用实际 Canvas 几何和点击回调验证三种长宽比，保留输入法保护。

## Task 4：构建、模拟器、手机与交叉审查

**Files:** `docs/idiom-selection.md`、`docs/verification-idiom-personalization.md`、`docs/idiom-game.md`、`README.md`。

- [x] 独立审查需求覆盖、状态更新、持久化和恢复；修复有证据的问题后复测。
- [x] 执行 `scripts/build-idiom-validation.sh`，只使用本任务 emulator-5586，保留其他 emulator 和工作区状态。
- [x] 验证人机独立输入、提示辅助、复习、记住/移出、双人边界、冷启动、长短屏与键盘；日志与截图放入 ignored artifacts。
- [x] 安装同一独立测试 APK 到手机 `3B6F4NE8A66ZXTRL`，核对安装包与启动状态。
- [x] 沉淀确切公式、数据所有权、参数取舍及测试证据，复核工作树隔离；本请求不执行发布或推送。
