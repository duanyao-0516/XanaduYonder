# 成语接龙键盘与气泡验证

日期：2026-10-30。工作树：`idiom-investigation/XanaduYonder`；分支：`p/duanyao.0516/feature/idiom-local-versus`。没有切换、提交或推送分支，没有修改其他工作树。

## 实现范围

- 成语专用设置新增“发送后收起键盘”（默认开）与“是否显示拼音”（默认关），即时生效并持久化；缺失旧字段独立补默认，非法显式值继续保护原档。
- 专用输入框保留发送 pointer-down 阶段的焦点，成功提交后才决定是否收键盘。保留键盘时通过 UGUI 的原生文本同步清空已提交词；无效提交不清空、不关闭。其他控件、弹窗、切页和后台沿用关闭路径。
- IME 测量在安全区更新后转换，原生快照校验 surface 宽高比例。不匹配期间同时暂停布局目标更新和动画推进，防止新窗口高度再次扣减旧 inset。
- 聊天 viewport 底边和输入栏共用最终位置的 200ms 缓出过渡，标题及聊天顶部固定。过渡开始时固定贴底意图或历史锚点，行高切换保留消息索引和像素偏移。
- 删除顶部常驻接字提示，聊天区向上扩展；气泡按文字测量加内边距，成语字号 30，左右对应对齐。默认隐藏拼音并收短气泡高度，打开后显示 19 号拼音；详情资料不受开关影响。

## 自动化与审查

| 检查 | 结果 |
| --- | --- |
| `scripts/test-idiom-persistence.sh` | 39 通过、0 失败；两项开关四种组合、缺字段默认、非法值保护，以及其他对局/草稿/生词/学习记录不变 |
| `scripts/test-idiom-ui.sh` | 720×1280、720×1582、900×1582，共 8,413 个断言通过 |
| 平台测量边界 | 11 个独立边界用例通过；持久化到 Unity 检查的用例包含缩放、surface 变高/变矮、过期比例和非法测量 |
| Android bridge | Android 36 `javac --release 8 -Xlint:all -Werror` 编译通过 |
| Android APK | 独立 Gradle 缓存/构建 ADB 端口，构建退出码 0，APK v2 签名校验通过 |
| 独立源码审查 | 发现并修复跨尺寸测量等待期间重复补偿；修复后无剩余重要问题 |

UI 检查执行真实 `EventSystem deselect → pointer down → release/send`，覆盖人机和双人两种关闭偏好、无效词保留焦点、其他控件失焦；验证动画中间帧/终点、标题位置、过期测量同时冻结和恢复、历史锚点跨过贴底阈值不误滚动、拼音开关真实点击/即时刷新/磁盘回读。它不是 Android 原生输入法运行验证。

红灯证据：新拼音默认隐藏断言先失败，存储三组新增检查也先失败；实现后通过。焦点夹具最初缺少 batchmode 下 EventSystem 注册，修正为配对生命周期后执行真实焦点时序，不修改生产输入框来迁就夹具。

日志在 ignored 目录 `artifacts/logs/`：`idiom-preferences-red.log`、`idiom-preferences-green.log`、`idiom-keyboard-ui-red.txt`、`idiom-ui-checks.txt`、`build-idiom-keyboard-command.log`。

## 测试包与设备边界

- APK：`artifacts/builds/XanaduYonder-0.7.0-idiom-validation.apk`，146,720,252 字节，SHA-256 `189fa12290206ccc7bbb3cfa07f1c67f8a65066c442809a4fbabb165a9fae812`。
- 包名 `com.duanyao.xanaduyonder.idiomtest`，版本 0.7.0 / code 7，ARM64 Development。该路径已替换前次气泡调整构建；旧 SHA 在历史记录中保留。
- OPPO PKM110 / Android 16 于 14:13:59 覆盖安装成功，启动 `Status: ok`。安装前后首次启动前，20 份 JSON 主存档/备份 SHA-256 全部一致。
- 实际打开游戏查看到默认隐藏拼音、自适应气泡和删除提示行后的页面；键盘弹起时输入栏位于键盘上方、标题保持原位。
- 发送测试期间前台切换为另一工作区的 `startuptest`，故未把原生发送收起/保持或过渡流畅度计作通过。停止操作后，仅恢复本任务独立包的 20 份原存档并逐项校验一致，未触碰另一包的数据。
- 用户随后明确要求代码完成即可，安装与真机测试待其通知并由用户验证。因此未继续设备或模拟器操作。两项开关的原生输入法体验、快速连发、后台恢复及实际动画流畅度仍待真机验收。

设备证据位于 ignored 目录 `artifacts/device-idiom-keyboard/`，含备份、安装后文件、恢复校验与页面截图。实现行为详见[玩法文档](idiom-game.md)和[键盘方案](superpowers/specs/2026-10-30-idiom-keyboard-transition-proposal.md)。

## 用户验收前重新打包安装

用户确认手机空闲后，于 2026-10-30 14:20（Asia/Shanghai）重新执行 `scripts/build-idiom-validation.sh`，构建退出码 0，确认 APK 修改时间晚于构建前文件，签名验证通过。当前 APK 为 146,720,252 字节，SHA-256 `386fb42feea7dd4c0e09e9eee10674b81783fe6264e4ccfe0ec6b236ff999fa6`；包名、版本及架构不变。

OPPO PKM110 于 14:20:37 覆盖安装成功。安装前再次备份，安装后首次启动前逐项校验 20 份 JSON 主存档/备份，SHA-256 全部不变。显式启动返回 `Status: ok`，确认前台为 `com.duanyao.xanaduyonder.idiomtest` 且大厅正常显示，留给用户自行验收；没有开始测试对局或更改设置。未产生 ProjectSettings、Scenes、Packages 改动。

本次证据位于 `artifacts/device-idiom-reinstall-20261030/`；安装成功与启动成功不替代用户后续的键盘及玩法验收结果。

## 用户验收反馈

2026-10-30，用户在上述重新打包安装后明确反馈“测试没问题了”，并要求提交、合并及推送远端 `master`。此项作为用户对本次测试包的验收记录；不扩展为未逐项记录的设备矩阵或自动化覆盖结论。
