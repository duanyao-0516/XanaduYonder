# Chromium Dino 加速实现核对

核对日期：2026-10-28。使用 Chromium 官方源码，只读对照；本项目没有引入其游戏实现代码。

## 来源与完整性

- [当前 main 的 offline.ts](https://chromium.googlesource.com/chromium/src/+/main/components/neterror/resources/dino_game/offline.ts)
- [固定资源修订 64574fa0f2daa1b48f2703a072687272000178ce 的 offline.ts](https://chromium.googlesource.com/chromium/src/+/64574fa0f2daa1b48f2703a072687272000178ce/components/neterror/resources/dino_game/offline.ts)
- [main 的 constants.ts](https://chromium.googlesource.com/chromium/src/+/main/components/neterror/resources/dino_game/constants.ts)

下载 Gitiles `?format=TEXT` 响应并 Base64 解码后，两个 `offline.ts` 均为 55,293 字节，逐字节比较一致。以下 SHA-256 针对解码后的原始文件，而非 HTML 页面或 Base64 响应：

| 文件 | SHA-256 |
| --- | --- |
| main / 固定修订 `offline.ts` | `3d6e251e07c7ff8a5d981c2cc736a2a2fa3dd3c4bf1b7e58546f83255e4af2ad` |
| main `constants.ts` | `2c6b09f9a6cad4f781ad81b1da9cc7036594d607c67d756bdfc2b66a076768a2` |

原始下载仅留在本工作树忽略目录 `artifacts/analysis/chromium-acceleration/`。下述行号按解码后的源码计算。

## 参数与实际更新路径

`offline.ts:61–80`：普通模式初速 6、上限 13、每次更新增加 0.001；慢速模式初速 4.2、上限 9、每次更新增加 0.0005。`constants.ts:18–24` 的参考帧率为 60，默认视口宽度为 600。

`offline.ts:485–496`：小于 600 宽的普通模式以 `min(speed, speed × width / 600 × 1.2)` 调整初速；慢速模式跳过该缩放。此处没有同步缩小最高速度。

`offline.ts:886–890`：行程增量乘以 `deltaTime / msPerFrame`，但速度增量直接增加配置值，没有乘 `deltaTime`。`offline.ts:1294–1298` 用 `requestAnimationFrame` 调度下一次更新，因此这个加速过程不是独立于回调帧率的固定时间曲线。

## 达到最高速度的推算

以下由源码参数按 `(最高速度 − 初速) / (每次增量 × 每秒回调数)` 推算，不是真机秒表测量。假设正常连续运行、无停顿及额外配置修改；浮点封顶和开场可能产生约一帧差异。

| 模式／视口 | 初速 | 上限 | 60 次回调／秒 | 120 次回调／秒 |
| --- | ---: | ---: | ---: | ---: |
| 普通，宽度至少 600 | 6 | 13 | 116.7 秒 | 58.3 秒 |
| 普通，宽度 360 | 4.32 | 13 | 144.7 秒 | 72.3 秒 |
| 普通，宽度 420 | 5.04 | 13 | 132.7 秒 | 66.3 秒 |
| 慢速 | 4.2 | 9 | 160 秒 | 80 秒 |

据此不能把「原版体感很快」写成「Chromium 几秒便达到真正上限」。本次没有测量用户所运行的 Chrome 版本、刷新率或具体模式，不能替用户判断其体感来源。

## 本项目采用的修订

根据用户希望缩短提速过程的反馈，`DinoRules` 加速度由 6 提高至 12 单位／秒²，起步速度保持 180，最高速度继续由实际视野和原有反应窗口推导。宽视野达到 360 的时间由 30 秒缩为 15 秒；这属于本项目的体验调校，不宣称与 Chromium 曲线一致。

左右各 32 设计单位留边后，720 宽竖屏实际跑道为 656，逻辑宽约 546.667，生产安全上限 267.25，名义上约 7.271 秒达到，固定步中第 437 tick（约 7.283 秒）封顶。1515 宽横屏实际跑道为 1451，逻辑宽约 1209.167，安全上限 360，第 900 tick（15 秒）封顶。

`SpeedAt` 与 `Travel` 共用 `SpeedPerTick`，只改加速度常量即可同时更新实际速度及精确预测；测试必须继续核对加速段、跨封顶点和封顶后逐 tick 位移，并回放实际生成的障碍序列。本项目仍采用固定 60 Hz 模拟，显示帧率不改变提速时间。
