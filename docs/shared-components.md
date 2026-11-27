# 新玩法接入与共用组件

新增游戏时，入口、菜单、系统返回和写盘分别接到下表的实现。游戏页面负责何时暂停、哪一步改变对局，以及返回后是否需要继续。这里列出的组件均有现成调用方，可以对照源码接入。

| 需要复用的行为 | 接入点 | 可参照的实际用法 |
| --- | --- | --- |
| 一份入口支持三种大厅布局 | 在[大厅目录](../Assets/Scripts/Runtime/Lobby/AppController.Lobby.cs)配置稳定 ID、分类、纪录和进入回调，由 [LobbyView](../Assets/Scripts/Runtime/Lobby/LobbyView.cs)显示 | 2048 启动时恢复对局，用 `SetRecord` 更新纪录。抓大鹅在首次进入时创建页面 |
| 大厅、说明、设置和返回按钮 | [UiFactory.NavigationButton](../Assets/Scripts/Runtime/UiFactory.cs) 提供图标按钮，[GameNavigationLayout](../Assets/Scripts/Runtime/Navigation/GameNavigationLayout.cs) 计算位置 | [推箱子顶栏](../Assets/Scripts/Runtime/Sokoban/SokobanPage.Layout.cs)只复用位置，沿用页面的点击热区 |
| 对话框、设置子页和底部面板 | [宿主弹窗](../Assets/Scripts/Runtime/App/AppController.Dialogs.cs)提供居中框和底部面板。自有画布复用 [DialogUi.ModalOverlay](../Assets/Scripts/Runtime/Navigation/DialogUi.cs) 管理蒙层和焦点 | [抓大鹅设置](../Assets/Scripts/Runtime/App/AppController.BakeryBasket.cs)从分类进入子页。[声音与触感说明](../Assets/Scripts/Runtime/App/AppController.FeedbackHelp.cs)是全屏二级页，固定返回入口、独立滚动正文 |
| 面板形状与安全区 | [DialogCorners](../Assets/Scripts/Runtime/Navigation/DialogCorners.cs)提供面板、按钮与开关圆角。[DialogSheetLayout.Attach](../Assets/Scripts/Runtime/Navigation/DialogSheetLayout.cs)将贴底外观与安全内容区分开布局 | [大厅展示方式](../Assets/Scripts/Runtime/Lobby/LobbyView.cs)使用自己的 390 宽坐标，与 720 宽宿主共用圆角比例和贴底规则 |
| 主次按钮、布尔与互斥选择 | [DialogUi](../Assets/Scripts/Runtime/Navigation/DialogUi.cs) 提供 `ActionButton`、`Disclosure`、`SwitchRow`、`FeedbackSwitches` 和 `ScrollText`，短互斥项用 [DialogChoices.Create](../Assets/Scripts/Runtime/Navigation/DialogChoices.cs) | `FeedbackSwitches` 将共用声音与触感并排，每个格子各自响应整格点击并即时写入。带「保存」与「取消」的设置先更新本页草稿 |
| 结算后查看原局面 | [ResultPresentation](../Assets/Scripts/Runtime/ResultPresentation.cs) 管理等待、回看与重新打开，页面提供局面身份和可展示条件 | [五子棋](../Assets/Scripts/Runtime/Gomoku/GomokuPage.Dialogs.cs)使用自己的结算动作。先冻结并保存结果，再允许回看 |
| 声音、触感与基础图形 | 复用 [GameFeedback](../Assets/Scripts/Runtime/GameFeedback.cs)、[PlatformHaptics](../Assets/Scripts/Runtime/Platform/PlatformHaptics.cs) 和 [UiFactory](../Assets/Scripts/Runtime/UiFactory.cs) 的字体、圆片及圆角精灵 | 2048 合并与 Dino 碰撞各自在自己的事件上触发反馈。原作人物、图集和许可随玩法管理 |
| 前后台与系统返回 | 通过[生命周期](../Assets/Scripts/Runtime/App/AppController.Lifecycle.cs)和[系统返回](../Assets/Scripts/Runtime/App/AppController.SystemBack.cs)接入宿主 | 成语、飞花令区分中文输入法造成的短时失焦与应用进入后台。横屏游戏在自己的退出回调恢复方向 |
| 校验后原子写盘 | 兼容相同备份规则的存储类调用 [AtomicSaveFile.Write](../Assets/Scripts/Runtime/AtomicSaveFile.cs) | [宿主](../Assets/Scripts/Runtime/LocalSaveStore.cs)、[跳一跳](../Assets/Scripts/Runtime/Jump/JumpSaveStore.cs)、[合成大西瓜](../Assets/Scripts/Runtime/Watermelon/WatermelonSaveStore.cs)和[欢乐球球](../Assets/Scripts/Runtime/HappyBall/HappyBallSaveStore.cs)已接入 |
| 离线网页菜单 | 在适配器的阻断层使用 [dialogs.css](../ThirdParty/WebGameUI/dialogs.css)。按钮使用 `.xy-primary`、`.xy-secondary`，开关提供可读名称和当前状态 | [只有一道门](../ThirdParty/LevelDevil/adapter/menu.js)在正在写入时禁用开关，并用宿主返回的状态刷新。各游戏继续通过自己的 WebView 协议暂停和恢复 |

