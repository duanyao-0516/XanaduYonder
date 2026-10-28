# Dino 验证记录 — 2026-10-27

本文前半部分记录首版实现的历史验证；后续 UI 修订以文末记录为准。首版 APK 的哈希和全宽布局描述保留为历史证据。

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

## UI 修订验证

根据用户在 OPPO 上的反馈，展示名称统一为 Dino，导航、标题、分数卡、按钮、说明文字与弹窗沿用跳一跳／2048 的 UiFactory 字体及配色。竖屏跑道改为 608 × 320 设计单位，两种方向都保留左右各 56 的页边距并由 UGUI Mask 实际裁剪圆角。物理视野从卡片的实际宽度计算，Core 规则与存档协议未变。

独立只读审查发现圆角 Mask 缓存了精灵材质，导致夜间对比度切换失效；已改为两份不可变材质并在昼夜门限切换引用，检查初始化、动态创建、恢复及释放路径。没有修改公共 UiFactory 或其他小游戏页面。

| 本次检查 | 实际结果 |
| --- | --- |
| `scripts/test-dino-persistence.sh` | 20 项通过，0 失败；文件名、字段及其他游戏文件隔离保持兼容。 |
| `scripts/test-dino-page.sh` | Unity PlayMode 11 个场景、138 条断言，0 失败；新增实际跑道宽度、圆角裁剪、紧凑横屏触区与共用 UI 配置检查。 |
| `scripts/test-dino-visual.sh` | 192 条断言通过；真实 UGUI 渲染 720×1320、720×1582、1515×720、730×720。 |
| `scripts/test-minigame-ui.sh` | 209 条断言通过；两种竖屏的大厅、跳一跳、数独、图标及羊场景回归。 |

人工检查选择页、准备页、帮助、设置、暂停和夜景截图，未见重叠或截断；横屏右侧页边距和圆角可见。日→夜→日复用同一个角色和遮罩，真实主体像素在三个阶段各检测到 1917 个，最终渲染材质倍率为 1 → 2.627 → 1，恢复白天正常。画面位于 `artifacts/validation/dino-ui/`，含受控示例分数，不是真机游玩纪录。

本次 Unity 检查使用受控视口，不能替代 Android 安全区、系统方向和实际触控验证；真机安装与检查结果另记如下。

### 修订版构建与 OPPO 检查

- `scripts/build-dino.sh` 成功，退出码 0。`aapt` 确认包名仍为 `com.duanyao.xanaduyonder.dinotest`，版本 `0.4.0` / code `4`，应用名改为 `Xanadu Dino测试`；构建后正式项目设置无差异。
- APK：`artifacts/builds/XanaduYonder-0.4.0-dino-validation.apk`，SHA-256：`53c6bb55716cb5e4292a4b09b5182dd2ce460f967cdc54d0aa08582827ac0b8f`。
- 2026-10-27 17:57 在 OPPO PKM110（Android 16，序列号 `3B6F4NE8A66ZXTRL`）执行 `adb install -r`，返回 `Success`；启动 Activity 返回 `Status: ok`。没有卸载或清除数据，没有操作其他设备。
- 安装前后及检查结束时的 `dino-v1.json` 字节一致：竖屏最高 149、横屏最高 268、横屏偏好保留。
- 实机检查大厅的 Dino 名称、方向选择页、竖屏准备页、跳跃后暂停、结束本局确认、切换到横屏准备页。确认两种方向的圆角和安全区留边，竖屏跑道不再占据大块空白高度。
- 实机截图在 `artifacts/screenshots/dino-ui/`，主要为 `oppo-portrait.png` 与 `oppo-landscape.png`；从本次启动起抓取的 `Unity:E` / `AndroidRuntime:E` 日志为空（`artifacts/logs/dino-ui-oppo-errors.log`）。检查后停留在横屏准备页。
- 本次实机回归针对 UI 与基本流程；未重做长时间高分游玩、音效、双指操作和性能测试。昼夜材质切换由上述真实 Unity 渲染检查验证。

