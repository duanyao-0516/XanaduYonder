# 黄金矿工素材与音效接入方案

日期：2026-10-27。适用 Unity 6000.3.27f1，Android 首版。

**首批 44 张 PNG、37 张 Alpha 素材及 43 个切片已导入；本轮另补 3 张透明 UI 图，当前共 47 张 PNG。** 原 PNG 字节、尺寸与 SHA-256 保持不变；角色、绞盘、绳索、动物、入账和爆炸由真实状态驱动。实际编译、界面、资源与设备验证见[验证记录](verification-gold-miner.md)。

玩法、模式及经济规则以[玩法设计](superpowers/specs/2026-10-27-gold-miner-design.md)为准；页面、生命周期、输入、存档及测试职责见[技术方案](gold-miner-technical.md)。本文件只规定资源、表现与声音，不从 UI 稿中的示例金额反推数值策划。

## 1. 已交付内容与版本化索引

| 类别 | PNG 数 | 内容 | 接入方式 |
| --- | ---: | --- | --- |
| characters | 12 | P1/P2 待机、四种状态、分层部件；小动物三种图；绞盘、部件与开合钩爪 | Sprite 切片；程序状态和变换驱动 |
| objects | 13 | 小中大金块、大小石头、钻石、神秘袋、五道具、金币 | 独立 Sprite；游戏物件与 UI 图标复用 |
| backgrounds | 10 | 横竖屏矿场、模式/商店背景、天空、地表边缘、大厅封面、两种模式插画 | 视图按需加载；背景允许裁切 |
| ui | 6 | 米白面板、三种按钮、胜利徽章、四个 HUD 图标 | 原矩形切片；面板/按钮九宫格 |
| effects | 3 | 收获闪光、尘土、爆炸峰值 | 单贴图配合缩放/透明度，不是逐帧动画 |

另有 11 张完整界面稿和 3 张最初风格参考，共计 58 张 PNG。`screens/` 和 `reference/` 仅供对照，不放进 `Resources` 或正式 APK，也不从整页稿裁取文字、价格、角色。

版本化文档如下，均保留源索引哈希，避免只依赖被 Git 忽略的 `artifacts/`：

- [production-assets-v1.json](gold-miner/production-assets-v1.json)：44 个文件逐项 SHA、源尺寸/字节、Alpha、目标路径、Resources key、导入 profile，以及全部 43 个切片的原坐标。该文件是首批素材的枚举清单；本轮补充素材另由 [production-assets-v2-additions.json](gold-miner/production-assets-v2-additions.json) 严格枚举，不覆盖首批文件。
- [style-guide-v1.json](gold-miner/style-guide-v1.json)：色板、左上光源、角色一致性及文字分离要求的原始快照。
- [ui-state-map-v1.json](gold-miner/ui-state-map-v1.json)：八类界面及可复用素材的设计快照；实际状态转移服从玩法与技术方案。
- [audio-cues-v1.json](gold-miner/audio-cues-v1.json)：首版合成音效参数、事件、开关和生命周期契约。

保持暖色手绘 2D 侧视、深棕轮廓、左上柔和高光。P1 橙服 `#E99A22`，P2 青绿 `#238F8A`；米白 `#F4EBD3`、深绿 `#123C30`。资金和商品价格始终用 `objects/ui_gold_coin.png`；`ore_gold_*` 只代表可抓取金矿。大力药为陶壶，钻石增值剂为带钻石标识的瓶子，石头收藏书无英文文字。

## 2. 原文件保全与可重现接入

当前源目录是 `artifacts/gold-miner-ui-v1/`。交付归档位于 `artifacts/Xanadu-GoldMiner-UI-v1.zip`，内部根目录为 `Xanadu-GoldMiner-UI-v1/`。归档 SHA-256：

```text
609a50c1ade3f111f1fefd783d4c605dfe7f7e5e820698bfc5dbbc1c7eac23f4
```

同目录的 `.zip.sha256` 可校验原包。`artifacts/` 被 `.gitignore` 排除；生产 PNG 及 `.meta` 已落在 `Assets/Resources/GoldMiner/`，应随本次实现一起提交。正常构建直接读取生产文件，不依赖交付 ZIP、生成器缓存或截图目录；导入器仅在生产文件缺失时从原交付包恢复。

