# Dino 氛围与节奏修订实施计划

> **For agentic workers:** Use subagent-driven-development in this isolated worktree; only the root worker runs Unity, builds APKs or operates the OPPO.

**Goal:** 恢复黑白昼夜，在指定节日与生日展示彩蛋，并让加速更早可感知。

**Architecture:** 运动与精确预测留在 Core；离线日期策略为不依赖 Unity 的 Runtime helper；TrackView 只负责颜色和帽子，Page 负责进入日期与生日横幅。公共 UI、存档协议不变。

**Tech Stack:** Unity UGUI / C# / ChineseLunisolarCalendar / Android IL2CPP。

1. `DinoRules.cs` 与 `Tests/Dino/Program.cs`：先验证旧实现不满足 10 秒 240、30 秒 360，再把每秒加速度改为 6，同步 Travel 等差求和。运行 `scripts/test-dino.sh`，覆盖实际宽度 608/1.2 与 1403/1.2。
2. 新建 `DinoCelebrationCalendar.cs`、`Tests/DinoPresentation/` 与 `scripts/test-dino-presentation.sh`：纯日期输入返回庆祝类型；覆盖公历窗口、春节、指定生日、闰月及支持范围外。
3. `DinoTrackView.cs`：颜色改白／黑，以 `(game.Tick / 1800) % 2` 为目标昼夜，保留 0.8 秒混合与暂停冻结；新建 `DinoCelebrationHat.cs` 使用 MaskableGraphic 绘制像素帽，并按图集的站立／蹲下头顶放置。
4. `DinoPage.cs`：通过 `Func<DateTime>` 的本地日期入口采样，保留本次进入的庆祝类型；选择页生日横幅不拦截点击，不改变跑道布局；重建时把同一类型交给 TrackView。
5. `DinoVisualChecks.cs` 与 `DinoPageValidation.cs`：用实际 Game.Step 和障碍推荐输入驱动 30 秒／60 秒／90 秒周期，验证渐变、暂停、重开、材质缓存及像素；注入固定日期验证生日和节庆帽、跳跃／下蹲／死亡／重排。
6. 根任务串行执行纯 C#、Unity 页面及渲染检查，检查截图并处理独立审查问题；运行 `scripts/build-dino.sh`，检查 APK 身份后 `adb -s 3B6F4NE8A66ZXTRL install -r`。更新验证记录、提交本工作树修改。
