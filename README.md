# XanaduYonder

使用 **Unity + C#** 开发的离线小游戏合集，Android 优先。

目标是把自己开发的小游戏放进一个独立 APP，共用大厅、设置、声音、触感和本地存档，同时保持各款游戏的规则与资源独立，方便学习、迭代和接入新玩法。

## 当前状态

当前版本为 **v0.3.0**，接入四款可离线游玩的游戏：

- **2048**：4×4 滑动棋盘、合并动画、胜利后继续与失败重开。
- **跳一跳**：斜俯视正交 3D 场景、按住蓄力松手起跳、随机平台、中心连击、落地和坠落动画。
- **成语接龙**：同字/同音双模式、机器/用户双开局、左右对话、免费提示与三次提示小结、可点击释义和本地生词本。
- **旸了个旸**：多层叠牌、7 格卡槽三消、教学关与挑战关、12种分堆布局、不限次移出/撤回/洗牌、一次免费复活和累计通关。

大厅采用纵向滚动列表，每张游戏卡保持固定高度，卡间距略微收紧。四款游戏各自保存对局；2048 与跳一跳记录最高分，旸了个旸记录累计通关。声音与触感设置共用，旸了个旸使用 `sheep-v1.json`，成语接龙使用 `idiom-v1.json` 与独立的 `idiom-notebook-v1.json`，不修改已有游戏的存档协议。

**最新正式版本为 v0.3.0**，新增旸了个旸和成语接龙，可[下载 Android 安装包与校验文件](https://github.com/duanyao-0516/XanaduYonder/releases/tag/v0.3.0)。正式版可覆盖升级 v0.1.0/v0.2.0 并保留兼容的旧存档；详情见[发布记录](docs/releases/v0.3.0.md)。正式版使用独立发布签名，与开发测试 APK 分开构建。项目使用原创代码和自制或明确授权的素材，来源见[第三方许可说明](THIRD_PARTY_NOTICES.md)。

## 开发与运行

固定 **Unity 6000.3.27f1（Apple Silicon）**，安装 Android Build Support 及配套 SDK/NDK/OpenJDK。C# 使用 VS Code + Unity 扩展，独立测试使用 .NET 10.0.403。完整环境、IDE 和构建步骤见 [开发说明](docs/development.md)。

在 Unity Hub 中添加仓库根目录，打开 `Assets/Scenes/Bootstrap.unity` 并点击 Play。2048 在棋盘上滑动，编辑器还支持方向键；跳一跳在游戏区域按住蓄力、松手跳跃，编辑器支持鼠标和空格键；旸了个旸点击未被遮挡的牌，编辑器使用鼠标点击；成语接龙输入后点击发送，点击气泡查看释义。

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
scripts/test-idiom.sh
scripts/test-idiom-persistence.sh
scripts/test-idiom-resources.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

APK 输出到 `artifacts/builds/XanaduYonder-0.3.0.apk`，构建日志在 `artifacts/logs/build-android.log`。这是用于本地验收的开发包，使用本机调试签名，不是商店发布包。安装脚本只选择物理设备；`DEVICE_SERIAL` 从 `adb devices -l` 获取。

正式发布使用 `scripts/build-release.sh`，配置发布签名后生成 `artifacts/builds/XanaduYonder-0.3.0-android-arm64.apk`；签名配置和升级注意事项见[正式发布 APK](docs/development.md#正式发布-apk)。Android 8.0 及以上的 ARM64 设备可安装；本地生成正式 APK 不会自动创建 GitHub Release。

[v0.3.0 发布记录](docs/releases/v0.3.0.md) · [成语接龙验证](docs/verification-idiom.md) · [旸了个旸玩法与接入](docs/sheep-game.md) · [v0.3.0 当前包与 Logo 验证](docs/verification-logo-padding.md) · [v0.3.0 玩法修订验证](docs/verification-sheep-refinement.md) · [v0.3.0 初版历史验证](docs/verification-sheep.md) · [跳一跳玩法与接入](docs/jump-game.md) · [v0.2.0 发布记录](docs/releases/v0.2.0.md) · [v0.2.0 验证记录](docs/verification-jump.md) · [架构与存档契约](docs/architecture.md) · [设备场景回归](docs/device-testing.md) · [首版验证记录](docs/verification.md) · [v0.1.0 发布记录](docs/releases/v0.1.0.md) · [第三方许可](THIRD_PARTY_NOTICES.md)

## 成语接龙与离线词库

成语接龙已接入，内置 42,851 个可玩成语；双模式、开局选择、三次提示小结和生词本的页面说明见[成语接龙玩法与 UI](docs/idiom-game.md)。使用 `scripts/test-idiom-data.sh` 验证清洗规则，运行 `python3 -B scripts/import-idioms.py` 离线整理两个固定版本的词库。缺少原始文件时可加 `--fetch` 下载；产物、冲突隔离和复现方式见[成语数据清洗说明](docs/idiom-data.md)。

运行资源位于 `Assets/Resources/Idioms`，普通构建不需要下载或运行清洗。重新导出用 `python3 scripts/pack-idioms.py`；只在进入游戏时加载索引，释义按分片读取。

多个工作树同时开发时，使用 `scripts/build-idiom-validation.sh` 在本树生成 `artifacts/builds/XanaduYonder-0.3.0-idiom-validation.apk`。该包标识为 `com.duanyao.xanaduyonder.idiomtest`，不会覆盖正式应用和其他游戏分支的标准安装；不要用普通安装脚本代替这项隔离配置。验证情况见[成语接龙验收记录](docs/verification-idiom.md)。

## 当前范围

- 先把独立 Android APP 做好，安装后所有玩法可离线使用。
- 使用 Unity + C#，优先考虑学习资料、工具生态和后续扩展，不自行维护引擎。
- 暂不接入微信、抖音等小游戏平台，不为这些渠道预装 SDK 或增加构建目标。
- 不引入登录、会员、广告、服务器或云存档。
- 游戏通过独立模块接入合集，不预装 Python 或其他无实际需求的运行时。
- 不提供旧版本游戏存档迁移。

## 与 DawnYew 的关系

XanaduYonder 是独立项目，与 [DawnYew](https://github.com/duanyao-0516/DawnYew) 没有运行时、账本或存档依赖。猜拳继续作为聚会工具保留在 DawnYew，其他个人小游戏在本项目开发。

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
