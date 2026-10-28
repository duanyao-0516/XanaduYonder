# XanaduYonder

使用 **Unity + C#** 开发的离线小游戏合集，Android 优先。

目标是把自己开发的小游戏放进一个独立 APP，共用大厅、设置、声音、触感和本地存档，同时保持各款游戏的规则与资源独立，方便学习、迭代和接入新玩法。

## 当前状态

最新正式版本为 **v0.4.0**。当前开发源码接入七款可离线游玩的游戏（黄金矿工尚未正式发布），按大厅顺序排列：

- **01 · 2048**：4×4 滑动棋盘、合并动画、胜利后继续与失败重开，记录最高分和最高数字。
- **02 · 跳一跳**：斜俯视正交 3D 场景、按住蓄力松手起跳、随机平台、中心连击、落地和坠落动画。
- **03 · 旸了个旸**：多层叠牌、7 格卡槽三消、四档第二关挑战与无尽模式、可选热身、12 种分堆布局；默认移出 / 撤回 / 洗牌为每局 2 / 3 / 1 次，新局配额可设为 0..99（0 为禁用）。各模式独立续局，记录累计通关和标准无尽最高消除数。
- **04 · 合成大西瓜**：Unity 原生 Physics2D 下落碰撞与同级合并，水果 / 表情 / 校徽三主题，拖动瞄准后松手投球、下一颗预览、合成图鉴、最高分和自动续局。
- **05 · 成语接龙**：同字/同音双模式、人机与本地双人对战、左右对话、免费提示与三次提示小结、可点击释义、生词本与个人学习记录。
- **06 · 数独**：标准 9×9、四档各 500 关、铅笔候选、快速输入、完整撤回、三错失败、每局三次提示、按难度独立续局与书本玩法入口。
- **07 · 黄金矿工**：单人竖屏与双人横屏合作，各 10 关；移动小鼹鼠、携钻动物、五种商店补给、两模式独立续局与检查点重试。

大厅采用纵向滚动列表，每张游戏卡保持固定高度，卡间距略微收紧。各款游戏独立保存对局；2048、跳一跳与合成大西瓜记录最高分，旸了个旸记录累计通关和标准无尽最高分。声音与触感设置共用，旸了个旸使用 `sheep-v2.json`，成语接龙使用 `idiom-learning-v1.json` 原子保存四组对局、生词本与个人学习记录；数独使用独立 `sudoku-v2.json`，自动承接原单局存档，每档最多保留一局，不修改其他游戏的存档协议。旸了个旸当前 schema 2 不迁移旧版 `sheep-v1.json`；无尽每波 144 张、固定正常难度，按实际消除张数计分，换波不恢复整局道具与复活次数。

