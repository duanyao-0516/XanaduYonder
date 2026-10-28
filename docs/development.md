# 本地开发与 Android 构建

## 固定环境

- Unity Editor **6000.3.27f1**，changeset `672d562a42a7`，Mac 使用 Apple Silicon 版本。
- Unity Hub 安装同一编辑器的 **Android Build Support**、**Android SDK & NDK Tools**、**OpenJDK**；保持 Unity External Tools 默认使用随该版本安装的依赖。
- VS Code 安装 Microsoft 的 `visualstudiotoolsforunity.vstuc` 扩展，它同时安装 C# 与 C# Dev Kit。Unity 项目使用 `com.unity.ide.visualstudio`，不使用已废弃的 Visual Studio Code Editor 包。
- .NET SDK **10.0.403** 用于独立测试，`global.json` 允许同 feature band 的后续补丁。它不替代 Unity 的脚本编译器与 Android 构建工具。
- Android 8.0/API 26 或以上的 **ARM64 真机**，开启开发者选项与 USB 调试，并授权当前电脑。首版固定 target SDK 36，避免跟随机器中额外安装的 SDK 自动变化。

Unity Hub 的账号登录、编辑器许可和首次条款需由使用者完成。许可文件、Android 调试签名和个人 IDE 配置不进入 Git。

本机通过官方 Unity CLI 安装的 Android 模块位于 `/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer`，与 `Unity.app` 同级；这是已通过真实构建的安装布局。环境检查同时兼容旧的 `Unity.app/Contents/PlaybackEngines/AndroidPlayer` 布局，不需要手动移动模块或创建软链接。

## 打开项目

在 Unity Hub 中添加当前仓库根目录；使用上面的固定版本打开 `Assets/Scenes/Bootstrap.unity`，点击 Play。首次导入会生成 `Library/` 和包缓存。在 Unity 的 External Tools 中将 External Script Editor 设为 VS Code；双击 C# 文件即可跳转。

也可以使用菜单 `XanaduYonder / Configure VS Code` 设置外部编辑器并生成解决方案。默认路径为 `/Applications/Visual Studio Code.app`，其他位置用 `UNITY_SCRIPT_EDITOR` 指定。

2048 在手机上滑动，编辑器支持鼠标滑动和方向键；跳一跳按住蓄力、松手起跳，编辑器也支持空格键；旸了个旸点击未被盖住的牌，编辑器用鼠标点击；成语接龙输入四字词后点发送，点击对话查看释义；数独点击格子和数字，支持铅笔候选与快速输入；04 合成大西瓜在棋盘上拖动瞄准、松手投球，编辑器使用鼠标拖动与释放。大厅纵向滚动，游戏卡片保持固定高度。规则、公共设置和各自存档逻辑均与手机共用。

## 命令

在仓库根目录执行：

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
scripts/test-sudoku.sh
scripts/test-sudoku-content.sh --bank
scripts/test-sudoku-persistence.sh
scripts/test-sudoku-session.sh
scripts/test-sudoku-profile.sh
scripts/test-sudoku-selection.sh
scripts/test-minigame-ui.sh
scripts/test-watermelon.sh
scripts/test-watermelon-persistence.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

`scripts/test-minigame-ui.sh` 在 Unity 中渲染两种高度的页面，检查数独填写/剩余计数、工具点击、玩法弹窗的输入隔离与计时恢复，并输出 `artifacts/validation/minigame-ui/` 截图和报告；这些编辑器检查不代替 Android 真机回归。

`scripts/test-sheep.sh` 直接编译生产规则代码，检查叠层遮挡、7 槽三消、工具、快照与多种子解法重放；`scripts/test-sheep-persistence.sh` 使用真实 Newtonsoft 解析器和临时目录检查新游戏存档。独立测试不能代替 Unity 编译与设备输入/表现验证。

