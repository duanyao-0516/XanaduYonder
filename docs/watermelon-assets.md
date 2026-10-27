# 合成大西瓜素材来源与资源契约

## 来源与授权

- 来源仓库：[idootop/watermelon](https://github.com/idootop/watermelon)。
- 固定版本：[`8af7d8892c0218206a579d4b5717facab2d26d6e`](https://github.com/idootop/watermelon/tree/8af7d8892c0218206a579d4b5717facab2d26d6e)。
- 素材导入日期：2026-10-27。
- 用户已明确确认取得素材授权，用于本项目游戏开发及分发。本记录保留该授权确认及公开来源，不将来源仓库的代码许可证等同于素材授权。
- 来源仓库代码采用 AGPL-3.0，README 说明部分图片、音效来自微伞及互联网。这里只导入下表列出的图片和音效，未复用其 Dart/Flutter 源码；本项目的游戏逻辑采用原创 C# 实现。
- PNG 和 MP3 均逐字节复制，没有裁剪、缩放、重新编码或移除原图留白。SHA-256 与固定版本的源文件一致。

## 运行时资源契约

所有文件位于 `Assets/Resources/Watermelon/`。使用 Unity `Resources.Load<Texture2D>` 或 `Resources.Load<AudioClip>`，路径不含扩展名。

| 主题 / 音效 | Resources 路径 | 源文件顺序 / 用途 |
|---|---|---|
| 水果 | `Watermelon/Fruit/1` … `Watermelon/Fruit/11` | `assets/images/friuts/1.png` … `11.png`；`friuts` 为源仓库原有拼写 |
| 表情 | `Watermelon/Emoji/1` … `Watermelon/Emoji/11` | `assets/images/qq/1.png` … `11.png` |
| 校徽 | `Watermelon/Campus/1` … `Watermelon/Campus/11` | `assets/images/985/1.png` … `11.png` |
| 落地 | `Watermelon/Audio/Drop` | `fall.mp3`，约 0.64 秒 |
| 合并 | `Watermelon/Audio/Merge` | `boom2.mp3`，约 0.139 秒 |
| 达成目标 | `Watermelon/Audio/Win` | `cheer.mp3`，约 2.927 秒 |
| 失败 | `Watermelon/Audio/Lose` | `boom1.mp3`，约 1.20 秒 |

主题等级沿用源仓库 `lib/game/level/levels_inner.dart` 中的数字顺序，三套素材均为 11 级。校徽依次为：中国海洋大学、湖南大学、山东大学、华东师范大学、电子科技大学、四川大学、复旦大学、上海交通大学、浙江大学、北京大学、清华大学。此顺序仅表达游戏素材等级，不代表学校排名。

校徽原图四角含白色底色，应在游戏渲染层按圆形显示；源图片本身保持不变。表情图含外围装饰，玩法碰撞形状使用游戏定义的圆形，不依据贴图的透明区域自动生成碰撞体。主题切换只替换外观，不改变同等级物体的物理尺寸。

## Unity 导入设置

- PNG：普通 `Texture2D` 用于 UI，sRGB，保留输入 alpha、启用 `alphaIsTransparency`，无 mipmap，不可读，最大尺寸 1024，双线性过滤，Clamp，无 NPOT 缩放；默认平台采用无压缩纹理以保留小图与校徽文字边缘。
- MP3：AudioClip，短音效解压后播放，预加载，强制单声道，2D 音效，Vorbis 导入质量 0.8。
- 为资源根目录、四个子目录、33 张 PNG 和 4 个 MP3 提供独立 GUID 的 `.meta`。没有引用源项目的 Unity 或脚本资产。

## 文件校验清单

下表目标路径相对于 `Assets/Resources/Watermelon/`，源路径相对于上述固定版本仓库。尺寸是 PNG 原始像素尺寸，字节数是原文件大小，不包含 `.meta`，不代表 Unity 导入后的内存或安装包大小。

| 源路径 | 目标路径 | 尺寸 / 格式 | 字节数 | SHA-256 |
|---|---|---:|---:|---|
| `assets/images/friuts/1.png` | `Fruit/1.png` | 52×52 | 1274 | `b2c705e34d5c521aa5457b9b0f9dd54935a1f5fc795318650b8db2b94ddc755b` |
| `assets/images/friuts/2.png` | `Fruit/2.png` | 80×80 | 3289 | `0d26442337ffd5b8d5371fb6bf89635dac5dde8e54f304908ed548cad531fecc` |
| `assets/images/friuts/3.png` | `Fruit/3.png` | 108×108 | 3397 | `71f6d23db404d6f703e2ab8c26f5587e2c8fb2f36179718ec9d240e285357682` |
| `assets/images/friuts/4.png` | `Fruit/4.png` | 119×119 | 5616 | `08f98b309d061f9fc3662ccc99348c9a955203c1dc1665f6d79db1ba97c9b995` |
| `assets/images/friuts/5.png` | `Fruit/5.png` | 153×152 | 10624 | `a739dcd6444359f1bf8c40792ab633521da722dce34950ef05cf12b7b8c38ee3` |
| `assets/images/friuts/6.png` | `Fruit/6.png` | 183×183 | 11072 | `e1774d12e03429f0f6deb2a573c0b83ecd221f3cd7adda0dbe0d22964643d88f` |
| `assets/images/friuts/7.png` | `Fruit/7.png` | 193×193 | 8202 | `c244ac0abc8487536364a430b2b105ff83bcd87d0efb7ab7907b15891ae3b51b` |
| `assets/images/friuts/8.png` | `Fruit/8.png` | 258×258 | 20325 | `7ba5577a59a366f7539e402bf76d9354daa8dac4d4ac6ad61896ec6275fe9663` |
| `assets/images/friuts/9.png` | `Fruit/9.png` | 308×308 | 16267 | `6703e1947780d77724e11e0618970481b295b00cbb0a2592f5256d7d3f216ca4` |
| `assets/images/friuts/10.png` | `Fruit/10.png` | 308×309 | 26159 | `c081461986bc1d4742c06fbb3bd167b602bc77394923cc089eb5bb1210ca9e3c` |
| `assets/images/friuts/11.png` | `Fruit/11.png` | 408×408 | 24931 | `67c252f9864feaa01f9b13cde3ebaa801a7d1f3b051f155d6a3dde09efa983d8` |
| `assets/images/qq/1.png` | `Emoji/1.png` | 224×224 | 39225 | `18f5d3fa87353f13d31fa9edb2360334cc8282b454c0b375a6a9a6764be18828` |
| `assets/images/qq/2.png` | `Emoji/2.png` | 224×224 | 41433 | `fdcaf35070c06dbe16341a352eccdde1f78f99be6b17d3457f93845f9d753392` |
| `assets/images/qq/3.png` | `Emoji/3.png` | 224×224 | 36956 | `aa849c95796956f86074de2e9289861b835deed4e0165382d59d812716e0397e` |
| `assets/images/qq/4.png` | `Emoji/4.png` | 224×224 | 48844 | `b0dc112e67958fff290cee0bfadc103bd2e7481345db31af97c3ded4b6d7041c` |
| `assets/images/qq/5.png` | `Emoji/5.png` | 224×224 | 43887 | `ad5ba3f061454e0585eb18bf9a943f48ab5833d3c1bff5738876b2554bac21b6` |
| `assets/images/qq/6.png` | `Emoji/6.png` | 224×224 | 46431 | `4b177c307c3a032350c2c7451e78db65428dc2ff036da1bd82826bcce38914b6` |
| `assets/images/qq/7.png` | `Emoji/7.png` | 224×224 | 41434 | `ac426282f998f912adfbcf8bb0eb781be9f463bc3d28f7ebd70b7a6a78860f57` |
| `assets/images/qq/8.png` | `Emoji/8.png` | 224×224 | 37481 | `ed5154f6fd1426ac51549d1ffd09d282a5693a80e9e513b316133fc3390de834` |
| `assets/images/qq/9.png` | `Emoji/9.png` | 224×224 | 52176 | `8988a84ad053e46e09d352fd9006c81b0625672ef380b7ac6a0cfe43692e546a` |
| `assets/images/qq/10.png` | `Emoji/10.png` | 224×224 | 55322 | `f90d8878c7498a2070a1210815b447b514038b548fb1eeb0ceb60fafce1ef8c7` |
| `assets/images/qq/11.png` | `Emoji/11.png` | 224×224 | 47764 | `68de64bda4eed7a82bc8e8e1c6c1c898e022d531aa0e6dc0d5c6c67e0b77569d` |
| `assets/images/985/1.png` | `Campus/1.png` | 416×414 | 234816 | `d858f8bb9569ec49e4efd21525d6b60f3b81611b342f84a2f3d0b0a8d5caf217` |
| `assets/images/985/2.png` | `Campus/2.png` | 451×449 | 302195 | `8e53c54411dab170e5b64cb24b75006050b4bc3ec8c407eb5e1ec6734ba8c249` |
| `assets/images/985/3.png` | `Campus/3.png` | 423×424 | 254944 | `24308a31eb2fb72285146dc300d3361069b465646e5b04e596000f2b22a1029d` |
| `assets/images/985/4.png` | `Campus/4.png` | 450×449 | 326850 | `7eaa6f253f6292874cb15c0d44daafe06d03addf9dbc785d9ce671a0b9eba4c5` |
| `assets/images/985/5.png` | `Campus/5.png` | 340×340 | 151727 | `4ed0f98ae7687c31b50d2f5ccae8c94f99c2a6dc1977c6db4737b99fc9249163` |
| `assets/images/985/6.png` | `Campus/6.png` | 842×836 | 108608 | `1cd1bdbee3793b9e22dc9db217541361874f6300d0b8712d11458819cf390fd1` |
| `assets/images/985/7.png` | `Campus/7.png` | 450×451 | 306553 | `c5704b5c0e2a85bd5342693cf8776825bb38f64642a891404754d59a95e3a1ff` |
| `assets/images/985/8.png` | `Campus/8.png` | 417×423 | 284795 | `06dc80a86259cb5458ec8d7d62eb4b1ce9a6b783d5f90c8f20a688d4b80c907e` |
| `assets/images/985/9.png` | `Campus/9.png` | 452×450 | 273749 | `d782be666d94e18afeabffa2396f91559939f7c1430511b9a860928887457b8d` |
| `assets/images/985/10.png` | `Campus/10.png` | 450×449 | 297367 | `8b45ffece57bffdf29c5ceed493f5190f63cb4e6b5835a2b965750352dcd9343` |
| `assets/images/985/11.png` | `Campus/11.png` | 451×450 | 377810 | `d21b34fbb01e3d69f262c6b2d4c612b7899e5868a237ebf3b82eb64995b94af0` |
| `assets/audio/fall.mp3` | `Audio/Drop.mp3` | MP3 | 11002 | `620a4dd8b2505893301ad5dae4f794e3c0d416495b72f2795a5266364c6115eb` |
| `assets/audio/boom2.mp3` | `Audio/Merge.mp3` | MP3 | 6313 | `b5a1c49762349d5e4f15c1311f2ddbdfcf36a7f7a8dbe49c7c2100ff5a2ce196` |
| `assets/audio/cheer.mp3` | `Audio/Win.mp3` | MP3 | 48109 | `a26445580ecbc0b5ea445f59ba11954dcdfeecb68074c553fdbc2cc105d9fa32` |
| `assets/audio/boom1.mp3` | `Audio/Lose.mp3` | MP3 | 38587 | `330120b8ac4616439e1c0f6192db70460b0c2edc1f80e297653b160f8e42cfc8` |

导入核验：33 张 PNG（3,541,523 字节）及 4 个 MP3（104,011 字节），共 **3,645,534 字节**，约 **3.48 MiB**。全部 37 项源文件与目标文件 SHA-256 相同。此步骤只完成静态素材与导入配置核验；Unity 导入、运行和设备呈现由游戏集成验证覆盖。
