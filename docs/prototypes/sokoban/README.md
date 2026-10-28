# 推箱子布局原型

用于评审「标准页内大棋盘 + 可选专注模式」，不是 Unity 游戏页面。完整方案见 [设计稿](../../superpowers/specs/2026-10-28-sokoban-design.md)。

从仓库根目录运行：

```bash
python3 -m http.server 8765 --bind 127.0.0.1
```

打开 <http://127.0.0.1:8765/docs/prototypes/sokoban/>。所有图片使用本地 `ThirdParty/Sokoban/`，不需要 CDN 或在线服务。正式应用仍使用 Unity/C#，没有引入 WebView 或 Node 运行时。

- 桌面同时显示两种布局，手机宽度下通过标签切换。
- 紧凑演示为原创 8×8，高地图为原创 8×13；它们用于比较比例，不代表已完成正式 8 关教学设计。
- 两种布局共享一份当前盘面。方向盘、方向键或 WASD 移动；支持单箱推动、撤回、重来、选关和说明/设置弹层。
- 不持久化，不播放音频，不做触感或真实 Android 生命周期处理。设置弹层明确说明演示边界。原型允许通关后撤回以便审阅，正式版本按设计稿的结果页流程实现。
- 本原型未实现滑动手势；方案中的一次滑动一步属于后续 Unity 实现。

## 已执行验证（2026-10-28）

1. `node --check docs/prototypes/sokoban/prototype.js` 通过。
2. 浏览器实际打开，图片全部加载，无损坏图片；推动后两侧均显示 1 步 / 1 推 / 1 箱到位，另一侧撤回后同时归零。
3. 原型第一关实际按 `UDDLLDRR` 回放，显示 8 步 / 3 推 / 2 箱到位的通关状态。
4. 1280 宽浏览器、390×844 模拟手机画框下，方形地图两侧都是 340×340 px；高地图标准页约 214×348 px、专注页约 287×466 px。测量来自渲染 DOM，截图见下。
5. 浏览器视口切到 390×844，标准/专注切换可用，无水平溢出（scrollWidth = viewportWidth = 390），可见手机画框内容无内部溢出；检查后恢复默认视口。
6. 新工作树基础 `scripts/test-core.sh` 为 43 passed / 0 failed。关卡源池结构和 100 个解法见证独立验证见 [内容报告](../../../Data/Sokoban/verification/README.md)。

没有执行 Unity 编译、UGUI 测试、APK 构建或 Android 真机回归。原型尺寸使用 CSS px，不能据此宣称达到了生产 Android 48dp 触控标准。

## 预览

![近方形地图的两种布局](preview-compact.png)

![高地图的两种布局](preview-tall.png)