在仓库根目录执行以下只读校验，可证明交付 ZIP 与已版本化的 44 项一致：

```bash
python3 - <<'PY'
from pathlib import Path
from zipfile import ZipFile
import hashlib, json
manifest = json.loads(Path('docs/gold-miner/production-assets-v1.json').read_text())
archive = Path(manifest['source_archive']['path'])
assert hashlib.sha256(archive.read_bytes()).hexdigest() == manifest['source_archive']['sha256']
with ZipFile(archive) as package:
    for asset in manifest['assets']:
        raw = package.read(asset['source_archive_member'])
        assert hashlib.sha256(raw).hexdigest() == asset['source_sha256'], asset['id']
        assert len(raw) == asset['source_bytes'], asset['id']
print('verified: 44 production PNG from the delivery archive')
PY
```

`GoldMinerAssetImporter.Prepare` 已实现确定性导入：读取本 JSON → 核对原 PNG → 必要时原字节复制 → 配置 TextureImporter → 复读实际 Sprite、源矩形、锚点和 Android 压缩设置。目标哈希不同即报错，目标一致复用已有 GUID。所有作者切片矩形直接来自清单；Unity 6 为 Single 模式自动生成的一张整图内部矩形单独校验，不误计入 43 个作者切片。

目标路径保持源目录及文件名，例如：

| 源文件（相对源目录） | 目标（相对仓库） | Resources key |
| --- | --- | --- |
| `objects/ore_gold_large.png` | `Assets/Resources/GoldMiner/objects/ore_gold_large.png` | `GoldMiner/objects/ore_gold_large` |
| `characters/hook-open-closed.png` | `Assets/Resources/GoldMiner/characters/hook-open-closed.png` | `GoldMiner/characters/hook-open-closed` |
| `backgrounds/bg-coop.png` | `Assets/Resources/GoldMiner/backgrounds/bg-coop.png` | `GoldMiner/backgrounds/bg-coop` |

全量映射及 SHA 见 JSON，不能仅导入此表的三个示例。Multiple Sprite 用该 key 加载子 Sprite，再按清单 `slices[].name` 建字典；不依赖 `LoadAll` 返回顺序，不使用无路径的全目录预加载。

`Assets/Resources/GoldMiner/Levels/` 已包含独立关卡数据，**不计入本批 44 PNG**。两模式 × 10 关 × 每关 12 个布局（三种图案各 4）共 240 个布局，均已用真实 Core 完成成功见证重放；采用无压缩 UTF-8 JSON，`catalog.json` 索引 20 个分片，每片为一个模式/关卡的 12 张图和见证。规则 v2 的 21 个 JSON 合计 1,210,447 字节。目录上限 64 KiB，每片上限 512 KiB；运行时只解码当前模式/关卡，不在手机上运行 beam 规划。版本、字段、散列及可达性证据见技术方案和验证记录。

## 3. Unity 导入约定

以下参数已通过导入器应用，并复读 Unity 实际结果验证。PPU 统一 100；玩法碰撞以逻辑物件尺寸为准，不从透明留白或纹理像素推导抓取半径。屏幕显示尺寸由布局决定。

| 项 | 首版约定 |
| --- | --- |
| 类型与模式 | Sprite (2D and UI)；清单有切片则 Multiple，否则 Single |
| Filter / Wrap / Aniso | Bilinear / Clamp / 1 |
| 色彩 / Mipmap / Read-Write | sRGB 开；Mipmap 关；Read/Write 关，避免额外 CPU 副本 |
| Alpha | 37 张透明素材使用 Input Texture Alpha、Alpha Is Transparency 开；7 张不透明素材 None/关 |
| 网格 / NPOT | Full Rect；NPOT None；不把物件轮廓转成碰撞体 |
| Physics / Extrude | Generate Fallback Physics Shape 关；Extrude Edges 1 像素 |
| Android | 有 Alpha 为 ETC2_RGBA8，无 Alpha 为 ETC2_RGB4；质量 100，Crunch 关 |
| 图像修改 | 不在源 PNG 上抠图、重画、拼图或改尺寸；平台缩放与压缩由导入设置完成 |