## 黑白昼夜、庆祝彩蛋与加速修订

此轮按用户反馈恢复白色／黑色跑道，改为每 30 秒有效奔跑时间交替昼夜，加速度从 2 提高至 6，并加入指定节日及农历四月十五／五月十六的生日彩蛋。规格见 `docs/superpowers/specs/2026-10-27-dino-atmosphere-design.md`。外层 App UI、跑道圆角留边、碰撞与存档协议未改。

### 自动化与独立审查

- 加速先对旧实现跑出三项预期失败，再修改 `SpeedAt` 与 `Travel`：最终 `scripts/test-dino.sh` **33 项通过，0 失败**。共回放六种宽度的 2,858 个生成障碍，包含真实竖屏逻辑宽 506.667（551 个）和横屏 1169.167（386 个）。二者安全上限为 241／360，最小有效反应窗口为 1.200／2.250 秒；横屏达到上限从 90 秒缩至 30 秒，竖屏从 30.5 秒缩至约 10.17 秒。
- 位移预测与逐 tick 求和在加速段、跨封顶点、封顶后及 `long.MaxValue` 邻近区间一致。模型零逐帧分配检查通过；新公开的只读最大跳高边界由真实长跳验证，用于给帽顶留空间，不改变运动常量。
- `scripts/test-dino-presentation.sh` **8 组、90 条断言，0 失败**：公历边界、春节初一至初七、端午／中秋、2026／2027 两个生日、闰月不重复、跨农历年与超支持范围。先用空实现验证预期失败；Unity 自带 Mono 的日历类也已独立执行。
- `scripts/test-dino-session.sh` **2,125 条断言通过**。
- `scripts/test-dino-page.sh` **14 个场景、253 条断言，0 失败**：包含生日横幅不拦截方向选择、实际长跳逐帧帽子边界、下蹲／死亡锚点、日期快照随重排保持及重新进入刷新。
- `scripts/test-dino-visual.sh` **364 条断言通过**：旧绿色天空先在白色断言失败。新实现用真正的 `Game.Step` 和生产障碍的推荐输入驱动连续对局，29.9 秒白色、30.4 秒灰色过渡、31 秒黑色、60.4 秒过渡、61 秒白色、91 秒再次黑色；不直接设置分数或夜色字段来冒充切换。
- 昼夜复用同一角色及其遮罩材质，像素与实际渲染材质同时检查；暂停冻结中间渐变，真实夜景重排保留动画与颜色，重开恢复白天。注入固定日期检查四类帽子、两个生日入口与横屏生日帽；彩色帽像素在白天和黑夜均可见。
- 独立交叉审查分别覆盖运动预测、日期策略、显示生命周期与测试边界，未留待修问题。初次集成编译发现新帽子 meta 的 GUID 长度错误，以及 Runtime 引用了 Core 内部常量；均已修复并重新通过 Unity 编译、页面与渲染检查。

画面位于 `artifacts/validation/dino-ui/`，主要包括 `night-1515x720.png`、`day-restored-1515x720.png`、`birthday-selection-0531-720x1320.png`、`birthday-landscape-1515x720.png`、`hat-festival-0101-720x1320.png`、`hat-halloween-1031-720x1320.png`、`christmas-night-720x1320.png`。这些是生产 Unity UI 的受控日期／视口截图，非当天 OPPO 上自然触发的节日画面。没有修改手机系统日期。

### 本轮 APK 与安装

