# 小恐龙验证记录 — 2026-10-27

## 范围与隔离

- 工作树：`/Users/bytedance/.codex/worktrees/chrome-dino/XanaduYonder`。
- 分支：`p/duanyao.0516/feature/chrome-dino`，来源 `master ca50db0`，设计提交 `7b66ca5` / `b88694c`。
- 模型、页面、资源、Library、测试输出和 APK 均在本工作树内；未切换、清理或合并其他工作树。
- Android 测试包：`com.duanyao.xanaduyonder.dinotest`，独立于正式包及其他任务测试包。
- 新建专用 AVD `DinoValidationApi36`，序列号 `emulator-5562`，Android 36 ARM64、1080×2400、420 dpi、软件 GPU；AVD 数据放在本树 `artifacts/android-avd/`。未向已有实体手机或其他 AVD 安装应用。
- Unity 会在退出时停止其 ADB 服务，Dino 构建与页面检查脚本使用专用 `ADB_SERVER_SOCKET=tcp:localhost:5039`；之后的构建已核对共享 5037 服务 PID 保持不变。

## 源码与存档审查

独立规格审查与代码质量审查已执行。修正了暂停态返回、方向取消时机、后台布局变化、准备／重开绕过方向校验、窄横屏触区重叠、窗口重排后的动画／昼夜保留、准备态下蹲精灵，以及一帧内结束的短触碰丢失。

使用实际 Newtonsoft.Json 与文件系统验证双纪录协议、非法内容拒绝、合法备份恢复、原子替换失败及其他游戏文件字节不变。运行记录只存最高分与偏好，不保存本局场景。组合仙人掌按对应图集切片检查碰撞轮廓。

## 自动化与构建结果

| 检查 | 实际结果 |
| --- | --- |
| `scripts/test-dino.sh` | 28 项通过，0 失败；真实图集 alpha、扫掠碰撞、短／长跳、空中下蹲、固定步、生成与实跑轨迹。 |
| `scripts/test-dino-persistence.sh` | 20 项通过，0 失败；实际序列化与文件读写。 |
| `scripts/test-dino-session.sh` | 2125 条断言通过，0 失败；包括 30／60／120 Hz 驱动一致性，此数不是独立用例数。 |
| `scripts/test-dino-page.sh` | Unity PlayMode 9 个场景、58 条断言通过，0 失败；真实 Page／UGUI 对象与回调，方向就绪输入通过反射控制。 |
| `scripts/test-minigame-ui.sh` | Unity 两种竖屏尺寸 209 条断言通过；七卡顺序、跳一跳回调、数独弹窗／输入／计时、图标与羊场景。 |
| 已有纯 C# 回归 | Core、Input、Persistence、Jump、Sheep、Watermelon、Idiom、Sudoku 的对应主测试脚本全部通过。 |

页面检查启动时 Unity Editor 的 QuickSearch 索引服务记录过 `SearchDatabase.EnumerateAll` 越界异常；堆栈不在游戏程序集，页面检查随后完成并以 0 退出。保留原始日志，未把它说成运行时异常，也未为本功能修改 Unity 索引配置。

公平性回放共检查 1868 个实际生成障碍；同时回放最早、推荐、最晚成功输入，晚一帧动作碰撞也有独立回归：

| 逻辑视野宽度 | 安全速度上限 | 扣除 100 ms 后的最小有效窗口 |
| --- | ---: | ---: |
| 420 | 189.25 | 1.200 s |
| 600 | 276.5 | 1.200 s |
| 900 | 360 | 1.583 s |
| 1200 | 360 | 2.317 s |

这些数据来自生产 Core 的确定性回放，不依赖横屏／竖屏名称。角色尺寸、跳跃、碰撞和计分规则共用；间距由通过轨迹及恢复时间推导。

最终 `scripts/build-dino.sh` 以 0 退出。APK 身份已用 `aapt dump badging` 核对为 `com.duanyao.xanaduyonder.dinotest`，versionName `0.4.0` / code `4`；构建后项目正式应用标识及产品名已恢复。文件：

- `artifacts/builds/XanaduYonder-0.4.0-dino-validation.apk`
- SHA-256：`9bc3e181678d8afe26fbf9de6158259b0c2120a229791d9fc4c4f2459da463a0`
- 最终包重新安装后完成竖屏开局、真实碰撞与新纪录 34 分结算、重开清零、改为横屏的回归；`Unity:E` / `AndroidRuntime:E` 日志为空。

## Android 已执行的交互检查

在专用模拟器安装独立 APK 后，以实际 Android 点击／滑动、系统返回、Home 与 Activity 恢复操作检查：

- 大厅第七张卡片、竖屏选择页、竖屏准备与全安全区宽跑道。
- 横屏切换与全宽布局，动作按钮不遮挡跑道；横屏退出后恢复竖屏大厅。
- 首次跳跃开始、暂停返回确认、取消确认返回暂停、继续前倒数。
- 横屏运行中切后台，回来维持暂停；未自动推进。
- 实际持久化 JSON 同时出现独立的竖屏／横屏纪录与 `landscape` 偏好；再次进入仍先展示选择页并预选横屏。
- 进程重启从大厅开始；旧 2048 入口、滑动合并及计分仍可用。

截图在 `artifacts/screenshots/dino/`；设备错误日志在 `artifacts/logs/dino-device-errors.log`。截图与 APK 不作为源码提交。

## 验证边界

- 模拟器上的交互检查不能代表实体手机上的触感、音量、长时间温升、耗电或真实触控延迟。
- 原生多指取消、各厂商刘海／分屏策略、横屏左右两方向及持续高分夜景仍需要实体机体验回归；页面状态与夜景连续性由 Unity 场景检查覆盖，不能据此声称上述硬件面已验收。
- 软件 GPU 模拟器曾触发超过 100 ms 的帧暂停保护，未补算停顿时间。该现象不能作为真机性能结论。
- 保留 1.2 秒有效反应窗口与 0.25 秒恢复间隔是可重复验证的规则约束，不等同于真人难度已调优。
