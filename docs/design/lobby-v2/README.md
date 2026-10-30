# 大厅 v2 设计归档与图标接入

本目录保留 2026-10-30 批准的奶油墨绿大厅设计，并说明 Unity 小图标资源的接入。原设计交付时有 17 个入口，另为王国守卫战（皇家守卫军）和战争进化史准备图标。当前源码已接入第 18 个战争进化史系列入口，包含一代与二代；王国守卫战仍仅预留图标。

## 设计基准与文件

- `lobby-ui-refined.html`：最新批准交互稿，按原文件逐字节归档；可直接用浏览器打开，图像内嵌在 HTML 中。
- `lobby-ui-v2-tokens.json`：批准的颜色、尺寸、布局和固定分类规则，原样保留。
- `lobby-ui-v2-readme.md`：批准包原说明，原样保留。其中 `preview.html`、`design-tokens.json`、`icons/`、`large-logos/` 是原压缩包中的名称；本目录依次使用上述 HTML、JSON、`lobby-ui-v2-assets/`、`lobby-ui-v2-media/`。原稿“尚未接入 App”描述的是设计交付当时的状态。
- `lobby-ui-v2-assets/`：23 组批准图标的完整原包，包含官方源 SVG、着色 SVG、PNG、来源映射、README、包信息与 LICENSE。
- `lobby-ui-v2-media/`：批准稿使用的大西瓜、Zuma、欢乐球球大 Logo，原样归档。
- `additional-icons/`：本轮为两个在研游戏准备的原 SVG、着色 SVG 和 PNG。
- `asset-map.json`：25 张 PNG 的统一来源、别名、Resources 路径、SHA-256、像素验证与 Unity 导入约定。
- `new-game-icons-preview.png`：两个新图标的 96px 原尺寸和 20px UI 尺寸对照。

批准 HTML 中的游戏节选和示例纪录用于评审文字密度；运行时使用当前全部 18 个入口和真实纪录；战争进化史不保存完整战场，展示“一代 / 二代”而非虚构纪录。

## Unity 接入

运行资源位于 `Assets/Resources/Lobby/Icons/`。只将 PNG 及 `.meta` 放入该目录；SVG、设计稿、映射、LICENSE 和说明留在本设计目录，无需安装 Unity SVG 扩展。

文件名就是图标 ID。使用不含扩展名的 `Resources.Load<Sprite>("Lobby/Icons/<id>")` 加载。PNG 已内置 `#276653`，`Image.color` 保持白色，启用 `preserveAspect`。缺少小图标时加载 `Lobby/Icons/fallback-controller`。

```csharp
Sprite icon = Resources.Load<Sprite>("Lobby/Icons/kingdom-rush");
if (icon == null)
    icon = Resources.Load<Sprite>("Lobby/Icons/fallback-controller");
image.sprite = icon;
image.color = Color.white;
image.preserveAspect = true;
```

双列和目录使用 20×20 设计单位的小图标，设计宽度基准为 390；当前 720 画布按 `720 / 390` 等比映射。双列图标在 20 高的标题行中下移 1 设计单位，标题为 14；目录标题为 13。单列标题不显示小图标，只复用已有大 Logo；成语接龙保留纯文字。

所有 25 张图均为 96×96、8 位 RGBA、sRGB 透明 PNG。`.meta` 约定：Sprite / Single、Full Rect、中心 Pivot (0.5, 0.5)、100 Pixels Per Unit、sRGB、Input Texture Alpha、Alpha Is Transparency；关闭 Mip Maps 和 Read/Write；Clamp、Bilinear、无压缩、Max Size 128、Non Power of 2 = None。导入时保留 96×96 原尺寸。

## 两个补充图标

| 游戏 | ID 与 Resources 路径 | 官方图形 | 别名 |
| --- | --- | --- | --- |
| 王国守卫战 | `kingdom-rush` → `Lobby/Icons/kingdom-rush` | castle | 皇家守卫军、Kingdom Rush |
| 战争进化史 | `age-of-war` → `Lobby/Icons/age-of-war` | swords | Age of War |

王国守卫战与皇家守卫军映射同一张 castle，不复制成两个独立资产。两个图标使用固定版本 [Lucide castle](https://unpkg.com/lucide-static@1.17.0/icons/castle.svg) 与 [Lucide swords](https://unpkg.com/lucide-static@1.17.0/icons/swords.svg)，只把根节点 `stroke="currentColor"` 替换为 `#276653`；24×24 viewBox、2 单位线宽、圆角、圆头和全部官方路径均保持不变。使用 bundled Node 中的 sharp 将 SVG 按 density 288 导出为 96×96 RGBA 透明 PNG。

后续游戏接入只需使用上述稳定 ID；小图标已经是完整 Sprite 资源，无须再从旧程序图标绘制函数提取或重画，也不依赖旧版图标代码。是否新增入口、玩法或大 Logo 属于后续游戏实现，本批资源不承担这些行为。

## 数量、许可与验证边界

25 张运行 PNG = 17 款原有游戏 + 2 款补充游戏图标 + 1 个通用兜底 + 5 个公共控件。公共控件为 settings、sliders-horizontal、chevron-down、check、x。原批准包的 23 张 PNG 全部逐字节复制，原 SHA-256 不变；完整哈希见 `asset-map.json`。

其中 23 组导出来自 Lucide（22 个不同官方源 SVG），成语接龙与 fallback-controller 共用 1 个原创 Switch / Joy-Con 式手柄，共 2 组导出。原批准 `assets.json` 的两处描述误写为 22 组 Lucide；其实际资产数组和 counts 为 21。原文件保留以便审计，统一映射记录新增两个图标后的准确数量。

[完整 LICENSE](lobby-ui-v2-assets/LICENSE) 保留 Lucide 的 ISC 许可证及原包附带的 Feather MIT 说明，适用于 Lucide 资产；原创手柄的来源和适用范围在统一映射中单独标为 custom，不冒称官方图形。

本次资源验证覆盖：批准稿和原图 SHA-256、25 张 PNG 的解码尺寸／RGBA8／sRGB／透明与抗锯齿像素、新图标与官方源 SVG 的几何一致性、导入元数据和 Resources 目录内容。Unity 实际导入、程序集、UI 交互和真机表现由大厅主任务统一验证。