- `scripts/build-dino.sh` 成功退出 0，Android IL2CPP 构建完成；构建后正式 `ProjectSettings` 无差异。
- `aapt` 确认 `com.duanyao.xanaduyonder.dinotest`，`0.4.0` / code `4`，应用名 `Xanadu Dino测试`。APK 仍在 `artifacts/builds/XanaduYonder-0.4.0-dino-validation.apk`，SHA-256 为 `a6901521fe78b32c0d349a2b9665191ce90b0e95d69056122d6f1fad7b4892fb`。
- 2026-10-27 18:26 在既有 OPPO PKM110 上执行 `adb install -r` 返回 `Success`；安装前后 `dino-v1.json` 字节一致，竖屏最高 149、横屏最高 414、竖屏偏好保留。没有卸载、清数据或安装到其他设备。
- 启动 Activity 返回 `Status: ok`，但手机当时已锁屏，未据此宣称画面或触控实机验收通过。已请求用户解锁，当前实际昼夜、生日及节日验证证据为上面的 Unity 测试；未修改或绕过设备锁屏。

## 2026-10-28 实测反馈：布局、返回、地面和提速

本轮在同一 Dino 工作树及 `p/duanyao.0516/feature/chrome-dino` 分支修订，没有合并其他工作树。需求与最终布局见 `superpowers/specs/2026-10-28-dino-feedback-layout.md`，Chromium 参数核对见 `dino-chromium-reference.md`。

### 最终行为

- 方向卡下移，恢复横竖屏示意框、最高纪录标签及独立加粗数值；开始按钮固定在下方。短选择窗口使用滚动卡片，最短生日布局仍可完整显示一张卡片。
- 游戏场景左右边距统一为 32 设计单位，与跳一跳场景区一致；文字和导航保持公共 56 单位边距。竖屏本局／最高分使用无底卡层级，小下蹲、大跳跃、下方玩法与暂停；横屏下蹲／暂停／跳跃。卡内说明长短按。
- 游戏页不单列更换方向，从暂停菜单进入。顶部返回箭头先回方向选择，运行中先确认并保存纪录；选择页再回大厅。结算里的显式「返回大厅」仍直接离开。
- 采用图集完整平坦与小丘两条地面，地平线穿过脚部 10.8–12.15 逻辑单位；地形按绝对世界片段稳定选择，渲染重排不重抽。角色、障碍物和碰撞坐标不因地面改变。
- 加速度由 6 提高到 12 单位／秒²，起步仍为 180，全局最高仍为 360。宽横屏达到上限由 30 秒缩至 15 秒。新竖屏实际视野 546.667 的安全上限为 267.25，第 437 tick（约 7.28 秒）封顶；上限相较旧版增加来自跑道实际变宽，算法未放宽。

### 实际验证

| 检查 | 最终结果 |
| --- | --- |
| `scripts/test-dino.sh` | 33 项通过，0 失败；6 种宽度、2,875 个实际生成障碍回放。竖屏／横屏最小有效反应窗口分别为 1.200／2.350 秒。逐 tick 位移与加速预测一致。 |
| `scripts/test-dino-session.sh` | 2,125 条断言通过，0 失败。 |
| `scripts/test-dino-ground.sh` | 56 条断言通过；原图丘陵像素、双 UV、主线与不透明脚像素交集、跨片连续性、暂停重建及模型坐标不变。 |
| `scripts/test-dino-page.sh` | 18 个场景、382 条断言通过，0 失败；方向卡、下方触区、返回层级、生日及暂停等实际 Unity Page／UGUI 对象检查。 |
| `scripts/test-dino-visual.sh` | 464 条断言通过；真实 UGUI 渲染、连续 Game.Step 昼夜切换及庆祝帽；包含短视窗滚动上下端与选择后的截图。 |
| `scripts/build-dino.sh` | 最终 Android IL2CPP APK 构建成功，进程退出 0；构建后 `ProjectSettings` 无差异。 |

本轮先对旧加速度验证三项预期失败，再通过新曲线检查。独立审查发现的短屏选择重叠、1152 高竖屏拒绝进入，以及更矮窗口状态文字压入跑道均已修复并覆盖回归。竖屏游戏至少需要 1120 设计单位高度；1068／1080／1107 会提示调整窗口，1152 能正常准备。720×720 生日选择页滚动视口为 272 高，可完整呈现 224 高方向卡。

