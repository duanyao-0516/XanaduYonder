# 欢乐球球实施计划

> 执行方式：subagent-driven-development；按职责分离并行实现，接口先固定；主 agent 负责宿主与 Unity 验证，不同时启动本工作树的多个 Unity 进程。

**目标：** 在已创建的 happy-ball 工作树实现用户确认的 3D 欢乐球球，按 UI v2 复用顶部组件、移除底部所有说明，交付可验证的独立 Android 开发包。

**架构：** Core 独占状态、连续碰撞与生成；Runtime 独占触点适配、3D 表现、音效与文件边界；HappyBallPage 协调暂停、存档与宿主。AppController 只增加现有路由/生命周期接线，不改其他游戏玩法。

**工具链：** Unity 6000.3.27f1、UGUI、现有 Newtonsoft.Json、.NET 10/C# 7.3。

## 任务 1：Core 与规则测试

- [x] 在 `Tests/HappyBall/Program.cs` 先添加行为测试并运行 `scripts/test-happy-ball.sh`，确认缺失行为失败。
- [x] 新增 `Assets/Scripts/Core/HappyBall` 规则、几何、模型、固定步碰撞、随机生成和快照。角度单位为弧度，局部前方 `(sin(a)*r,y,-cos(a)*r)`，球固定局部角零，塔共享旋转。
- [x] 提供 Game(seed/snapshot)、Step(seconds,angleDelta)、GetSnapshot；输出 Bounce/LayerPassed/LayerBroken/Blocked/Armed/Died 一次性事件。
- [x] 覆盖普通黑/红、重击碎红后下一层失效、穿层去重、半穿层侧撞、竖板顶高、快速旋转/低帧率、回退重算、随机续局和长局内存/原点。

## 任务 2：3D 场景、分裂与音效

- [x] `Runtime/HappyBall/HappyBallSceneView.cs`、`HappyBallSceneGeometry.cs` 和 `Resources/HappyBall` 实现独立相机/纹理、厚黑环、红扇区、白柱、绿球与黑竖板。
- [x] SceneView Initialize/Render/Tick/SetVisible/Reset，消费同一核心几何；相机前方负 Z，塔 Unity Y 旋转为负模型角。
- [x] 每次普通通过层两大半环向屏幕左右脱离、外翻下坠；重击接触时提前碎开；通过已碎层不重播。脱离对象固定世界变换，不再随塔旋转；暂停不推进 Tick。
- [x] HappyBallAudio 使用本游戏生成的短音效，设置即时生效，退出释放自建资源。

## 任务 3：输入与真实存档边界

- [x] `Tests/HappyBallInput` 先验证单指连续位移、最终 UP、多指同帧、取消、失焦和释放门禁；实现纯 reducer 与 `HappyBallTouchInput`。
- [x] `Tests/HappyBallPersistence` 使用实际 Newtonsoft 和临时文件验证快照往返/损坏恢复/严格字段/写失败；实现 SaveData/Codec/Store。
- [x] 存档 `happy-ball-v1.json`，独立 schema、最高分、HasStarted、ResultRecorded 与完整 core 快照；不保存触点或动画。
- [x] 执行 `scripts/test-happy-ball-input.sh` 与 `scripts/test-happy-ball-persistence.sh`。

## 任务 4：页面与宿主接入

- [x] 在 Unity 页面检查中先固定顶部按钮几何、中心标题、单行数值、无品牌/副标/底部图注、场景到底部及暂停规则。
- [x] 新增 `HappyBallPage.cs`：初次开始/读档明确继续、固定步推进、事件反馈、暂停/玩法/重开/死亡、存档失败提示。
- [x] AppController 新增第八张固定高卡片、显式路由、所有页面切换互斥、隐藏本游戏常驻底部状态条、公共设置错误转交本页、Save/Suspend/Focus/Back 接线。
- [x] 本页顶部使用已有 IconButton/Home/Restart/Book/Settings 和 Label；中心只显示“欢乐球球”，下一行 `本局 <分数> / 最高 <分数> / 暂停`，不改通用 UiFactory。

## 任务 5：真实验证、审查与交付

- [x] 新增 `Assets/Editor/HappyBallUiChecks.cs`，以注入临时 Store/内存初始状态运行生产页；两种屏幕高度生成真实截图，验证触点/暂停/分裂一次性与资源隔离。
- [x] 新增 `scripts/test-happy-ball-ui.sh` 和工作树独立 Unity/Gradle 环境；运行图形检查并查看截图。
- [x] 新增 ProjectBuild 隔离构建入口和 `scripts/build-happy-ball-validation.sh`；临时配置 try/finally 恢复，产物非空且退出成功才认定构建通过。
- [x] 运行受影响合集回归、独立规格审查和代码质量审查，修正真实问题。
- [x] 已在授权真机安装并取得实际运行、暂停与设置截图；设备并发出现另一测试包安装页，停止自动点击，将未完成的后台/读档/多指/性能检查明确记录，不以编辑器代替。
- [x] 更新 README/architecture/development 与 `docs/verification-happy-ball.md`，记录真实命令、结果、APK 和验证边界。

当前无授权合并、推送或发布要求；实现保留在本工作树供检查。

完成证据：规则套件、输入 11、存档 22、Unity 本页 90 与合集 213 项通过，ARM64 独立包构建和签名校验成功。完整范围及真机未完成项目见 `docs/verification-happy-ball.md`。
