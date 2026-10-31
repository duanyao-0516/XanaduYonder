# 飞机大战接入与验证记录

当前交付为 v0.9.4：大厅进入游戏原始菜单（经典模式／关卡模式／历史成绩），返回大厅、玩法说明与设置只显示在入口页。实战新增的导航与顶部遮挡栏已移除。0.9.3 的入口修订及 0.9.4 按用户要求仅构建并发布 APK，未运行额外测试或成品校验。下述自动化、模拟器与散列记录属于入口修订之前的初版，不能视为当前界面的回归结果。

当前 APK：`artifacts/builds/XanaduYonder-0.9.4.apk`；Android 版本名 `0.9.4`、版本码 `13`。沿用 `com.duanyao.xanaduyonder.aircrafttest` 与本地测试签名，可覆盖此前飞机大战测试包，并与正式应用共存。

验证日期：2026-10-31。基线为 `origin/master` 的 `2586c249`（v0.9.1），工作分支为 `p/duanyao.0516/feature/aircraft-fire`，使用独立 managed worktree。原始检出目录与未提交文档没有改动。

## 初版交付与验证范围（入口修订前）

第 20 个大厅入口“飞机大战”已接入双列、分类目录和单列。单列使用 APK Manifest 指定的真实图标及圆角遮罩，另外两种视图使用同风格原创飞机线条图标。点击直接进入竖屏全宽战场；背景覆盖整个屏幕，HUD 避开安全区，右上角是合集常规玩法说明与设置按钮。

玩法由独立 Unity/C# 模型和页面实现：经典普通／疯狂、七关闯关、三类敌机与 Boss、相对拖动、自动射击、炸弹、90 次双发、生命、一次免费复活、暂停和分模式最高纪录。原包的联网、账号、通知、广告、购买、推荐和 native 运行库均未接入。已恢复的参数与明确调整见 [APK 参考记录](aircraft-fire-reference.md)。

## 已完成验证

| 项目 | 结果 |
| --- | --- |
| `scripts/test-aircraft-fire.sh` | 13 个规则场景通过；涵盖真实射击/碰撞、双发、炸弹、暂停、复活、七关推进、疯狂随时间变化、Boss 连发间隔、离场回收及尺寸变化 |
| `scripts/test-aircraft-fire-persistence.sh` | 48 项断言通过；严格 JSON 类型/版本/重复键、主备份保高、损坏恢复、原子保存和防止旧数据覆盖 |
| Unity 6000.3.27f1 Android 编译 | 通过，无 C# 编译错误 |
| `scripts/test-aircraft-fire-ui.sh` | 108 项断言通过；生产 UGUI、真实 Raycast 与 Camera.Render；720×960、720×1320、720×1600 的全宽布局、拖动所有权、说明/设置暂停、后台恢复、模式切换、终局动画和复活 |
| `scripts/test-lobby-design.sh` | 3,607 项断言通过；720×1320／720×1582 三种布局、20 个真实入口、Logo/圆角、小图标、纪录投影与点击路由 |
| `scripts/build-aircraft-fire.sh` | Unity IL2CPP ARM64 APK 构建成功；复用并校验同 master 基线已生成的 Kingdom Rush 构建依赖，没有改动其内容 |
| 成品检查 | `apksigner verify` 通过；仅 `arm64-v8a`，没有原包 `libgame.so`，Manifest 唯一权限为 `android.permission.VIBRATE` |
| 代码与资源审查 | 独立交叉审查已处理双发碰撞、疯狂计算顺序、Boss 连发延迟、终局动画/音效、存档重试；43 个资源散列与尺寸核对一致，Unity metadata 完整，`git diff --check` 通过 |

Unity 图像及报告在 `artifacts/aircraft-fire-ui/`；三种大厅截图在 `artifacts/lobby-design/`。这些检查覆盖生产页面，但不代替物理设备验证。

## 模拟器简测

使用现有 ARM64 Android 16 / API 36 模拟器的只读会话，1080×2400，安装独立测试包。通过大厅进入后直接运行全宽战场，拖动改变战机位置并自动射击；实际结束一局，最高分 12,000，使用免费复活继续。右上角说明可打开并暂停；设置复用公共弹窗，关闭声音后实际保存文件的 `soundState=2`。关闭设置后保持暂停，返回大厅显示最高 12,000 分；独立 `aircraft-fire-v1.json` 中对应经典纪录为 `12000`。捕获的 Unity / AndroidRuntime 日志没有本次应用异常。

模拟器冷启动曾出现 System UI 未响应弹窗，点击“等待”后恢复，之后完成上述流程。未为此调整应用代码或继续排查模拟器。截图和日志保存在 `artifacts/aircraft-fire-emulator/`。

## 测试安装包

- 文件：`artifacts/builds/XanaduYonder-0.9.1-aircraft-fire-preview.apk`
- 大小：318,384,324 bytes（约 304 MiB）
- 包名：`com.duanyao.xanaduyonder.aircrafttest`，可与正式应用共存，不覆盖正式版数据。
- 本地测试签名、非 Development Player；不是正式发行版，不创建 Release。
- SHA-256：`db859fbbd482c5f1e134e2a60e167a487e90014544b4a30b4fee8156353ae4d1`

## 留给用户的主要验收

本轮没有连接物理设备。真机触控手感、多指同时拖动/放炸弹、音效听感、长时间性能，以及七关完整手动通关未执行；交由用户主要验收。原 APK 只能恢复静态代码证据，不能宣称二进制级或逐帧等价。
