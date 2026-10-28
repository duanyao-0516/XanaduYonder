# Tetris 布局契约检查

运行 `scripts/test-tetris-layout.sh`，使用生产 `TetrisLayout.cs`，不依赖 Unity。

覆盖两组原型尺寸（388×784 → 237×474 棋盘；388×688 → 194×388 棋盘），以及 5 种宽度、4 种等比例安全高度、有无保存警告的 40 组组合。324 项断言检查完整 10×20 正方形网格、四周间距、Hold 热区和 Next 预览范围、警告与操作区分隔、五个动作最小宽度及底部间距。

这些是布局数值检查。48 CSS 像素对应的设计单位不等于所有设备的 48 dp；实际 UGUI 射线、字体、导航覆盖层与设备触摸仍需在生产页面验证。

本轮输出：`PASS Tetris layout: 324 assertions, 40 responsive cases + 2 prototype sizes.`