## 示例一：设置里的数量显示

抓大鹅把「数量显示」「3D 主题」「声音与触感」放在设置首页。玩家点进数量显示后，有两个独立开关和一个返回动作。下面摘自同一入口，坐标沿用面板的设计单位。

```csharp
var panel = CreateSheet("数量显示", 408, OpenBakerySettings);
float width = panel.rect.width - 64;
DialogUi.SwitchRow(panel, "篮中剩余数量", 32, 190, width, 72,
    bakeryPreferences.ShowRemaining, value =>
    {
        bakeryPreferences.ShowRemaining = value;
        SaveBakeryDisplay();
    });
DialogUi.ActionButton(panel, "返回设置", 32, 308, width, 72, OpenBakerySettings, true);
SetModalBack(OpenBakerySettings);
```

`CreateSheet` 返回安全内容区，标题、开关和按钮都挂在这个节点下。父节点负责绘制贯穿所属画布底边的面板底色，用 `DialogCorners.ApplyTop` 处理上方两角。自行创建底部面板的页面把背景和内容节点传给 `DialogSheetLayout.Attach`，由它按实际画布和系统手势区留出底部空间。各类控件的半径和真机边缘检查见[弹窗规范](dialogs.md#圆角与屏幕边缘)。

上例中面板外点击、返回按钮与系统返回都回到设置首页。玩法有自己的画布时，先取消当前连续输入，再调用 `DialogUi.ModalOverlay`。组件负责蒙层与背景按钮焦点，页面负责时钟、草稿、对局与关闭回调。

## 示例二：保存一次操作

先由业务编解码器校验并生成二进制存档数据，再调用 `AtomicSaveFile.Write(path, encoded, canBackUp)`。`canBackUp` 使用该玩法自己的读取规则，回答当前主档能否成为备份。组件在同一进程内串行完成临时文件写入、刷新和替换。第一份有效存档成为主档，下一次成功写入能保留上一份有效主档。损坏主档被替换时，最近的有效备份得以保留。[宿主写入与回读](../Assets/Scripts/Runtime/LocalSaveStore.cs)

2048 一次有效移动或共用偏好改动只写宿主档。离开或进入后台时，宿主再通知已初始化的玩法保存。各模块保留各自的文件名、版本、坏档恢复、错误提示和重试入口。飞花令另外管理诗词收藏，网页反馈偏好在宿主写盘成功后才确认收到。[各玩法存档契约](architecture.md#玩法模块与存储) · [网页偏好交接](../Assets/Scripts/Runtime/App/AppController.WebGamePreferences.cs)

## 如何验证接入

| 改动 | 可复跑的检查 | 核对的结果 |
| --- | --- | --- |
| 入口、顶栏或返回 | `python3 scripts/test.py lobby-design minigame-ui` | 三种大厅视图使用同一 ID，关闭弹窗后焦点和操作权回到来源 |
| 贴底与手势区 | [面板几何检查](../Assets/Editor/DialogSheetGeometryChecks.cs)，随 `minigame-ui` 执行 | 验证带底部手势区、零底部 inset、横屏侧边挖孔，以及缩放旋转后的独立画布。真机另查屏幕两下角的实际显示和底部按钮触点 |
| 共用存盘 | `python3 scripts/test.py persistence jump-persistence watermelon-persistence happy-ball-persistence host-save-isolation` | 校验失败、备份恢复、写盘失败后的重试以及一次滑动对其他存档的隔离 |
| 棋盘上的弹窗 | `python3 scripts/test.py classic-boards-ui snake-ui tetris-ui` | 蒙层拦截对被盖住控件的点击和确认输入。俄罗斯方块的准备页仍可使用放在面板前方的顶栏，游戏恢复后可继续操作 |
| 网页资源和原生桥 | `python3 scripts/test.py --group platform` | 打包输入、离线清单和宿主协议。菜单视觉和手势还需在相应网页或设备上运行 |
| 文档索引和示例路径 | `python3 scripts/test.py doc-links` | 仓内链接、图片及标题定位能从当前 checkout 打开 |

全部后台套件、平台构建和实机检查的边界见[开发指南](development.md#按模块选择验证)和[构建指南](building.md)。