`DEVICE_SERIAL` 从 `adb devices -l` 取得。安装脚本显式拒绝模拟器；不传序列号时仅在恰好存在一台已授权物理设备且没有其他未就绪设备时自动选择。可以传第二个参数指定 APK。

构建脚本默认使用 `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity`。非默认安装位置用 `UNITY_EDITOR=/absolute/path/to/Unity` 覆盖；ADB 可用 `ADB` 覆盖，测试脚本可用 `DOTNET_ROOT` 指定 SDK 目录。

构建输出为 `artifacts/builds/XanaduYonder-0.4.0.apk`，日志为 `artifacts/logs/build-android.log`。当前源码版本为 0.4.0、Android versionCode 为 4；最新正式发行版为 [v0.4.0](https://github.com/duanyao-0516/XanaduYonder/releases/tag/v0.4.0)。构建本地 APK 不会自动发布新版。开发包使用 Development Build 便于本地日志与存档检查，关闭脚本调试和 profiler 自动连接；使用本机 Android 调试签名，不是商店发布包。

如需单独创建/检查启动场景与构建设置：

```bash
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
mkdir -p artifacts/logs
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.PrepareProject \
  -logFile "$PWD/artifacts/logs/prepare.log"
```

使用同样命令将 `-executeMethod` 替换成 `XanaduYonder.EditorTools.PersistenceChecks.Run`，可以在 Unity 运行真实存档验证。批处理前关闭此项目的交互编辑器，避免同一工程被两个进程占用。

## 黄金矿工独立验证

在黄金矿工的隔离工作树运行以下入口；Unity UI 检查和构建必须串行，避免同一工程被两个编辑器占用。

```bash
scripts/test-gold-miner.sh
scripts/test-gold-miner-input.sh
scripts/test-gold-miner-persistence.sh
scripts/test-gold-miner-levels.sh
scripts/test-gold-miner-catalog.sh
scripts/test-gold-miner-campaign.sh
scripts/test-gold-miner-assets.sh
scripts/test-gold-miner-ui.sh
scripts/build-gold-miner-validation.sh
```

前六项直接编译同一份生产 C#；目录检查遍历 240 张地图和 2000 组种子，Levels 检查用生产模拟重放全部成功见证。UI 脚本启动真正的 Unity Play Mode，使用隔离临时目录与实际 UGUI 命中，报告/截图在 `artifacts/gold-miner-development/ui/`。它不会读写正式玩家存档。资源导入入口为 `XanaduYonder.EditorTools.GoldMinerAssetImporter.Prepare`。

APK 为 `artifacts/builds/XanaduYonder-0.4.0-gold-miner-validation.apk`，日志在 `artifacts/logs/build-gold-miner-validation.log`。显式选择 `adb devices -l` 的物理设备，安装及启动 `com.duanyao.xanaduyonder.goldminertest/com.unity3d.player.UnityPlayerActivity`；不要使用普通安装脚本覆盖正式包。测试包与正式版、其他工作树的测试包各有独立数据目录。

`scripts/prepare-gold-miner-touch.sh` 只在本机编译 Android 原生多指验收驱动，输出 `artifacts/gold-miner-development/device-tools/gold-miner-touch.jar`；不自动连接或操作设备。手工选择设备后可用 `adb -s SERIAL shell CLASSPATH=/data/local/tmp/gold-miner-touch.jar app_process / NativeTouch dual X1 Y1 X2 Y2 HOLD_MS` 验证真正的 Android MotionEvent 双指链路。实际设备、构建和验证范围见[黄金矿工验证](verification-gold-miner.md)。

## 合成大西瓜独立验证

04 合成大西瓜使用 Unity 原生 `PhysicsScene2D` 独立场景与固定步长模拟，避免改变其他游戏的全局物理设置。水果、表情、校徽只改变贴图；同等级使用相同圆形碰撞体。水果、表情、前 10 级校徽和音效来自用户授权的固定版本仓库，第 11 级中山大学校徽来自学校官网；完整路径、哈希及来源说明见[素材清单](watermelon-assets.md)。

```bash
scripts/test-watermelon.sh
scripts/test-watermelon-persistence.sh
scripts/test-watermelon-physics.sh
scripts/build-watermelon-validation.sh
ADB=/Applications/Unity/Hub/Editor/6000.3.27f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$ADB" -s DEVICE_SERIAL install -r artifacts/builds/XanaduYonder-0.4.0-watermelon-validation.apk
"$ADB" -s DEVICE_SERIAL shell am start -n com.duanyao.xanaduyonder.watermelontest/com.unity3d.player.UnityPlayerActivity
```

`test-watermelon.sh` 编译生产规则模块，检查掉落队列、冷却、同级合并与重复碰撞、计分、最高级处理、警戒宽限和快照约束。`test-watermelon-persistence.sh` 使用真实 Newtonsoft.Json 解析器检查主题与完整运动状态往返、缺失/错误字段、损坏恢复、写入失败和其他游戏存档隔离。独立测试不运行 Unity 的物理求解器，也不能替代构建与设备回归。

`test-watermelon-physics.sh` 启动 Unity Play Mode，验证真实碰撞合并、堆叠、出生位置、独立场景、恢复运动及越线，结果位于 `artifacts/validation/watermelon-physics.txt`。执行前关闭此工作树的 Unity 编辑器。编译或运行故障会返回非零，超时会终止本次测试进程。

验证脚本输出 `artifacts/builds/XanaduYonder-0.4.0-watermelon-validation.apk`，日志为 `artifacts/logs/build-watermelon-validation.log`；应用包标识为 `com.duanyao.xanaduyonder.watermelontest`。它使用独立应用数据目录，不覆盖正式应用或数独等其他工作树的安装。请使用上面的显式 ADB 安装与启动命令；普通 `install-android.sh` 启动的是正式包名。最新正式发行版本为 v0.4.0，生成此包不会创建新版本或 GitHub Release。

`watermelon-v1.json` 独立保存棋盘状态、当前/下一颗、随机状态、主题、得分、最高分及目标提示状态，保存备份为 `watermelon-v1.json.bak`。暂停、退回大厅和应用切后台时应保留续局；主题切换应保持物体位置、大小与速度。需要在真机覆盖拖动松手、连续合并、触摸取消、暂停/恢复、进程重启续局、声音/触感设置及三个主题显示。

相对参考项目，本实现采用松手投球、持续 2 秒的稳定越线判定，以及首次合成最高级后的继续挑战。验收应覆盖短暂越线后回落、正在下落与弹跳的物体、持续堆高失败和第 11 级达成后继续。这些是当前实现的验收要求，文档不据此宣称已通过设备验证。

## 正式发布 APK

发布构建使用独立、长期保留的私有签名。通过安全的本地凭据管理方式给当前进程设置以下环境变量，不要把口令直接写入命令历史、仓库或日志：

| 环境变量 | 用途 |
| --- | --- |
| `XANADU_KEYSTORE_PATH` | 私有 keystore 的绝对路径 |
| `XANADU_KEY_ALIAS` | 发布密钥别名 |
| `XANADU_KEYSTORE_PASSWORD` | keystore 口令 |
| `XANADU_KEY_PASSWORD` | 密钥口令 |

```bash
scripts/build-release.sh
```

脚本缺少签名配置时立即失败。Unity 子进程不会收到上述变量，只生成关闭 Development Build、脚本调试与 profiler 连接的中间 APK；签名配置不进入 Unity 的 `ProjectSettings`。构建入口在结束或抛出异常时恢复原来的 Development 和自定义签名开关。

脚本使用配套 SDK 的 `apksigner` 替换中间 APK 的调试签名，口令只经环境变量传入；拒绝 debuggable APK，并在签名验证成功后原子替换 `artifacts/builds/XanaduYonder-0.4.0-android-arm64.apk`。中间文件不会作为发布产物保留。脚本返回非零代表本次失败，即使目录中存在较早的正式 APK，也不能将其当作本次构建成功。可以通过 `APKSIGNER`、`AAPT` 覆盖工具路径。

Unity 日志为 `artifacts/logs/build-android-release.log`，本地签名验证日志为 `artifacts/logs/sign-android-release.log`。只上传最终 APK、校验和及公开发布说明；不要上传 keystore、口令、私有签名日志或整个构建目录。后续版本必须使用同一发布密钥才能覆盖升级。正式签名与现有开发签名不同，Android 会拒绝直接覆盖开发包；测试迁移前应先保护设备存档，不能直接卸载并丢失进度。

### GitHub CLI

macOS 通过 Homebrew 安装 GitHub 官方 CLI，后续创建 Release、上传附件及校验可直接使用 `gh`：

```bash
brew install gh
gh auth login --hostname github.com --git-protocol ssh --web --skip-ssh-key
gh auth status
```

首次登录在 GitHub 完成设备授权与必要的二次验证；复用现有 SSH 密钥，不生成或上传新密钥。登录后检查 `gh auth status` 显示系统钥匙串存储，不在仓库、脚本或环境文件中保存令牌。参见 [GitHub CLI 登录文档](https://cli.github.com/manual/gh_auth_login)。

发布前先验证 APK、签名、校验文件及实际目标分支，将代码和对应标签推到远端。通过 `gh release create --draft --verify-tag --notes-file` 创建草稿并上传指定的 APK 与校验文件；回读附件确认文件长度和 SHA-256 后，再用 `gh release edit --draft=false --latest` 发布。版本号、附件路径和说明文件均应使用本次版本的真实值，不覆盖已有版本附件。

## 项目与缓存

应提交 `Assets/` 及其 `.meta`、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/`、测试和脚本。`Library/`、IDE 生成的 `.sln/.csproj`、测试 `bin/obj`、APK、设备截图/日志、签名与许可均不提交。

开发环境建议预留约 60 GB，且只保留需要的编辑器版本与平台模块。Android 工具链与个人已有的 Android Studio SDK 分开管理，避免版本混用。

## 应用图标与启动标识

`Assets/Branding/XY_logo.png` 原样使用项目所有者提供的完整图像，不裁剪或修改原图像素。每次构建的 `AppBranding.Configure()` 配置默认及 Android Legacy/Round/Adaptive 图标。Legacy/Round 位图保留原图中的 Logo 与白边比例；Adaptive 图标在生成的 XML 中给完整前景图像添加每边 `1/6`（`16.666667%`）的 inset，抵消 Android 将 108 dp 图层的中央 72 dp 显示为图标造成的放大，使可见图标内的 Logo 与白边比例保持与原图一致。机制见 [AdaptiveIconDrawable 官方说明](https://developer.android.com/reference/android/graphics/drawable/AdaptiveIconDrawable)与 [InsetDrawable 比例内缩说明](https://developer.android.com/reference/android/graphics/drawable/InsetDrawable)。

Unity 原生 Splash 和 Unity 标识均禁用，原生 Logo 列表清空。Bootstrap 上的 `BrandSplash` 使用独立 Canvas 居中显示完整原图，图片宽度为屏幕短边的 `0.5`，白底静止显示 `1.75` 秒后用 `0.25` 秒淡出。Android 12 系统启动窗口保持白底，引用经过上述 inset 修正的 `app_icon`；Gradle 生成后回调同时覆盖实际 Activity 主题的窗口背景。

Unity 6 Personal 允许关闭或定制 Unity 启动标识，见[官方发行说明](https://unity.com/releases/editor/whats-new/6000.0.0f1)与 [6000.3 Player Settings](https://docs.unity3d.com/6000.3/Documentation/Manual/class-PlayerSettingsAndroid.html)。构建后需检查生成的 Adaptive XML、原生 Splash 禁用配置，并通过系统应用图标截图及冷启动录像确认留白比例、Android 原生窗口与 `BrandSplash` 的显示效果。
