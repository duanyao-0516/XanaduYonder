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

手机使用滑动操作；编辑器同时支持鼠标滑动和键盘方向键。数字规则、设置、存档均与手机共用。

## 命令

在仓库根目录执行：

```bash
scripts/check-environment.sh
scripts/test-core.sh
scripts/test-input.sh
scripts/test-persistence.sh
scripts/test-jump.sh
scripts/test-jump-persistence.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

`DEVICE_SERIAL` 从 `adb devices -l` 取得。安装脚本显式拒绝模拟器；不传序列号时仅在恰好存在一台已授权物理设备且没有其他未就绪设备时自动选择。可以传第二个参数指定 APK。

构建脚本默认使用 `/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity`。非默认安装位置用 `UNITY_EDITOR=/absolute/path/to/Unity` 覆盖；ADB 可用 `ADB` 覆盖，测试脚本可用 `DOTNET_ROOT` 指定 SDK 目录。

构建输出为 `artifacts/builds/XanaduYonder-0.2.0.apk`，日志为 `artifacts/logs/build-android.log`。开发包使用 Development Build 便于本地日志与存档检查，关闭脚本调试和 profiler 自动连接；使用本机 Android 调试签名，不是商店发布包。

如需单独创建/检查启动场景与构建设置：

```bash
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
mkdir -p artifacts/logs
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PWD" \
  -buildTarget Android -executeMethod XanaduYonder.EditorTools.ProjectBuild.PrepareProject \
  -logFile "$PWD/artifacts/logs/prepare.log"
```

使用同样命令将 `-executeMethod` 替换成 `XanaduYonder.EditorTools.PersistenceChecks.Run`，可以在 Unity 运行真实存档验证。批处理前关闭此项目的交互编辑器，避免同一工程被两个进程占用。

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

脚本使用配套 SDK 的 `apksigner` 替换中间 APK 的调试签名，口令只经环境变量传入；拒绝 debuggable APK，并在签名验证成功后原子替换 `artifacts/builds/XanaduYonder-0.2.0-android-arm64.apk`。中间文件不会作为发布产物保留。脚本返回非零代表本次失败，即使目录中存在较早的正式 APK，也不能将其当作本次构建成功。可以通过 `APKSIGNER`、`AAPT` 覆盖工具路径。

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
