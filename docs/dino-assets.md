# 小恐龙素材来源与导入记录

素材来源为 Chromium 固定修订 `64574fa0f2daa1b48f2703a072687272000178ce`，本地原始文件及来源清单位于 `docs/superpowers/specs/assets/chrome-dino/`。该目录仍保留设计参考原件；运行时副本位于独立的 `Assets/Resources/Dino/`。此次仅复制下列图像、音频及许可，没有复制 Chromium 游戏 JavaScript。

完整许可来自该修订的 [Chromium LICENSE](https://chromium.googlesource.com/chromium/src/+/64574fa0f2daa1b48f2703a072687272000178ce/LICENSE)，原始字节复制到 `Assets/Resources/Dino/LICENSE.txt`，Unity 可通过 `Resources.Load<TextAsset>("Dino/LICENSE")` 读取并展示。

## 文件映射与校验

2026-10-27 对以下来源文件及导入副本逐一核对 SHA-256 和字节数，均与原始 `source-manifest.json` 一致。音频仅改扩展名和资源名称，未转码；其 `.mp3` 原始扩展名与实际 Ogg 容器不符。

| 原始文件 | 运行时副本（相对 `Assets/Resources/Dino/`） | 字节数 | SHA-256 |
| --- | --- | ---: | --- |
| `200-offline-sprite.png` | `Sprites.png` | 4758 | `e4222715b556e7d99622c83e620d2f8e090047e56adb07923047f95828d561f2` |
| `button-press.mp3` | `Audio/Jump.ogg` | 5178 | `c29ea1bb6353a9bd16753d3f46957f0c80fb08769cc945d83b20d3c48470236e` |
| `hit.mp3` | `Audio/Hit.ogg` | 7212 | `4a618da307a0e5807de7782693113328bf0bd2bcdedc1029a7ed5c545354f4a1` |
| `score-reached.mp3` | `Audio/Score.ogg` | 9509 | `cfd198edb6d6adb02456074d7617eec1c745cd00b5b912840cda2b8fbd1aa4fc` |
| `LICENSE.chromium` | `LICENSE.txt` | 1536 | `368cca1106be99d39ecd32a38d8305585d802a475effb66380b91ffc9bcf709b` |

上游路径均相对同一修订：图集为 `components/neterror/resources/images/default_200_percent/offline/200-offline-sprite.png`；音频位于 `components/neterror/resources/sounds/`。原始文件的完整 URL、尺寸、检索日期和哈希保留在原清单。

## Unity 导入约定

| 类型 | `Resources.Load` 路径 | 用途 |
| --- | --- | --- |
| `Texture2D` | `Dino/Sprites` | 2446 × 194 的原始 2x 图集，运行时代码按像素矩形创建精灵 |
| `AudioClip` | `Dino/Audio/Jump` | 跳跃 |
| `AudioClip` | `Dino/Audio/Hit` | 碰撞 |
| `AudioClip` | `Dino/Audio/Score` | 里程提示 |
| `TextAsset` | `Dino/LICENSE` | 完整许可文本 |

图集采用普通 Texture 导入，`spriteMode=0`、`textureType=0`、`isReadable=1`、Point 过滤、Clamp 环绕、关闭 mipmap、不压缩、保留非二次幂尺寸。最大尺寸设为 4096，避免宽 2446 的图集被缩小。音效采用 Decompress On Load、PCM 导入、预加载、禁用响度归一化与 3D 音频；运行时仍由公共音效开关控制播放。

`ffprobe` 对原字节副本的只读检测结果：

| 资源 | 容器 / 编码 | 采样率 | 声道 | 时长（秒） |
| --- | --- | ---: | ---: | ---: |
| Jump | Ogg / Vorbis | 44100 Hz | 2 | 0.141519 |
| Hit | Ogg / Vorbis | 44100 Hz | 2 | 0.283039 |
| Score | Ogg / Vorbis | 44100 Hz | 2 | 0.576145 |

此记录只证明来源字节、文件结构及导入配置；本任务内未试听音频、未运行 Unity 资源导入或 Android 设备播放验证。
