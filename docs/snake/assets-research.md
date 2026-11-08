# 贪吃蛇玩法与素材调研台账

核验日期：2026-10-28。本台账记录玩法研究与素材候选留档；资源尚未导入 Unity `Assets/`。本项目另行设计的 12 关草案见 [关卡说明](levels-notes.md)，游戏逻辑尚未实现。

建议正式画面采用项目自绘的蛇头、直身、弯身和尾巴，保证四向移动、转弯和穿边界时的连接一致。下载的 Snake Sprites 2D 适合作为圆形分节蛇的备选，不能宣称它已经提供独立的直身、弯身和尾巴。音效优先评估已下载且包内许可清晰的 Kenney Interface Sounds。

## 玩法依据与项目选择

[Visnalize 的 Snake II 开发者文档](https://visnalize.com/brick1100/games/snake)说明其作品自动前进、不能倒退、可以从一侧离开并在另一侧出现，撞到自身或迷宫墙会结束；方向键支持四向移动，速度可选择。Classic 是吃食物直至撞击的无限挑战；Campaign 按食物目标通过迷宫，并在循环后加快速度。这是现代复刻作品的第一方产品说明，不作为 Nokia 原版说明书，也不作为地图或图像的再分发许可。

本项目将四边穿越 `wrap` 作为经典默认规则，`solid` 为可选撞墙规则。20 × 24 的空棋盘共有 480 格，初始长度 4，每份食物增长 1 格，因此吃到第 476 份食物时长度达到 480，应结算“满盘完成”，不能继续抽取食物或无限循环寻找空格。障碍模式以 `480 - 障碍格数` 作为可占用上限；先判定满盘，再生成下一份食物。这些数字与终局策略是本项目的设计约定，不是外部素材包或上述作品提供的规则数据。

## 来源与下载状态

| 候选 | 作者与来源页 | 页面可证实的内容、许可证 | 本次状态与用途 |
| --- | --- | --- | --- |
| Snake Sprites 2D | BdRGames / Bas de Reuver，[作者发布页](https://opengameart.org/content/snake-sprites-2d) | CC0；蛇头、圆形蛇节、苹果、墙块及其他物件；个人和商业项目可用，署名可选 | **已下载、解压、核对说明、清单、尺寸和 SHA-256，并查看预览图**；候选圆形分节蛇皮肤 |
| Interface Sounds 1.0 | Kenney，[作者发布页](https://opengameart.org/content/interface-sounds) | CC0；100 个独立 OGG，页面说明音量已归一；包含点击、确认等界面音效 | **已下载、解压、核对包内 License.txt、清单和 SHA-256**；6 个初选音效已完整解码 |
| UI Audio | Kenney，[官网页](https://kenney.nl/assets/ui-audio) | 官网列 50 个音效；CC0；button / switch / click | 页面已核实；官网压缩包请求返回 HTTP 403，**未下载**；不与 Interface Sounds 混称 |
| Sokoban | Kenney，[官网页](https://kenney.nl/assets/sokoban) | 100 个文件，2D tile / puzzle / top-down / character；CC0 | 页面已核实；下载请求返回 HTTP 403，**未解包**；仅列为棋盘、角色视觉的候选，不声称具有 Snake 关卡 |
| Tiny Dungeon | Kenney，[官方 itch 页面](https://kenney-assets.itch.io/tiny-dungeon) | 130+ sprites，地牢 tiles、武器、道具、角色，独立图与 tilesheet；CC0；明确包含 Tiled sample map | **仅页面核实，未下载**；可以参考障碍地图表现；示例地图不等于可直接玩的 Snake 关卡 |
| UI Pack | Kenney，[官网页](https://kenney.nl/assets/ui-pack) | 官网列 430 个文件，按钮、面板、滑条；CC0 | **仅页面核实，未下载**；项目已有统一菜单风格时优先沿用现有 UI |

上表的 CC0 标识来源于作者发布页；已下载的 Kenney 包还含明确的许可文件。[CC0 官方说明](https://creativecommons.org/publicdomain/zero/1.0/)允许复制、修改和再分发，包括商业用途。这里没有使用 GitHub 仓库的代码许可证推定素材许可。

本次没有核实到 Kenney 专门的 Snake 素材包，也没有找到可以直接纳入本项目、许可清晰且已验证可玩的统一 Snake 障碍关卡库。Tiny Dungeon 确实提供 Tiled 示例地图，但还需要按蛇长、出生点、食物规则、可达性和满盘条件重新设计、转换并验证。计划中的关卡应明确标记为项目自制，不能冒称已下载的现成关卡。

## 已下载文件与完整性

原包、解压内容和机器可读清单只保存在仓库忽略的 `artifacts/snake-research/` 下，不作为游戏运行资源提交。

| 包 | 下载入口 | 原包字节数 | ZIP SHA-256 |
| --- | --- | ---: | --- |
| Snake Sprites 2D | [snakesprites.zip](https://opengameart.org/sites/default/files/snakesprites.zip) | 756205 | `c7b67965d09c4a781a7a351e287c9896ea245ecfa28258bdcfa909fc44c8e134` |
| Kenney Interface Sounds | [kenney_interfaceSounds.zip](https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip) | 834536 | `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232` |

各包目录的 `source.zip` 保留原始压缩字节，`extracted/` 保留原始文件。`manifest.json` 记录每个文件的相对路径、字节数和 SHA-256；蛇图包额外记录每张 PNG 的宽高。`download-status.json` 记录 UI Audio 的 403 失败及已完成下载的包名。

Snake Sprites 2D 共 62 个文件：`png/` 下 60 张精灵、1 张 800 × 480 预览图、1 份 `readme.txt`。尺寸包括 19 张 32 × 32、19 张 64 × 64；其余为较大的原图，详见清单，不能把所有文件都称为 32 像素图块。`readme.txt` 原样保留，SHA-256 为 `8075f8e9b77c4adc9ad8d39eea7dadc72a90cc784cc53e81f18bff731ba03730`。它说明个人和商业项目可用、署名可选，但未附单独完整 CC0 文本；CC0 标识来自作者发布页，并记录于 `license-provenance.txt`。

| 需求 | 已下载的代表文件 | 尺寸 | 覆盖情况 |
| --- | --- | --- | --- |
| 蛇头 | `png/snake_green_head_64.png` | 64 × 64 | 有绿、黄两色；方向可由渲染旋转处理，包内没有四套方向文件 |
| 蛇身 | `png/snake_green_blob_64.png` | 64 × 64 | 圆形分节身体；没有单独直身与弯身 |
| 尾巴 | 无独立文件 | — | **缺少专用尾巴**；不能计为完整连体蛇素材 |
| 食物 | `png/apple_red_64.png` | 64 × 64 | 有红、绿及其他苹果变体 |
| 障碍墙 | `png/wall_block_64_0.png` 至 `png/wall_block_64_6.png` | 64 × 64 | 有墙块图，可按格组装；没有可玩的 Snake 地图数据 |

Kenney Interface Sounds 共 103 个文件：100 个 OGG、`License.txt` 和 2 个作者网址快捷文件。`License.txt` 明确写出 CC0，个人、教育和商业项目可用，署名非强制；其 SHA-256 为 `f7966c773bbed0eca6a9c75081c44a178b38eae112724dbb5fdfbd4192d118a9`。网址快捷文件只保留作为原包内容，没有执行。

## 6 个音效候选

以下按文件语义和事件用途初选，文件均来自已下载的 Interface Sounds；不是下载失败的 UI Audio。它们均为 44.1 kHz Vorbis，`ffprobe` 读取成功、`ffmpeg` 全段解码通过。尚未试听、调整音量或在 Android 上听感验收，因此“适合该事件”仍是候选判断。

| 事件 | 包内文件 | 时长 | 声道 | 建议用途 |
| --- | --- | ---: | ---: | --- |
| `ui_click` | `Audio/click_001.ogg` | 0.100 s | 1 | 普通按钮 |
| `ui_select` | `Audio/select_001.ogg` | 0.043 s | 2 | 选择模式或关卡 |
| `eat_food` | `Audio/pluck_001.ogg` | 0.102 s | 2 | 吃到食物 |
| `game_over` | `Audio/error_001.ogg` | 0.165 s | 2 | 撞身或撞障碍结束 |
| `level_clear` | `Audio/confirmation_002.ogg` | 0.539 s | 1 | 通关或满盘完成 |
| `pause_toggle` | `Audio/toggle_001.ogg` | 0.139 s | 1 | 暂停或继续 |

逐文件 SHA-256、字节数、解码结果和未试听标记保存在 `kenney-interface-sounds/selection.json`。移动每一格不播放音效，避免高速时连续噪声；正式接入时统一受游戏音效开关控制。当前只是资源与事件映射方案。

## 尚未完成的验证

- 自绘蛇头、直身、弯身和尾巴尚未制作；穿越四边时的视觉衔接尚未在 Unity 实现或验证。
- 外部关卡数据尚未引入；满盘可达性、障碍布局与出生点需要项目关卡验证。
- 候选图像尚未进行 Unity 导入、Sprite Atlas、缩放、色彩空间或 Android 实机检查。
- 候选音效已通过文件解码验证，尚未试听、混音或进行设备播放验证。

上述状态仅说明素材研究的验证层级，不代表游戏已实现。
