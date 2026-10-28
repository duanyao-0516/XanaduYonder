# 黄金矿工视觉与机械体验修复计划

> **For agentic workers:** Use subagent-driven-development with disjoint UI, core balance and presentation ownership. Work only in worktree `0053/XanaduYonder`.

**Goal:** 按用户已认可的 11 张预览还原所有界面，修正绞盘、矿工和绳钩运动，建立可感知的矿物重量差异。

**Architecture:** UI 样式只在 GoldMiner 目录内收敛，引用原有美术和正式切片；机械呈现从 SceneView 拆为专属 view，不改变碰撞和指针路由。回收规则、关卡见证和存档兼容由 Core 统一负责，不能仅在渲染层假减速。

**Tech Stack:** Unity 6000.3.27f1 / UGUI / C# deterministic fixed-step simulation / Android.

## 1. UI 原稿还原

- [x] 逐张检查 `artifacts/gold-miner-ui-v1/screens/01-mode-select.png` 到 `11-confirm-exit.png`，比对现有运行截图。
- [x] 修改 `GoldMinerPage.Layout.cs`、`GoldMinerPage.Dialogs.cs`、`GoldMinerLayout.cs`：恢复原稿面板圆角和描边、暖白底、深绿文字、金色/青色玩家区分，使用原有 UI 图集图标。
- [x] 单人 HUD 恢复紧凑单行分栏，双人恢复独立 P1/P2 徽标、左右操作提示；弹窗和商店遵循同一原稿控件体系。
- [x] 大厅 `AppController.cs` 使用 `miner-p1-states/success` 庆祝精灵，不改变其他游戏卡片。
- [x] Unity UI 检查覆盖两种方向、长屏、商店及各弹层，实际截图逐页人工比对，断言数量不作为视觉一致的证据。

## 2. 重量节奏与经济回归

- [x] 用固定距离计时测试量化旧速度；为轻物、小金、中金、大金和石头建立显著分层。
- [x] 修改 `Core/GoldMiner/GoldMinerRules.cs` 及必要规则调用；同步空钩和力量药水的速度关系。
- [x] 核查 Draining 最大延时、旧存档规则版本及正在回收的快照兼容，保留已有进度和资金。
- [x] 重新生成关卡见证、catalog 校验信息，运行 `scripts/test-gold-miner{,-levels,-catalog,-campaign,-persistence}.sh`，验证 60 秒、两模式 10 关可达目标。

## 3. 绞盘、矿工、绳钩

- [x] 在 `Assets/Editor/GoldMinerMechanicsChecks.cs` 先复现斜绳下钩爪仍朝下、绳筒整图 Z 旋转的问题。
- [x] 新增 `GoldMinerWinchView.cs`：固定支架/轴承/横轴，摇柄绕横轴做透视椭圆运动；利用现有头、身体、上臂、前臂部件，手掌跟随手柄，肩肘联动。
- [x] 新增 `GoldMinerHempRope.cs`：同一线段几何上绘制暗边、暖麻色主体与交错绞股，沿线移动表现收放，不添加物理弯曲影响碰撞。
- [x] `GoldMinerSceneView.cs` 用钩爪顶部挂环作为 pivot，`localEulerAngles.z = hook.AngleRadians * Mathf.Rad2Deg`；收放绳长驱动机械相位，暂停及待机时停止摇柄。
- [x] 验证正中/两侧极限角、双人独立操作、加载中回收、空钩、成功/失败姿态；保存多个机械周期截图。

## 4. 交付验证

- [x] 独立交叉审查规则兼容与机械几何，处理有证据的问题。
- [x] 运行实际 Unity 编译及 Play Mode UI/机械检查，检查所有原稿对应页面，修复发现的裁切和布局问题。
- [x] 使用独立 `com.duanyao.xanaduyonder.goldminertest` 包构建；使用干净构建输出避免旧 Resources 缓存混入。
- [x] 连接设备可用时安装测试包并实际操作；否则使用专属 5560 模拟器，明确未完成的真机体验验证。
- [x] 保存结果到 `docs/verification-gold-miner.md`，交付代码、实际截图、速度数据与 APK；本次没有提交/推送/发布授权，不执行这些操作。