**最新正式版本为 v0.4.0**，可[下载 Android 安装包与校验文件](https://github.com/duanyao-0516/XanaduYonder/releases/tag/v0.4.0)。本版新增数独和合成大西瓜，并更新成语接龙、旸了个旸及大厅和游戏界面。沿用已有发布签名，支持覆盖升级；2048、跳一跳和公共设置保持兼容，旸了个旸与成语接龙改用独立新存档，旧版对应记录不迁移、原文件保留。详情与验证范围见[v0.4.0 发布记录](docs/releases/v0.4.0.md)。正式版与开发测试 APK 分开构建。项目使用原创代码，素材来源及权利说明见[第三方许可说明](THIRD_PARTY_NOTICES.md)。

## 开发与运行

固定 **Unity 6000.3.27f1（Apple Silicon）**，安装 Android Build Support 及配套 SDK/NDK/OpenJDK。C# 使用 VS Code + Unity 扩展，独立测试使用 .NET 10.0.403。完整环境、IDE 和构建步骤见 [开发说明](docs/development.md)。

在 Unity Hub 中添加仓库根目录，打开 `Assets/Scenes/Bootstrap.unity` 并点击 Play。2048 在棋盘上滑动，编辑器还支持方向键；跳一跳在游戏区域按住蓄力、松手跳跃，编辑器支持鼠标和空格键；旸了个旸点击未被遮挡的牌，编辑器使用鼠标点击；成语接龙输入后点击发送，点击气泡查看释义；数独点击格子和数字，支持铅笔候选与快速输入；合成大西瓜拖动瞄准后松手投球。

```bash
scripts/check-environment.sh
scripts/test-core.sh
scripts/test-input.sh
scripts/test-persistence.sh
scripts/test-jump.sh
scripts/test-jump-persistence.sh
scripts/test-sheep.sh
scripts/test-sheep-persistence.sh
scripts/test-sheep-layouts.sh
scripts/test-sheep-session.sh
scripts/test-sheep-presentation.sh
scripts/test-sheep-difficulty.sh
scripts/test-idiom.sh
scripts/test-idiom-persistence.sh
scripts/test-idiom-resources.sh
scripts/test-idiom-ui.sh
scripts/test-minigame-ui.sh
scripts/test-sudoku.sh
scripts/test-sudoku-content.sh --bank
scripts/test-sudoku-persistence.sh
scripts/test-sudoku-session.sh
scripts/test-sudoku-profile.sh
scripts/test-sudoku-selection.sh
scripts/test-watermelon.sh
scripts/test-watermelon-persistence.sh
scripts/test-gold-miner.sh
scripts/test-gold-miner-input.sh
scripts/test-gold-miner-persistence.sh
scripts/test-gold-miner-levels.sh
scripts/test-gold-miner-catalog.sh
scripts/test-gold-miner-campaign.sh
scripts/test-gold-miner-assets.sh
scripts/test-gold-miner-ui.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

APK 输出到 `artifacts/builds/XanaduYonder-0.4.0.apk`，构建日志在 `artifacts/logs/build-android.log`。这是用于本地验收的开发包，使用本机调试签名，不是商店发布包。安装脚本只选择物理设备；`DEVICE_SERIAL` 从 `adb devices -l` 获取。

正式发布使用 `scripts/build-release.sh`，配置发布签名后生成 `artifacts/builds/XanaduYonder-0.4.0-android-arm64.apk`；签名配置和升级注意事项见[正式发布 APK](docs/development.md#正式发布-apk)。Android 8.0 及以上的 ARM64 设备可安装；本地生成正式 APK 不会自动创建 GitHub Release。

[v0.4.0 发布记录](docs/releases/v0.4.0.md) · [v0.3.0 发布记录](docs/releases/v0.3.0.md) · [成语接龙验证](docs/verification-idiom.md) · [数独玩法与接入](docs/sudoku-game.md) · [数独初版验证](docs/verification-sudoku.md) · [数独修订验证](docs/verification-sudoku-refinement.md) · [数独题库来源](docs/sudoku-content.md) · [旸了个旸玩法与接入](docs/sheep-game.md) · [四档难度校准](docs/verification-sheep-difficulty.md) · [模式、难度与紧凑界面验证](docs/verification-sheep-modes.md) · [v0.3.0 当前包与 Logo 验证](docs/verification-logo-padding.md) · [v0.3.0 玩法修订验证](docs/verification-sheep-refinement.md) · [v0.3.0 初版历史验证](docs/verification-sheep.md) · [跳一跳玩法与接入](docs/jump-game.md) · [v0.2.0 发布记录](docs/releases/v0.2.0.md) · [v0.2.0 验证记录](docs/verification-jump.md) · [架构与存档契约](docs/architecture.md) · [设备场景回归](docs/device-testing.md) · [首版验证记录](docs/verification.md) · [v0.1.0 发布记录](docs/releases/v0.1.0.md) · [第三方许可](THIRD_PARTY_NOTICES.md)

## 成语接龙与离线词库

成语接龙已接入，内置 42,851 个可玩成语与 1,534 个公共常用词；支持人机与双人本地对战，字／音规则分别续玩。双人双方各 3 次提示，任一方第三次展示提示后回顾本轮。页面说明见[成语接龙玩法与 UI](docs/idiom-game.md)，选词、20% 生词复习与个人熟悉词规则见[算法设计](docs/idiom-selection.md)，常用表策划见[数据审计](docs/idiom-common-vocabulary.md)，本轮结果见[学习机制验证](docs/verification-idiom-personalization.md)。使用 `scripts/test-idiom-data.sh` 验证清洗规则，运行 `python3 -B scripts/import-idioms.py` 离线整理两个固定版本的词库。缺少原始文件时可加 `--fetch` 下载；产物、冲突隔离和复现方式见[成语数据清洗说明](docs/idiom-data.md)。

调整成语页头、计数、开关、图标或弹窗时，运行 `scripts/test-idiom-ui.sh` 检查实际居中、控件边界和点击分发，并在长短屏截图中复核字形、留白、键盘展开与收起。该检查需要 Unity 图形渲染；不要与本工作树的 Unity 构建同时运行。编辑器检查不能代替 Android 输入和生命周期验收。

运行资源位于 `Assets/Resources/Idioms`，普通构建不需要下载或运行清洗。重新导出用 `python3 scripts/pack-idioms.py`；只在进入游戏时加载索引，释义按分片读取。

多个工作树同时开发时，使用 `scripts/build-idiom-validation.sh` 在本树生成 `artifacts/builds/XanaduYonder-0.4.0-idiom-validation.apk`。该包标识为 `com.duanyao.xanaduyonder.idiomtest`，不会覆盖正式应用和其他游戏分支的标准安装；不要用普通安装脚本代替这项隔离配置。验证情况见[成语接龙验收记录](docs/verification-idiom.md)。

## 旸了个旸算法与设计

旸了个旸的生成流水线、可解性原理、四档难度、模拟玩家、无尽模式及模块设计，统一见[算法与设计总览](docs/sheep-algorithm-design.md)；玩法和字段契约见[玩法与接入](docs/sheep-game.md)，历史模拟数据见[难度验证记录](docs/verification-sheep-difficulty.md)。

独立验证使用 `scripts/build-sheep-validation.sh`，输出 `artifacts/builds/XanaduYonder-0.4.0-sheep-validation.apk`，包标识为 `com.duanyao.xanaduyonder.sheeptest`。难度改进只作用于新开局，已有对局保留原牌面。

## 04 · 合成大西瓜

在容器上方左右拖动，松手投下当前物体；相同等级碰撞后合成更高一级。三种主题各有 11 级，切换主题保留当前对局与物理尺寸。新物体从前 5 级中随机生成，合并按原等级加分。达成第 11 级后可选择继续挑战；最高等级物体不再合并。警戒线使用持续 2 秒的越线宽限，并排除刚生成、仍在快速运动的物体，减少短暂弹跳导致的失败。

游戏使用独立 Unity `PhysicsScene2D`，规则与存档由可独立测试的 C# 模块负责。`watermelon-v1.json` 保存棋盘中的物体位置与运动状态、当前及下一颗、随机序列、得分、最高分和主题，另有损坏恢复备份；不修改已有游戏的存档。大厅仍使用固定卡片高度与滚动列表。

其中 32 张图片与 4 个音效经用户授权，从 [idootop/watermelon](https://github.com/idootop/watermelon) 的固定版本导入；第 11 级中山大学校徽按用户要求从学校官网原样导入，共 33 张图片与 4 个音效。游戏逻辑为原创 C#，没有复制参考项目的 AGPL-3.0 Dart 源码。文件来源、主题顺序及 SHA-256 见[素材清单](docs/watermelon-assets.md)。

独立验证包使用 `scripts/build-watermelon-validation.sh`，输出 `artifacts/builds/XanaduYonder-0.4.0-watermelon-validation.apk`，包标识为 `com.duanyao.xanaduyonder.watermelontest`，与正式应用及其他工作树的测试包隔离。此前独立分支的核心、存档和 Unity Play Mode 检查均通过，并已在 Android 16 真机与独立模拟器运行验证；具体范围与限制见[验证记录](docs/verification-watermelon.md)，命令见[开发说明](docs/development.md#合成大西瓜独立验证)。已随 v0.4.0 正式发布。

## 当前范围

- 先把独立 Android APP 做好，安装后所有玩法可离线使用。
- 使用 Unity + C#，优先考虑学习资料、工具生态和后续扩展，不自行维护引擎。
- 暂不接入微信、抖音等小游戏平台，不为这些渠道预装 SDK 或增加构建目标。
- 不引入登录、会员、广告、服务器或云存档。
- 游戏通过独立模块接入合集，不预装 Python 或其他无实际需求的运行时。
- 数独新版兼容本项目旧版单局存档；不导入第三方游戏存档。

## 与 DawnYew 的关系

XanaduYonder 是独立项目，与 [DawnYew](https://github.com/duanyao-0516/DawnYew) 没有运行时、账本或存档依赖。猜拳继续作为聚会工具保留在 DawnYew，其他个人小游戏在本项目开发。

## 07 · 黄金矿工

黄金矿工已接入当前开发源码，采用一张固定高度大厅卡片。进入后选择单人竖屏或双人横屏合作，左右半屏分别在按下时出钩；到时会等已出发的钩子完成本次动作。通关后可购买炸药、力量药水、幸运四叶草、石头收藏书和钻石油。各模式有独立的 10 关挑战、进度和历史纪录。

240 张矿图均附生产 Core 的成功输入见证，运行时只选取已验证矿图。`gold-miner-v1.json` 独立保存完整钩子、动物、随机状态、商店和本关开局检查点；失败重试恢复同一矿场与补给。编辑器支持单人 Space，P1 S/W、P2 ↓/↑ 分别出钩/炸药，Escape 暂停。

运行 `scripts/build-gold-miner-validation.sh` 生成 `artifacts/builds/XanaduYonder-0.4.0-gold-miner-validation.apk`；包名为 `com.duanyao.xanaduyonder.goldminertest`，与正式应用及其他工作树测试包隔离。该包为开发验收用途，不创建新版本或 Release；实际执行记录见[黄金矿工验证](docs/verification-gold-miner.md)。

- [玩法与首版数值](docs/superpowers/specs/2026-10-27-gold-miner-design.md)：10 关挑战、矿物/动物、商店、关卡变体与失败重试。
- [技术契约与验收矩阵](docs/gold-miner-technical.md)：横竖屏、双指、生命周期、两模式存档与事务。
- [资源与音效接入](docs/gold-miner-assets.md)：44 张生产 PNG、切片/导入参数、动画、音效和资源预算。
- [分步实施计划](docs/superpowers/plans/2026-10-27-gold-miner.md)：文件归属、任务依赖、测试入口、隔离验证包及交付条件。

## 开发约定

- 默认分支为 `master`。
- 编辑器版本固定在 `ProjectSettings/ProjectVersion.txt`，依赖固定在 `Packages/`；升级时同步更新构建说明并重新验证 Android 真机。
- 游戏规则与表现分开；规则使用可测试的 C#，场景、动画和输入由 Unity 承担。
- 每款游戏使用稳定标识、独立资源和版本化本地存档。大厅负责进入、暂停、返回及资源释放。
- 以真实 Android 设备检查输入、动效、性能和生命周期；编辑器运行正常不能代替设备验证。

## Git 与文件管理

Unity 工程创建后，应提交 `Assets/` 及其 `.meta`、`Packages/manifest.json`、`Packages/packages-lock.json` 和 `ProjectSettings/`。缓存、构建产物、设备数据、签名和凭据不进入版本控制，具体规则见 [.gitignore](.gitignore)。

Git 作者和 GitHub 连接参数在本地 `.git/config` 配置。本次初始化沿用 DawnYew 的项目级配置与现有 SSH 凭据，没有修改全局 Git/SSH 配置。`.git/config` 不随仓库发布；其他贡献者使用自己的身份和凭据。

## 许可证

本仓库原创代码采用 [MIT License](LICENSE)。Unity 编辑器、运行时及未来引入的第三方插件或资源仍遵循各自许可证；本仓库的 MIT 许可不覆盖这些第三方内容。

添加第三方依赖或素材时保留其许可证与署名，只提交允许再分发的内容。