PPU、Full Rect、Alpha 扩边和 Read/Write 的定义见 [Unity 6.3 Sprite 导入说明](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-sprite.html)。本项目 Android 构建使用 OpenGLES3，通用 APK 首版采用 ETC2；所有 OpenGL ES 3.0 GPU 支持 ETC2，而 ASTC 需要验证设备覆盖。[Unity 平台纹理格式说明](https://docs.unity3d.com/cn/2022.3/Manual/class-TextureImporterOverride.html)

ASTC 6×6（Alpha）/8×8（背景）可作为后续经 GPU 覆盖验证的优化方案，不能因为一台测试机支持便默认全设备支持。压缩后必须分别在米白和深绿背景检查描边；透明区 RGB 残留本身不是缺陷，不可以丢弃 Alpha 来验收图片。

| Import profile | Max Size | 使用范围 |
| --- | ---: | --- |
| standalone | 1024 | 独立角色、动物、绞盘整件；13 个对象图标 |
| sheet | 2048 | 多姿态、分层部件、开合钩爪、动物跑动、绞盘部件 |
| background | 2048 | 全屏场景、天空、地表边缘 |
| illustration | 1024 | 大厅封面、单人/双人模式卡插画 |
| ui | 1024 | 面板、按钮、徽章、HUD 图标图集 |
| effect | 512 | 闪光、尘土、爆炸 |

Max Size 是导入上限，不要求放大小图。清单中的切片及 border 坐标始终对应**原文件像素**，不因平台导入后缩小而再次换算写回源坐标；导入后检查实际 Sprite 尺寸与边界。首版可直接用独立纹理；若合批需要 SpriteAtlas，按共同使用的 gameplay/UI 分组，不能把 44 图和所有横竖背景打进一张常驻大图集。加 Atlas 后需重新测内存和包体，不能同时保留重复打包副本。

## 4. 切片、九宫格、枢轴与层级

43 个矩形已经记录 `rect_xywh_top_left` 和 `unity_rect_xywh_bottom_left`。换算关系为 `unityY = sourceHeight - topY - height`。必须逐项使用：钩爪两状态不同宽；绞盘三部件、矿工姿态、六部件和动物四帧都不能均分整张图。HUD 四格恰好规则，但也从清单读取，保持一条导入路径。

枢轴及 border 是本次补充的首轮绑定值，在 JSON 中标为 `planned_*`，并非已经经过 Unity 绑定验收的结果。枢轴归一化、原点左下；border 以单切片的原像素为单位，顺序左/下/右/上：

| 类别 | 首轮枢轴 / border | 接入检查 |
| --- | --- | --- |
| 金矿、石头、钻石、道具、金币、特效 | 中心 `(0.5,0.5)`；border 0 | 游戏半径与 UI 图标尺寸分开；不把透明留白算作碰撞 |
| 矿工整身/状态、动物帧、绞盘整件 | 脚底 `(0.5,0)`；border 0 | 状态切换脚底不跳动；P1/P2 源尺寸相差 1 像素，不按整图中心硬对齐 |
| 钩爪开/合 | 挂环附近 `(0.5,0.85)`；border 0 | 两种状态同挂环位置、同环直径；绳端连到环心，抓点另设子节点 |
| 分层矿工 | 头 `(0.5,0.1)`；身体/腿 `(0.5,0)`；上下臂初始 `(0.5,0.85)` | 首版以姿态切换可完成表现；分层动作按肩/肘/颈接缝校准，不能声称已有骨骼 |
| 绞盘部件 | frame `(0.5,0)`；drum `(0.5,0.5)`；crank 初始 `(0.18,0.5)` | 鼓轮轴心与摇柄孔对齐后旋转，不整体摇晃底座 |
| 按钮三色 | 中心；border `220/100/220/100` | Image.Type=Sliced；文本另层，圆角和上下厚度不拉伸 |
| 米白面板 | 中心；border `80/80/80/80` | Image.Type=Sliced；内容 Rect 内缩，不贴边；不能 Stretch 填满所有比例 |
| 徽章/HUD/插画/背景 | 中心；border 0 | 徽章与图标等比缩放；背景 Cover 裁切，重要交互区域守安全区 |

按钮与面板 border 是原切片上的初始安全边，不是最终屏幕像素留白。实施时需在最窄/最宽按钮和横竖面板中检查角部，将调整后的值写回 manifest 与 `.meta`。点击热区由 RectTransform 决定，不能跟随图片透明像素收缩。

建议 GoldMiner 根节点下使用以下绘制顺序（Canvas sibling 或 SortingOrder 使用同一相对次序，不要求混用两套）：

| 顺序 | 内容 |
| ---: | --- |
| 0 | 当前方向完整背景，或天空 + 地表分层；二选一，避免重复地表 |
| 10 | 静态矿物在前、动物及携钻附着 Sprite 在后绘制；动物经过物件时显示在其上方 |
| 20 | 绳索，使用程序线段，按钩状态实时更新 |
| 30 | 钩上负载和钩爪；互相覆盖按物件挂点定义 |
| 40 | 地表设备与矿工；远臂 → 身体 → 头 → 近臂，设备前后顺序局部调整 |
| 50 | 闪光/尘土/爆炸；仅视觉，不吞输入 |
| 100 | HUD、玩家标识、炸药按钮和入账数字 |
| 200 | 暂停/结算遮罩 |
| 210 | 弹窗面板、文字、按钮 |

地表边缘没有无缝平铺承诺，首版单幅缩放/裁切；不通过重复拼贴制造接缝。小动物携钻使用独立 `ore_diamond` 附着在头背上方，跟随方向/跑动，不另造一个 baked 钻石动物。

## 5. 首版动画节奏

以下均为程序表现方案。计时使用游戏的暂停域；暂停/失焦时停止，恢复后不突跳。特效与入账动效不应阻止下一次合法出钩，动作是否允许由模型状态决定。未实际进行 Unity 动画绑定或真机验收。

| 表现 | 首版驱动与时长 |
| --- | --- |
| 矿工待机 | idle Sprite，呼吸缩放 1.00↔1.015，1.6 秒循环；脚底固定 |
| 摆动/出钩 | operate 姿态；绳长/角度完全来自钩状态，姿态 0.08 秒切换；不人为延迟输入 |
| 收回负载 | effort 姿态；鼓轮/摇柄随实际收绳速度转动；重物可以 0.12 秒周期轻微用力晃动 |
| 收获/本关胜利 | success 姿态保持 0.45 秒，缩放 1→1.06→1；若随后收到新出钩命令立即切回 operate |
| 本关失败 | failure 姿态进入 0.18 秒，随后保持；可用相同部件，不新增逐帧图 |
| 小动物跑动 | 四个切片按 01→02→03→04、8 fps（0.125 秒/帧）循环；统一脚底与身体长度，移动速度来自模型 |
| 小动物静止/被抓 | idle / caught；被抓时停跑、切 caught，跟随钩上挂点；朝向由 X 翻转决定 |
| 钩爪抓取 | open→closed 在有效碰撞后立即切换；收回卸载时反向切换；视觉不先于模型判定 |
| 收获闪光 | 单纹理 0.24 秒，scale 0.6→1.2，alpha 1→0；最多两组，双人各一组 |
| 尘土 | 单纹理 0.30 秒，scale 0.5→1.4，向上漂移 12 逻辑像素、alpha 0.65→0 |
| 爆炸 | 单纹理 0.28 秒，前 0.06 秒 scale 0.35→1，余时 1→1.2 且 alpha 1→0；不是完整爆炸序列 |
| 入账数字 | 0.45 秒，上浮 32 逻辑像素、末 0.2 秒淡出；只展示已提交的金额 |
| 按钮按下/禁用 | 按下 0.08 秒 scale 1→0.97；释放 0.10 秒恢复；禁用 tint/文字提示，不额外绘图 |
| 倒计时警告 | 最后 5 秒整数 tick 脉冲 1→1.08→1 共 0.18 秒；文字变色，读数不闪烁隐藏 |

动物帧身体长短和高低存在差异，首版用独立容器固定逻辑体积，渲染 Sprite 调整到共同身体长度和脚底，不能随每帧自动重算碰撞盒。分层矿工只作为后续细化或摇臂表现的部件来源；首版四个状态姿态已足以实现完整状态反馈。

## 6. 字体与声音

文字复用已有 `Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf`（随仓库已有 OFL 说明），由 `UiFactory.Font` 渲染。`AppController.Awake` 已加载该字体；不再导入字体，不从 UI 稿读取字形。金额、时间、标题、P1/P2、库存、商品说明、禁用原因均为可布局文本。

已有 [GameFeedback](../Assets/Scripts/Runtime/GameFeedback.cs) 的 `Tone` 使用 22050 Hz 单声道、正弦波与平滑包络、`AudioClip.Create/SetData`，并在销毁时释放生成片段；方法是 private，不能直接假设可复用。首版新增独立 **GoldMinerAudio**，复用这套生成方式和公共设置约定，不往 GameFeedback 中加入矿工专属状态，也不依赖外部 MP3。

合成规则：22050 Hz、单声道、采样幅度 0.5；每个音符使用 `sin(π×i/N)` 包络并在末尾补零；滑音逐采样累积相位，频率线性变化；多个音符顺序拼接。AudioSource：2D、`playOnAwake=false`、不循环、volume=0.2；下表 gain 对应 PlayOneShot 的 volumeScale，没有第二套隐含主音量。精确数值在 [audio-cues-v1.json](gold-miner/audio-cues-v1.json)。

| 事件 | 频率 / 合成 | 时长 | gain |
| --- | --- | ---: | ---: |
| 有效出钩 | 正弦滑音 440→660 Hz | 0.08 s | 0.45 |
| 金块/袋金币/普通动物入账 | 660、880、1175 Hz，每音 0.05 s | 0.15 s | 0.60 |
| 钻石/携钻动物入账 | 880、1175、1568 Hz，每音 0.06 s，替代普通收获声 | 0.18 s | 0.60 |
| 石头入账 | 正弦滑音 180→100 Hz | 0.10 s | 0.35 |
| 炸药成功使用 | 90→45 Hz 正弦占 55% + 固定种子噪声占 45% | 0.20 s | 0.60 |
| 最后 5 秒 | 880 Hz；进入 5/4/3/2/1 秒各一次 | 0.06 s | 0.40 |
| 胜利 | 523.25、659.25、783.99、1046.5 Hz，每音 0.09 s | 0.36 s | 0.65 |
| 失败 | 392、293.66、196 Hz，每音 0.11 s | 0.33 s | 0.50 |
| 购买提交成功 | 660、880 Hz，每音 0.06 s | 0.12 s | 0.45 |
| 不可操作提示 | 120 Hz，最多每 0.2 秒一次 | 0.05 s | 0.25 |

只在语义事件提交后播放一次，不逐帧响、不因重复触摸响；双人最多 4 个同时发声实例。饱和时优先保留胜败、爆炸、倒计时，丢弃低优先级旧声；只丢声音，不能丢掉模型事件。按本幅度/主音量约束保证混音不削波，验收还要检查设备听感。

声音读取共用 `data.SoundEnabled`，通过注入 getter 与显式设置变更通知连接。现有设置关闭声音时只调用 `feedback.Stop()`；实施时必须同时通知活动的 GoldMinerAudio.StopAll，不能仅在下次播放时再读取布尔值。暂停、失焦、退出页面也 StopAll；恢复后不补播旧事件；退出时销毁生成的 AudioClip 和 AudioSource。开关声音不修改触感设置。

用户原素材中的 12 个 MP3 保留在 `artifacts/gold-miner-research/assets/Audio/` 作后续备选；本轮没有试听，本版不导入，不将其视为开发阻塞。首版没有背景音乐或持续绞盘音轨。后续若决定选用 MP3，再做逐个听感/内容检查与导入清单更新；这不增加本轮授权确认步骤。

## 7. 内存、包体与验收

当前可重复测量：44 张原 PNG 共 **51,711,510 bytes（49.32 MiB）**；若全部按原分辨率 RGBA32 同时驻留且无 mipmap，理论像素内存为 **276,789,864 bytes（263.97 MiB）**。这不是 Unity 实测内存，也不是 APK 增量；不能原样全载入后声称轻量。

首版按视图保留必要集合：大厅只保留封面；选择页加载模式背景/两插画；单人不载 P2 和横屏背景；合作不载竖屏背景；进入商店释放矿场专属集合，复用 UI/道具；退出游戏释放 GoldMiner 缓存及声音。背景只存在当前模式版本；Levels 数据只保留当前模式/关卡解码结果。释放顺序必须先销毁引用组件/缓存，再在页面切换边界回收资源，不每帧调用卸载。

以下是**目标值，不是测量结果或已通过承诺**：

| 指标 | 首版目标 | 测量方式 |
| --- | --- | --- |
| 当前 GoldMiner 页面纹理驻留 | ≤ 48 MiB；页面切换短时峰值 ≤ 64 MiB | Android Development Build，记录实际 Texture2D Runtime Memory Size，按 GoldMiner 路径去重合计 |
| 合成音频与缓存 | ≤ 1 MiB | 生成 clip 的 samples×channels×4 及 AudioSource 数量；Profiler 核对释放 |
| 当前关卡解码数据 | ≤ 4 MiB | 只加载当前模式/关卡；Profiler 统计 JSON 字符串、解析对象与布局缓存，退出释放 |
| APK 增量（含后续 Levels 数据） | ≤ 40 MiB | 同 Unity、签名/构建配置与基础提交的 APK 字节差；BuildReport 分离 GoldMiner 纹理及代码贡献 |
| 重复进入退出 | 20 次后无单调增长 | 同一设备、相同页面循环；每轮回到大厅稳定后对比纹理与 clip 实例数 |
| 视觉流畅性 | 60 fps 为目标；需实机验证 | 双人并发出钩、动物、两次特效及结算同时运行时抓 Profiler；报告设备和帧分布 |

预算超出时先检查重复资源/旧引用，再降低非关键 Max Size 或调整按页加载；在确认 GPU 兼容的分发配置中再评估 ASTC。不以牺牲文字可读性或删除必要 UI 状态满足预算。

2026-10-27 实测：同基线、同配置的两份干净 Development APK 相差 **34.32 MiB**，已满足 40 MiB 包体目标。记录包含 APK 文件及 ZIP 有效条目差异，不是安装后占用或 BuildReport 逐纹理归因；物理设备的其他驻留及帧时间目标仍待测，详见[验证记录](verification-gold-miner.md)。

实施验收：

1. 清单 44 个目标文件/源 SHA 对应一致，37 个 Alpha 保持；43 个切片坐标、名称、border、枢轴与 `.meta` 复读一致；首次导入及重复导入 GUID 稳定。
2. 不规则图集无截断或串帧；角色换姿态、动物四帧、钩爪开合不跳轴；米白/深绿底上没有实心背景块、明显压缩色边；九宫格角部不变形。
3. 横竖屏、刘海/导航安全区、暂停/返回/商店/结算均按技术方案验收；大厅卡片固定高度并滚动，不压缩卡片。
4. 所有音效只在对应模型/提示事件时触发；关闭声音立即静音；暂停/失焦/退出无残留，恢复无补播；双人并发不爆音，失败购买不播放成功声。
5. 输出实际导入设置、资源内存/包体测量、运行截图与真机交互记录。PNG 校验成功不能代替 Unity 导入通过，编辑器运行也不能代替手机触控验证。

实施状态：原 ZIP/44 个生产文件哈希与字节、43 个切片、37 个 Alpha、索引及 JSON 已核对；Unity 导入器、`.meta`、动画绑定、声音生成和 Android 构建已完成。Unity 布局、20 次进出资源采样及 Android 模拟器交互已有实测记录；物理手机的纹理/音频/关卡数据驻留、声音触感、帧时间与人工体验仍待验收。各层证据与测量边界见[验证记录](verification-gold-miner.md)，不把编辑器内存视为手机预算达标。

## 2026-10-28 原稿还原补充

新增三张独立透明 PNG：关卡预告竖拇指半身矿工、失败矿工与空白木牌、手绘“黄金矿工”标题。由内建 imagegen 依据批准的 09/08/01 预览重绘，原始输出直接导入；木牌上的动态标题仍由 UGUI 绘制。补充清单记录 SHA-256、原尺寸和字节，严格限制两个 characters ID 与一个 ui ID，Single、Max Size 1024、ETC2_RGBA8，关闭 Read/Write 与 mipmap。首批 44 PNG 原字节和 43 切片保留；当前合计 47 PNG，3 张补充图共 3,947,797 字节。

机械动画使用原有 rig 部件与 crank 图：侧视鼓轮外形/支架固定，摇柄围绕三维横轴，肩肘两段运动使手掌跟随握柄；木握柄通过原图 UV 片段保持侧视厚度。麻绳使用专用 UGUI 网格绘制暗边、暖麻色和绞股；长绳与引绳依据实际收放距离同速移动，钩爪沿绳角度旋转。最终运行截图、机械回归和设备证据见验证记录末节。


## 2026-10-28 完整抓持图补充

根据用户对运行时拼接效果的反馈，新增九种完整抓钩与负载组合：小/中/大金块、小/大石头、钻石、袋子、普通鼹鼠、抱钻鼹鼠；另有一张无钩的抱钻行走动物。当前合计 **57 张生产 PNG**。全部使用内置 image_gen 独立绘制，原始 1254×1254 RGBA 字节直接保留；最终提示词、源哈希、尺寸、吊环坐标与导入目标见 `gold-miner/production-assets-v3-caught.json`。图像没有经程序裁切或重新合成。

抓到物品后，SceneView 隐藏独立 Ore Image，将 Hook Image 替换为该类完整图。按每图吊环外径缩放、按环心设置 pivot；空钩与负重钩保持相同吊环显示直径、连接绳端并共同沿绳角度旋转。小物在爪内，大物由爪尖夹住上肩，前后遮挡由整图负责。负载插画按统一钩尺度呈现，不再把物品额外缩放到碰撞圆直径；碰撞、价值、重量和存档坐标仍由 Core 管理。

携钻鼹鼠的钻石由双前爪抱住，行走与抓持都只有一张完整图；删除独立悬浮钻石 Image。行走采用完整图轻微起伏和倾斜，普通动物仍使用原四帧。新十图采用 Single、Max Size 512、ETC2 RGBA8、关闭 mipmap/ReadWrite，按视图懒加载。炸药特效以此次命令前的真实快照计算负载中心，避免同步输入已抓住物品、尚未渲染时使用旧位置。

本轮 **不修改矿工手臂/转机动画**。此前的肩肘/握柄坐标测试只能证明机械点关系，不能证明袖子视觉连续；用户指出的断臂效果仍保留为待讨论项。建议下一步采用同一母版六帧完整矿工+手臂+摇柄+转机循环，固定底座、轴心与出绳点，绳钩负载独立；本轮没有制作或接入这些动画帧。

最终v3独立干净APK相对基线增量37.195 MiB，满足40 MiB目标；实际导入/Android验证及边界见验证记录第10节。


## 完整矿工六帧动画（v4，2026-10-28）

在用户明确授权“绘制并接入”后，新增橙色 P1 与青色 P2 两张 1536×1024 原生透明图集，每张包含六个完整矿工、手臂、握柄和绞盘姿势。P2 从选定 P1 图编辑服装配色，使用内置 imagegen，原始 PNG 字节保留；生成提示、来源、哈希、透明度、切片和出绳点见 `gold-miner/production-assets-v4-reel.json`。总生产素材为 59 张 PNG，多图集切片总数 55。

`GoldMinerWinchView` 删除旧分段手臂与独立摇柄几何，改为完整帧切换。短绳末端通过逐帧 pivot 对齐同一位置，再由 8 个设计单位的麻绳 lead 接入既有主绳。轻物/空钩约 0.60 秒一圈，最重物约 0.95 秒一圈；每人以真实收线位移独立推进，停止与暂停时停帧。已有绳钩和九类完整抓持图继续使用。

这是六帧卡通动画；手柄姿势不是严格匀角速度采样。原图头顶与轮廓存在约 1–2 源像素差异，正常游戏缩小后约半像素。脚底和轴心元数据是统一注册参考，不能替代逐帧视觉检查。当前实施与验证结论以 `verification-gold-miner.md` 的 v4 节为准；上述 v2/v3 手臂说明保留作为历史记录。
