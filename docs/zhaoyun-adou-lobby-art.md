# 赵云与阿斗大厅图标

2026-11-01，按用户最终指定替换大厅资源。

- 单列大 Logo 使用用户附件原文件，逐字节复制到 `Assets/Resources/ZhaoyunAdou/Logo.png`；72×72，SHA-256 `3b29b1cf674b1ef55d08330f996690f3204492d1f50c575c8662defe079fd8b2`。不裁剪、不改色，按 96×96 设计单位显示。
- 双列与目录使用原创“长枪与护盾”线条图标，表示布阵与守护阿斗。沿用大厅的 24×24 viewBox、`#276653`、2 单位线宽、圆头圆角、透明背景，无文字和底色。
- SVG 源位于 `docs/design/lobby-v2/additional-icons/svg/zhaoyun-adou.svg`，SHA-256 `aabea7da593346eb207525e90b96b740e3743c28262e0880580ee64f1b27e855`；使用本机已有 sharp 按 density 288 导出 96×96 RGBA/sRGB PNG，归档与运行副本字节相同。PNG SHA-256 `c3c23d9897023230af60ba0f696c7562aa7b9206831df2a674ec06d405759a52`。
- 保留原资源 GUID；小图标沿用 Sprite/Single、Full Rect、100 PPU、Clamp/Bilinear、无压缩/无 mipmaps、不缩放 NPOT 的大厅导入规则。

按用户要求，本次替换之后未运行测试或 Unity/Android 编译。此前预览 APK 不包含这次大厅美术调整和最新 master 的其它游戏合并。
