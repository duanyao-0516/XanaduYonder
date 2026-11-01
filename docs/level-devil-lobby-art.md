# 只有一道门大厅小图标

原创单门图案由门框、圆点门把手和地线组成，沿用大厅 `24×24` viewBox、`#276653`、2 单位线宽、圆头与圆角规范。没有使用游戏原始像素门图或 AI 栅格生成。

- SVG：`docs/design/lobby-v2/additional-icons/svg/level-devil.svg`
- 设计 PNG：`docs/design/lobby-v2/additional-icons/png/level-devil.png`
- Unity PNG：`Assets/Resources/Lobby/Icons/level-devil.png`

使用本机已有 sharp `0.35.5`，按 density `288` 读取 SVG，输出 `96×96`、sRGB、RGBA8 透明 PNG。两份 PNG 字节一致，均为 802 字节；SHA-256 为 `9af0e671afc091d4e2cd1cab3566d85a9281ad7e5ac398f6c8971e111c047257`。SVG SHA-256 为 `1c09bcc876e291a65af403123edd20b31fa5db30237648ecc5efe52f0bc4854e`。

Unity 保留原 GUID `46dd4ce5702d4e149ae1f77e856eed78` 和 Sprite 内部 ID，导入设置与相邻大厅图标一致：Bilinear、Full Rect、无 mipmap、无压缩、无 fallback physics shape，Sprite 矩形更新为 `96×96`。

已检查 PNG 尺寸、透明通道、实心描边 RGB `(39,102,83)` 和两份文件一致性；将图标以 Bilinear 缩至 20px，与王国守卫战、战争时代和飞机图标并排目视，门框及把手均清晰，线宽和颜色一致。

大 Logo 使用用户于 2026-11-01 提供的黄色游戏原图，原样保存于 `Assets/Resources/LevelDevil/Logo.png`，72×72，SHA-256 `1c5a296e47ca21413919f479df696b5373fd34f769c6f5e87c65d9d786a06b58`。大厅展示保持正方形，不裁切。
