# v0.3.0 Logo 边距修订

2026-10-26，Unity 6000.3.27f1，Android ARM64。游戏规则和存档协议未修改；此前玩法测试见[旸了个旸验证记录](verification-sheep-refinement.md)。

## 显示尺寸

原始 `Assets/Branding/XY_logo.png` 与用户 Downloads 中的文件逐字节一致。问题发生在显示环节：Android 自适应图标只展示 108 单位图层的中央 72 单位，原图作为整个前景时会放大可见图案。现在两个自适应图标 XML 均在前景四边内缩 1/6，保留原图内的白边比例；Legacy/Round 位图不改变。

Unity 原生 Splash 已关闭。Bootstrap 场景序列化引用原始纹理，`BrandSplash` 使用完整图片居中显示，宽度为屏幕短边的 0.5，保持原始长宽比；白底显示 1.75 秒后淡出 0.25 秒。Android 系统启动窗口继续使用白底和修正后的应用图标。

## 验证范围

- 开发与正式包构建成功，正式签名和包元数据验证通过，仍为 0.3.0 / code 3、minSdk 26、targetSdk 36、ARM64、非 debuggable。发布证书沿用原密钥。
- Android 模拟器应用信息页同一 138 像素图标框内，鸟形宽度由约 119 像素缩为 80 像素，约 58%，与原图约 58% 一致。证据在本地 `artifacts/logo-padding/emulator-app-icon.png`。
- 手机与模拟器覆盖安装、冷启动和 Home 返回后的六份游戏存档均与本轮安装前的 SHA-256 一致。
- 启动画面的大小由用户在测试手机上确认可接受。自动抓帧覆盖了系统图标与页面过渡，未作为完整自有启动页稳态尺寸的验证证据；按用户要求停止进一步排查，移除临时诊断后保留其接受的实现。
- 独立静态审查已覆盖布局计算、指针遮挡、清理、场景引用和 Android 资源。未重新运行与本轮显示修改无关的游戏规则测试。

## 当前安装包

| 文件 | 字节数 | SHA-256 |
|---|---:|---|
| `artifacts/builds/XanaduYonder-0.3.0.apk` | 58,839,927 | `ddddb65f4e0cfe6804586484f116b68de0899798fbcee81876030a643d15cb6a` |
| `artifacts/builds/XanaduYonder-0.3.0-android-arm64.apk` | 38,079,817 | `077628dc31243d4b4d94d2c922669560e49d1e14f2fffa4ad1d84f529349d9d7` |

图标机制参考 [Android AdaptiveIconDrawable](https://developer.android.com/reference/android/graphics/drawable/AdaptiveIconDrawable) 与 [InsetDrawable](https://developer.android.com/reference/android/graphics/drawable/InsetDrawable)。