人工查看生产渲染的 720×1152、720×1320、720×1582、1515×720、730×720 游戏页，以及 720×960 和生日 720×720 选择页；确认按钮层级、圆角留边、地面交叠与小丘、生日横幅和黑白昼夜。截图在 `artifacts/validation/dino-ui/`，分数和日期为受控测试数据，不是 OPPO 真实纪录。

Unity Editor 启动期间仍出现既有 QuickSearch `SearchDatabase.EnumerateAll` 异常，堆栈属于编辑器索引服务；上述测试和构建均继续完成。未据此修改编辑器或公共游戏代码。

### APK 交付与未验证范围

- APK：`artifacts/builds/XanaduYonder-0.4.0-dino-validation.apk`。
- SHA-256：`f331229ba50f52d36b4a0d7b7401b4c41872f90b35b30c3233be86911fcd43c7`。
- `aapt` 核实包名 `com.duanyao.xanaduyonder.dinotest`，版本 `0.4.0` / code `4`，应用名 `Xanadu Dino测试`。
- 设备准备阶段读取到 OPPO 现有纪录为竖屏 201、横屏 414、横屏偏好；随后 ADB 连接断开。用户明确选择「先给我 APK，稍后安装」，因此本轮未安装、未修改手机数据，也未操作其他测试应用或设备。
- 本轮完成源码审查、核心回放、Unity 页面与截图、Android 构建；未完成新版 APK 的实机触控、实际 Android 安全区／旋转、高刷新率体感及音效复测。后续覆盖安装应保留数据，不要卸载旧测试包。

建议设备复测：方向选择和开始按钮的拇指可达性；竖屏大跳跃与小下蹲；横屏中部暂停；游戏返回→选择→大厅及运行中取消确认；地平线／小丘不影响碰撞；前 5／10／15 秒提速、短跳／长跳和暂停续玩；最后确认旧纪录保留。

## 2026-10-28 后续修订：四倍高速、暂停细节与完整节日截图

用户明确确认「按四倍实速做高难度版，不加预警」。此次实际运行峰值为上一轮各视野峰值的四倍，加速度 12→24，初速仍为 180；旧 1.2 秒门槛只作为视野参考上限的历史基准，当前真实候选门槛为扣除 0.1 秒输入预算后至少 0.15 秒。典型竖屏实际峰值 1069，37.05 秒达到；宽横屏实际峰值 1440，52.5 秒达到。跳跃物理、0.25 秒恢复间隔及存档协议保持。设计见 `superpowers/specs/2026-10-28-dino-high-speed-design.md`。

竖屏暂停原位加粗、移除底部重复玩法入口；横屏暂停加粗并下移 34，与左右动作卡中心一致，保存提示同步避让。玩法弹窗主按钮距底边 32，两按钮间距 14，正文截图完整可读。

### 最终验证

- `scripts/test-dino.sh`：旧实现先出现 9 项预期失败，修改后 **38 项通过，0 失败**。8种宽度回放7,402个实际生成障碍，覆盖每tick完整加速段的单株后备障碍、逐tick精确位移、到顶后持续生成、最早／推荐／最晚有效操作；典型竖屏最低有效反应窗口0.15秒，宽横屏0.4秒。
- `scripts/test-dino-session.sh`：2,125 条断言通过。
- `scripts/test-dino-page.sh`：19个场景、412条断言通过，0失败；新增暂停Bold、原位／中心对齐、重复入口移除、提示不重叠、玩法弹窗底距及关闭路径。
- `scripts/test-dino-visual.sh`：704条断言通过；黑夜真实推进、所有四类帽子在日夜均有彩色渲染像素、两个生日入口、圆角／字体／重排。截图原图与日期说明见 `dino-ui-preview.md`，本轮归档 `artifacts/validation/dino-ui-highspeed/`。
- `scripts/test-dino-ground.sh`：56条断言通过。
- 独立源码审查未留阻断问题；仓外探针对照HEAD5396d5a，在20个视野上证明新峰值=旧峰值×4，180,000 tick、1,952个随机有效操作障碍全部通过，所有视野在满速后继续生成。该探针只补充源码／模拟证据，不替代真机输入。
- 最终 `scripts/build-dino.sh` 成功退出0，Android IL2CPP构建成功，`ProjectSettings` 无差异。APK为 `artifacts/builds/XanaduYonder-0.4.0-dino-validation.apk`，SHA-256 `47e8ed8a386b10a634233bbed78fbc36ac2660a186316a4886f13928a4f0e490`。

