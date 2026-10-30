# 大厅三种视图实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Execute the user-approved design without another approval gate.

**Goal:** 按已确认的奶油墨绿 UI 实现双列、目录和单列大厅，并提供两个在研游戏的小图标。

**Architecture:** AppController 继续拥有游戏入口、实时纪录与存档错误语义；独立 LobbyView 负责原生 UGUI 布局和展示切换。图标使用已评审 PNG，不重画。展示偏好独立保存，游戏存档协议不变。

**Tech Stack:** Unity 6000.3.27f1、UGUI、现有 NotoSansCJK 字体、96px RGBA 图标。

## 已批准的边界

- 基准为本任务 `lobby-ui-refined.html` 与 `lobby-ui-v2-tokens.json` 的最新版本；将批准稿归档到仓库文档附件。390 设计单位等比映射当前 720 画布，奶油底 #F6F4EC、Ink #173D35、Accent #276653、辅助字 #5D7468。
- 双列：图标 20、名称 14，首行 20 高并做 1 单位光学下移；内边距 10、最小高 66、圆角 14、间距 10；仅一行真实摘要。
- 目录：图标 20、名称 13、内边距 10、最小高 48、圆角 12，侧边距 16。
- 单列：只显示大 Logo，标题 24，最小高 143；顶部“原编号 / 分类 / 原玩法标签”。缺大 Logo 的成语保留纯文字；小图标缺失使用导出的 Switch 式手柄。
- 固定分类、组内固定顺序，无自定义排序。左侧静态“全部游戏 · 17 款”，右侧切换三种展示；顶部工具行上距 9、下距 12、下内边距 7。
- 默认双列；记住用户选择。三种模式均显示全部现有 17 款，预览节选和示例成绩不进入产品。
- 王国守卫战／皇家守卫军、战争进化史仅提供图标及接入说明，不创建假入口，不修改其他工作树。
- 保留已验证启动交接、大西瓜手势规则及原 17 个 Enter 回调。

## 文件与接口

新增 `Assets/Scripts/Runtime/Lobby/LobbyView.cs`、`LobbyEntry.cs`、`LobbyDisplayPreferences.cs`。

```csharp
enum LobbyDisplayMode { Double = 0, Directory = 1, Single = 2 }
// Entry: Id, ObjectName, Name, Number, Category, Tagline, Description,
//        Record, CompactRecord, Action Enter, Action<RectTransform> BuildLogo.
// LobbyView.Initialize(entries, openSettings, initialMode, modeChanged)
// LobbyView.Scroll / StatusLabel / Mode / HasDisplayOptions
// LobbyView.SetRecord(id, detailed, compact)
// LobbyView.SetMode(mode) / CloseDisplayOptions()
```

`AppController.BuildLobby` 组装实际入口与静态元数据；各 Refresh 方法调用 SetRecord，防止切换后持有失效 Text。大 Logo 绘制复用现有素材/程序图形，并按实际预览包围盒居中放入单列插槽。

`LobbyDisplayPreferences` 使用独立 PlayerPrefs key，范围校验后默认双列。只在用户切换时写入；测试保留并恢复该键，不动游戏存档。

新增 `Assets/Resources/Lobby/Icons`，PNG 原样导入 Sprite、无压缩、无 Mipmap、透明、Clamp/Bilinear。SVG、LICENSE、映射和批准稿放入 `docs/design/lobby-v2`，避免源文件进入游戏 Resources。

## 实施与验证

- [x] 先新增缺失展示入口的回归检查，运行取得 RED；主线程统一运行 Unity，其他代理先在 artifacts 草拟。
- [x] 导入已确认图标，追加 castle（kingdom-rush）与 swords（age-of-war）同版线稿，明确王国守卫战别名映射。
- [x] 实现纯展示组件、三模式布局、选择浮层、遮挡与退出清理；背景不能接受点击或键盘 Submit。
- [x] 接入宿主真实记录、原入口、Back、启动就绪和展示偏好；删除旧大厅重复构建代码，保留业务初始化链。
- [x] 通过真实输入模块验证轻点、抖动、拖动取消、切换模式后点击，以及固定分类/完整目录/长标题/空或坏偏好。
- [x] 渲染 720×1320、720×1582 的三模式截图，与批准稿的内容几何/配色/图标逐项核对。保留游戏本身的独立检查，不让布局旧断言阻塞其入口验证。
- [x] 独立审查需求符合性及生命周期/资源/记录正确性，修复后复核。
- [x] 构建独立启动测试包；安装时保留测试存档，记录构建、签名、包信息和本轮验证范围，不把浏览器示意等同于真机验证。

UI 实施阶段仅交付测试包；用户随后授权汇总提交并推送远端 master。交付前同步最新 master 并验证整合结果，不修改其他开发工作树。

最终证据见 [原生实现与验证](../../design/lobby-v2/implementation-validation.md)。
