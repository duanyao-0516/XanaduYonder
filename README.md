# XanaduYonder

使用 **Unity + C#** 开发的离线小游戏合集，Android 优先。

目标是把自己开发的小游戏放进一个独立 APP，共用大厅、设置、声音、触感和本地存档，同时保持各款游戏的规则与资源独立，方便学习、迭代和接入新玩法。

## 当前状态

已创建 Unity 工程并实现 **2048 v0.1.0**：中文大厅、4×4 滑动棋盘、合并动画、分数与最高纪录、胜利后继续、失败重开、声音/触感设置，以及带备份恢复的本地存档。

Android 开发测试 APK 可通过下方命令构建。后续逐步增加蓄力跳跃、堆叠拾取消除等自研玩法；使用自己的代码与素材，不直接复制其他游戏的实现或资源。

## 开发与运行

固定 **Unity 6000.3.27f1（Apple Silicon）**，安装 Android Build Support 及配套 SDK/NDK/OpenJDK。C# 使用 VS Code + Unity 扩展，独立测试使用 .NET 10.0.403。完整环境、IDE 和构建步骤见 [开发说明](docs/development.md)。

在 Unity Hub 中添加仓库根目录，打开 `Assets/Scenes/Bootstrap.unity` 并点击 Play。手机在棋盘上滑动操作，编辑器还支持鼠标滑动和方向键。

```bash
scripts/check-environment.sh
scripts/test-core.sh
scripts/test-input.sh
scripts/test-persistence.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

APK 输出到 `artifacts/builds/XanaduYonder-0.1.0.apk`，构建日志在 `artifacts/logs/build-android.log`。这是用于本地验收的开发包，使用本机调试签名，不是商店发布包。安装脚本只选择物理设备；`DEVICE_SERIAL` 从 `adb devices -l` 获取。

[架构与存档契约](docs/architecture.md) · [设备场景回归](docs/device-testing.md) · [首版验证记录](docs/verification.md) · [第三方许可](THIRD_PARTY_NOTICES.md)

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
