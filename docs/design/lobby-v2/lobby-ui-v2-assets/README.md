# Lobby UI v2 图标资产

本目录仅供 UI 预览和后续 Unity 导入，**尚未接入 App**。未修改项目代码或现有资源。

- 共 23 组独立 SVG / PNG（17 款游戏、1 个兜底手柄、5 个公共控件）。其中 21 组来自固定版本 lucide-static@1.17.0（20 个不同的官方图标）；成语接龙和通用兜底共用 1 个按用户规格原创的 Switch / Joy-Con 式手柄。
- 原始来源与 SHA-256、游戏到图标的映射见 assets.json；source/ 保留官方原始 SVG、原创手柄 SVG 和 package.json。LICENSE 保留 Lucide 完整许可文本（含原包附带的第三方说明），仅适用于官方 Lucide 资产；原创手柄明确标为 custom，不冒称官方原图。
- svg/：21 枚官方图标仅把 currentColor 改为 #276653，并保留全部原始路径；2 枚手柄为原创 SVG。全套统一为 24×24 viewBox、#276653、2 单位线宽、圆头和圆角。
- png/：由同一 SVG 导出为 96×96、8 位 RGBA、sRGB 透明 PNG（24 单位图标的 4 倍分辨率）。没有底色、阴影或文字。
- 成语接龙与通用兜底采用原创 Switch / Joy-Con 式横向手柄：圆角外框、左右控制区分隔、中央留空、左摇杆和右侧按键，没有品牌或文字；跳一跳与 Dino 仍共用官方 footprints。

## Unity 建议导入参数

导入 png/ 中的图片：Texture Type = Sprite (2D and UI)，Sprite Mode = Single，Mesh Type = Full Rect，Pivot = Center (0.5, 0.5)，Pixels Per Unit = 100。启用 sRGB、Alpha Source = Input Texture Alpha、Alpha Is Transparency；关闭 Mip Maps、Read/Write。Wrap Mode = Clamp，Filter Mode = Bilinear，Compression = None，Max Size = 128，Non Power of 2 = None，保留原图 96×96 尺寸。

UI Image 的 Color 保持白色即可呈现已写入资源的 #276653；按 24×24 逻辑尺寸展示并保持宽高比。若后续需要动态改色，应从保留的 source/ 原图另导出统一的白色 Alpha 图，不对当前墨绿色图叠加不等价的色调。

SVG 母版留作编辑与再次导出；当前交付不依赖 Unity SVG 扩展包。

## 导出验证

所有 23 张 PNG 已解码验证为 96×96、4 通道 RGBA8，均含透明像素、有效图形和抗锯齿半透明像素；逐图像素计数写在 assets.json。21 枚官方 SVG 校验仅修改根节点 stroke、原始路径保持一致；2 枚手柄与 source/custom-switch-controller.svg 完全一致。替换手柄后重新校验其余 21 枚 SVG / PNG 的 SHA-256 均未改变。

自定义排序功能已从本轮计划移除，故未打包排序箭头图标。
