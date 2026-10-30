# 王国保卫战开始页菜单

本目录实现用户要求的“玩法说明”和“设置”右上角按钮，仅挂在实际 `screen_slots` 开始页面。玩法说明使用纸色滚动弹层，正文可直接上下拖动，也支持鼠标滚轮；返回键先关闭弹层。设置按钮打开原有 `options_view`，音乐、音效的即时调整与点击“完成”保存均复用原代码。

不增加 Unity 启动页面，不改战斗、资源加载、存档、原设置和跨 Activity 设置契约。所有图标用矢量线条绘制，字体直接复用稳定包 `all-desktop/assets/fonts/HarmonyOS_Sans_SC.ttf`，不新增或替换字体。原字体版权信息仍保留在原字体内：Copyright 2021 Huawei Device Co., Ltd. All Rights Reserved.

## 仅两处内容差异

1. `all-desktop/screen_slots.lua`：在最后的 `return screen` 前插入 `require("xanadu_start_menu").attach(screen)`。
2. 新增 `all-desktop/xanadu_start_menu.lua`，来自本目录同名文件。

其余 2423 个稳定基线文件必须保持字节一致；不删除任何文件。`content-menu-patch.py` 在应用前校验完整稳定清单；重复执行只能接受与仓库模板一致的菜单。验证接口 `validate_patched_screen` 会移除唯一 hook 并核对原 screen SHA，同时验证插入位置；`expected_menu_files` 返回新增文件的精确字节。

## 验证

```sh
python3 scripts/kingdom-rush/test-content-menu-patch.py --staging artifacts/kingdom-rush-1/staging
python3 scripts/kingdom-rush/test-content-menu-behavior.py --pythonlib artifacts/kingdom-rush-1/final-prune/code/pythonlib
python3 scripts/kingdom-rush/test-content-menu-font.py --staging artifacts/kingdom-rush-1/staging --pythonlib artifacts/kingdom-rush-1/menu-tools/pythonlib
```

上面的 `staging` 路径适用于实际构建后的菜单版本，也接受原始可玩基线。第一项始终先复制到临时目录：如果输入已经包含菜单，必须先通过完整菜单白名单校验，再仅在临时副本移除唯一 hook 和新增模块，恢复并验证原始基线后执行测试。测试覆盖完整文件边界、幂等、拒绝修改战斗文件、拒绝额外资源和过期菜单，结束时再次核对调用方输入未被修改。第二项执行实际菜单 Lua，记录图形与原开始页边界，覆盖手势、返回、关闭触点、设置复用、四种宽高比及页面销毁重入；不声称替代手机视觉验收。第三项检查帮助用字在原字体 cmap 中全部存在，同时验证字体保持稳定清单的原 SHA。

菜单在开始页 init 后创建，destroy 前释放字体引用；屏幕尺寸改变时重建自身布局，保留并限制滚动位置。弹层打开期间消费全部菜单鼠标按下/释放以及键盘事件；关闭动作的释放事件也不传给原开始页。LÖVE 的 Android 触摸鼠标事件沿用原开始页已使用的输入链；正文拖动通过同一指针的位置更新。