### OPPO 覆盖安装

2026-10-28 11:42在OPPO PKM110（`3B6F4NE8A66ZXTRL`）执行保留数据的 `adb install -r`，返回 `Success`；包 `com.duanyao.xanaduyonder.dinotest`，0.4.0/code4。安装前后Dino存档字节一致：竖屏269、横屏414、横屏偏好。没有卸载、清数据、修改手机日期或操作其他设备。

此次已核对安装和存档；未在手机上代替用户完成高速真人触控、音效或日夜／节日的真机视觉验收。给用户看的黑夜与节日图片是生产Unity受控视口渲染，非当天手机日期触发的截图。

## 2026-10-28 Chrome 规则整体对齐与碰撞修复

用户明确批准采用 Chrome 原版速度、加速度、跳跃和间距，修复白边及碰撞并覆盖安装 OPPO。本轮仅在既有 Dino 工作树执行，基线 `0b6e0d7`，没有合并或改动其他工作树。设计与执行范围见 `superpowers/plans/2026-10-28-dino-chrome-alignment.md`。

### 实现与证据

- 采用固定归档 Chromium `3df8d372a3f9d5c38a89d0bc496cc44c85af1feb` 普通移动端规则，统一 60 Hz；不继承浏览器 120 次回调时加速翻倍的差异。速度、跳跃、间距、组数、速度门槛、两档翼龙高度和偏移、计分均进行原图单位→世界单位换算。固定时间类型阶段、四倍上限、恢复窗口及生产轨迹筛选被原版规则取代。
- 官方外框内缩、细分框、仙人掌整组伸展与严格边界，配合连续扫掠。全部对象停在最早接触比例；`PresentationDistance` 用于地面和天空的部分帧位移，计分距离仍只统计完成的固定步。下蹲碰撞保持蹲姿。
- 夜间生成独立的 RGB 反色图集，保留 Alpha 和 point/clamp，随视图释放；原 PNG 和 meta 字节不变。原白边夜间映射为黑色，身体 83→172，节日帽独立保留彩色。圆角、外层 UI、30 秒昼夜节奏和存档协议不变。
- BSD 原始许可继续打包，新增 C# 移植部分署名、第三方声明及游戏内规则来源说明。

真实失败先行：旧夜景在恐龙区域检出 1005 个白色像素，新版横竖屏夜景为 0；日间恢复、第二次入夜同时通过。地面旧终止位置约 -964.0759、新接触应为 -970.4566 的回归先失败，接入部分帧距离后通过。下蹲结束原先强制站姿的真实模型／渲染检查先失败，修复后通过。核心原版轨迹与速度、首次生成时机、官方碰撞和可见实体间隙均先在旧实现失败。

独立审查发现的后继重复生成和宽屏列表扩容均已补回归修复：原版 `followingObstacleCreated` 状态防止较快后继先出屏后重复生成；列表预分配 32，在允许最大宽度 4096、两个种子各 20,000 步检查中运行帧分配为 0。审查未留下阻塞问题。

