# 中山大学校徽主题调整 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 删除中国海洋大学，湖南大学至清华大学各前移一级，中山大学占第 11 级最大半径。

**Architecture:** 从本地 `master` 的 `ca50db0` 建立独立工作树及 `p/duanyao.0516/feature/watermelon-sysu` 分支。校徽主题继续按 `Campus/{level + 1}` 加载，仅更新图片与 `CampusNames`；保留 11 档物理半径、槽位 `.meta`、水果/表情主题及存档协议。旧局保存的等级保持原值，恢复后使用新外观。

**Tech Stack:** Unity 6000.3.27f1、C#、PNG、.NET 独立规则/存档测试。

---

### Task 1: 来源与图片

- [x] 核对中山大学官网 `https://www.sysu.edu.cn/xxg/zdjj1.htm` 的校徽栏目，下载其原始 PNG，检查校名、圆形边缘与正方形比例。
- [x] 将原 `Campus/2.png` 至 `11.png` 的字节分别移到 `Campus/1.png` 至 `10.png`，原样导入官网 PNG 为 `Campus/11.png`；编号槽位的 `.meta` 保持原值。

### Task 2: 标签与来源记录

- [x] 修改 `Assets/Scripts/Runtime/Watermelon/WatermelonArtwork.cs`：

```csharp
public static readonly string[] CampusNames = { "湖南", "山东", "华东师范", "电子科大", "四川", "复旦", "上海交大", "浙江", "北京", "清华", "中山" };
```

- [x] 更新 `docs/watermelon-assets.md` 的顺序、来源映射、文件尺寸/哈希/总量及旧局行为；更新 `THIRD_PARTY_NOTICES.md`、`docs/development.md` 与 `README.md`，将官网新增素材与旧素材来源区分。

### Task 3: 核验

- [x] 基线执行 `scripts/test-watermelon.sh` 和 `scripts/test-watermelon-persistence.sh`，分别为 36、24 项通过。
- [x] 比对 master 的图片字节，确认新 1–10 级分别等于旧 2–11 级，中山校徽与官网下载字节相同；核对文档所有 37 项资源的 SHA-256、尺寸和字节数。
- [x] 再次执行上述规则和存档测试，并执行 `git diff --check`；独立审查修改范围、等级与图片对应关系。没有运行的 Unity/设备验证须明确说明。

验证结果：规则 36/36、存档 24/24 通过，37 项资源清单与当前文件一致，10 张前移图及官网原图字节一致，`git diff --check` 通过。独立审查发现 README 的旧来源概述，已同步修正。未运行 Unity 编译、Play Mode 或真机视觉验证。用户随后明确要求不再编译，将本次调整直接提交到远端 `master`；交付前仅检查提交范围和 Git 状态。
