# Aircraft Fire Implementation Plan

> For agentic workers: use subagent-driven-development; tasks have separate file ownership and share one managed worktree.

**Goal:** 在最新 master 上完整接入 Unity/C# 飞机大战，提供全宽战场与三种大厅视图。

**Architecture:** 独立纯 C# 模型、独立 Runtime 页面和资源目录；现有 AppController 只加入 shell 契约。原包 ARM native 代码只用于分析，不装载到新应用。

**Tech Stack:** Unity 6000.3.27f1、C#、UGUI、Newtonsoft JSON、.NET 10 独立测试。

- [x] 拉取 origin/master，创建 managed worktree 和功能分支，读取工程约定。
- [x] 分析 APK 图集、native 符号、模式和交互，保存出处和复现提取脚本。
- [x] Core：Assets/Scripts/Core/AircraftFire/；先运行真实模型测试，再实现固定步战斗、生成、计分、道具、两模式与难度、暂停和复活。
- [x] Runtime：Assets/Scripts/Runtime/AircraftFire/；导入独立资源，完成全宽 UGUI 战场、单指拖动、逐帧动画、音效、HUD、暂停/帮助/结算及独立存档。
- [x] Shell：Assets/Scripts/Runtime/AppController.AircraftFire.cs 和必要 AppController.cs hooks；懒加载页面、三视图入口、大 Logo 和小图标、保存和前后台生命周期。
- [x] 复用既有构建依赖，Unity 编译并生成 ARM64 开发 APK；测试新模块及受影响大厅，不跑全仓长期测试。
- [x] 独立源码交叉审查，修复实际问题；记录验证结果、参考来源与用户真机验收事项。

## 入口菜单修订（用户后续要求，待验证）

- [ ] 从大厅进入原游戏入口菜单，不创建对局；菜单内选择经典普通／疯狂或七关闯关后开始实战。
- [ ] Home／Book／Settings 仅在入口显示；说明和设置关闭、后台恢复均保持入口，菜单 Home／返回才退出至大厅。
- [ ] 实战保留原素材暂停按钮，移除入口导航与新增仪表栏；暂停和结算的“主菜单”返回游戏入口。
- [ ] 调整生产 UGUI 检查并补充三尺寸入口截图，保留输入、暂停、终局和复活覆盖；必要 Unity 检查由主代理执行，结果另行记录。