| 检查 | 本轮实际结果 |
| --- | --- |
| `scripts/test-dino.sh` | 20 组通过、0 失败，包含 7 组官方碰撞回归；独立原版逐帧跳跃／释放／快速下落、13,000 tick 障碍生成对照、最早碰撞、终态冻结与 long.MaxValue。30／60／120 Hz 均完成相同 100 秒真实障碍回放且状态一致。 |
| `scripts/test-dino-session.sh` | 2,125 条断言通过。 |
| `scripts/test-dino-persistence.sh` | 20 项通过，Dino 与其他游戏存档隔离保持。 |
| `scripts/test-dino-presentation.sh` | 8 组／90 条断言通过，节日及农历生日边界保持。 |
| `scripts/test-dino-page.sh` | 19 个场景／412 条断言通过，方向、返回、暂停、控件、弹窗和帽子生命周期保持。 |
| `scripts/test-dino-ground.sh` | 59 条断言通过，新增真实碰撞的部分帧地面停止及终态冻结。 |
| `scripts/test-dino-visual.sh` | 1,073 条断言通过；真实昼夜回放、横竖屏白边、日间恢复、下蹲撞停，以及四类帽子的日夜像素。 |
| 官方参考与源码完整性 | 17 份来源文件及参数重新提取校验通过，原始运行图集／meta 无修改，`git diff --check` 通过。 |

本轮图像归档在 `artifacts/validation/dino-chrome-alignment/`，失败及构建日志在 `artifacts/logs/dino-chrome-alignment/`。图片为生产 Unity UGUI、受控视口／日期和示例纪录，非手机系统日期自然触发；未修改手机日期。独立诊断回放只用于测试，不参与生产生成，也不保证任意随机长序列自动通关。官方细框仍是轮廓近似，不能表述为逐像素碰撞。

### APK、OPPO 安装及真机检查

- `scripts/build-dino.sh` 成功，退出码 0，日志 `Build Finished, Result: Success.`；Android IL2CPP 产物生成后 `ProjectSettings` 无差异。最后一次 UI 检查后生产代码仅把障碍列表预分配 8 改为 32，该修改已通过核心 GC 回归并在最终 Android 构建中重新编译。
- APK：`artifacts/builds/XanaduYonder-0.4.0-dino-validation.apk`；SHA-256 `43492ef9e077c6195b7f0c47757b90d6f94e3af607ca2519c253e4cdb79b3270`。`aapt` 核实测试包 `com.duanyao.xanaduyonder.dinotest`、版本 `0.4.0`／code `4`、应用名 `Xanadu Dino测试`。
- 2026-10-28 12:52:52 在 OPPO PKM110（序列号 `3B6F4NE8A66ZXTRL`）覆盖安装，`adb install -r` 返回 `Success`。未卸载、清数据或操作其他设备。
- 安装前、安装后、交互检查结束后的 `dino-v1.json` 三份文件逐字节一致：竖屏纪录 544、横屏纪录 539、横屏偏好。对应文件在本轮归档目录 `oppo-before-install.json`、`oppo-after-install.json`、`oppo-after-checks.json`。
- 真机冷启动返回 `Status: ok`，随后实际检查大厅双纪录、横竖屏准备页与跳跃、暂停菜单、结束本局并切换方向、竖屏系统返回确认后回到方向选择。最终恢复横屏准备页。`oppo-landscape-jump.png`、`oppo-portrait-jump.png`、`oppo-portrait-back.png` 和 `oppo-final.png` 为此次 APK 的真实手机截图。
- 当前应用 PID 的 `Unity:E`／`AndroidRuntime:E` 日志为空，保存在 `artifacts/logs/dino-chrome-alignment/oppo-errors.log`。

本次实机只覆盖上述基本输入与流程，没有用手机持续跑满整个加速周期、测量硬件触控延迟／音效／温升，也没有在手机上自然触发全部节日。长时规则一致性由确定性模型回放验证；黑夜、四类帽子和生日横幅由真实 Unity 受控渲染验证。
