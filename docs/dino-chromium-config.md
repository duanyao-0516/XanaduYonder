# Chromium Dino 官方参数归档与本项目对照

抓取日期：2026-10-28。此次仅归档官方配置与解释调用链，没有修改本项目游戏运行参数、碰撞或资源。

## 来源与复现

官方 Gitiles 的 `main` 在抓取时解析为 **`3df8d372a3f9d5c38a89d0bc496cc44c85af1feb`**，提交时间为 2026-10-28 04:03:16 UTC。随后所有文件均以这个 SHA 下载，避免移动分支产生混合版本。[固定提交](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb)

归档内容：

- [parameters.json](chromium-dino-reference/parameters.json)：从源码字面量提取的普通/慢速、跳跃、障碍物、碰撞盒、动画、计分、夜景配置，以及明确标注的单位换算与调用链说明。
- [manifest.json](chromium-dino-reference/manifest.json)：每份原文件的上游路径、固定 SHA 链接、字节数及 SHA-256。
- [source/](chromium-dino-reference/source/)：17 份原始文件，包括 `offline.ts`、`trex.ts`、`obstacle.ts`、`offline_sprite_definitions.ts`、`game_config.ts`、`constants.ts`、地面/夜景实现与 CSS。
- [LICENSE](chromium-dino-reference/source/LICENSE)：同一提交的 Chromium 原始许可证，保留所有源文件的版权头。代码归档不是对整个 Chromium 的完整构建副本。
- [版本比较](chromium-dino-reference/chrome-156.0.8078.48-comparison.json)：本机桌面 Chrome 版本 `156.0.8078.48` 的官方 tag 解析到 `d0c02bfad08ec92a6ffcf989e53cd995e6381a4c`，`offline.ts`、`trex.ts`、`obstacle.ts`、`offline_sprite_definitions.ts` 四份文件的字节和 SHA-256 均与本次 main 快照相同。这不代表已经确认 OPPO 中 Chrome 的版本。

在仓库根目录运行：

```sh
# 不联网：逐文件检查原始字节、哈希，再解析源码并比对参数 JSON。
python3 scripts/fetch-dino-chromium-config.py --verify

# 重抓同一版本；默认也是下列固定 SHA，不会自动追踪 main。
python3 scripts/fetch-dino-chromium-config.py \
  --ref 3df8d372a3f9d5c38a89d0bc496cc44c85af1feb

# 另存一份，附带本机 Chrome 版本的四份核心文件对比。
python3 scripts/fetch-dino-chromium-config.py \
  --ref 3df8d372a3f9d5c38a89d0bc496cc44c85af1feb \
  --compare-ref refs/tags/156.0.8078.48 \
  --output /tmp/chromium-dino-156-reference
```

显式传 `--ref main` 才会更新版本。脚本只解析有限的 TypeScript 常量语法，不执行下载代码；遇到无法识别的常量表达式会失败，避免静默写入猜测值。`--verify` 能确认保存的来源文件与清单一致、参数可以重建；它不连接上游重新认证远端内容。

## 速度和加速度：数值之外还要看单位

Chromium 使用 600 × 150 的逻辑画布、参考 FPS=60。`speed` 是每个 **60 Hz 参考帧** 的逻辑像素数，不是每秒像素数；高分辨率精灵图的 2 倍资源尺寸不意味着游戏单位翻倍。[constants.ts](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/constants.ts)

| 参数 | 普通模式原值 | 慢速模式原值 | 含义 |
| --- | ---: | ---: | --- |
| speed | 6 | 4.2 | 未经窄视野缩放的初速 |
| maxSpeed | 13 | 9 | 名义速度上限 |
| acceleration | 0.001 | 0.0005 | 每次有效 Runner 更新增加的 speed |
| mobileSpeedCoefficient | 1.2 | 1.5 | 窄视野初速配置；慢速分支实际跳过该缩放 |
| gapCoefficient | 0.6 | 0.3 | Runner 配置；实际障碍间距还要追踪 Horizon 保存的系数 |
| clearTime | 3000 ms | 基础值 3000 ms | 开始生成障碍的运行时间门槛；音频辅助路径还会增加 |
| invertDistance | 700 | 350 | 触发夜间的计分里程，不是秒数 |

以上原值来自 [offline.ts 配置](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline.ts#n36)。

实际更新顺序中，距离使用 `currentSpeed × deltaTime / (1000/60)`，而加速仅做 `currentSpeed += acceleration`，没有乘 `deltaTime`。障碍物每次移动取整为 `floor(speed × 60/1000 × deltaTime)`；鸟还可能加上自己的速度偏移。只有 `currentSpeed < maxSpeed` 才加速，但没有最终钳位，所以数值可能比名义上限略高一个增量以内。[Runner.update](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline.ts#n887)、[Obstacle.update](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/obstacle.ts#n174)

| 模式与更新频率假设 | 初速 → 名义上限，逻辑 px/s | 等效加速度，逻辑 px/s² | 宽视野名义到顶时间 |
| --- | ---: | ---: | ---: |
| 普通，60 次有效更新/s | 360 → 780 | 3.6 | 约 116.7 s |
| 普通，120 次有效更新/s | 360 → 780 | 7.2 | 约 58.3 s |
| 慢速，60 次有效更新/s | 252 → 540 | 1.8 | 约 160 s |
| 慢速，120 次有效更新/s | 252 → 540 | 3.6 | 约 80 s |

这里是源码公式推导，不是对特定手机刷新率、掉帧情况或首跳开场动画的实测。屏幕支持 120 Hz 也不保证浏览器一定每秒执行 120 次有效更新。

当逻辑画布宽度 `W < 600` 时，普通模式初速为 `min(6, 6 × W/600 × 1.2)`。例如 W=360，初速是 4.32，即 259.2 px/s；上限仍为 13，并没有按同一比例降低，60 次更新/s 时理论加速到顶约 144.7 s。慢速模式走专用分支，直接保持 4.2，因此不能把其配置中的 1.5 机械套进公式。[setSpeed](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline.ts#n485)

## 跳跃、长按与下蹲

坐标原点在左上，向下为正。站立尺寸 44 × 47，蹲下 59 × 25；默认地面位置 `150 - 47 - 10 = 93` 是角色左上角的 y 坐标。[Trex 配置](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/trex.ts#n64)

| 参数 | 普通 | 慢速 | 解释 |
| --- | ---: | ---: | --- |
| initialJumpVelocity | -10 | -20 | 实际起跳还要减去 currentSpeed/10 |
| gravity | 0.6 | 0.25 | 每参考帧的速度增量；60 Hz 等效 2160 / 900 px/s² |
| minJumpHeight | 30 | 45 | 相对地面上升到此门槛，才允许松开截短上升 |
| maxJumpHeight | 30 | 50 | **绝对 y 坐标门槛**，触发 endJump；不是净跳高，也不是硬限制 |
| dropVelocity | -5 | -5 | 松开后截短上升使用的速度 |
| speedDropCoefficient | 3 | 3 | 空中向下操作后的位移倍率 |

正常模式以 speed=6 起跳时速度是 -10.6/参考帧，即 -636 px/s；在 speed=13 时变为 -11.3，即 -678 px/s。因此原版的跳跃会随奔跑速度略微变化。Runner 基础配置中另有 `initialJumpVelocity: 12`，不能误当成 Trex 正常起跳值；实际起跳读的是 Trex 的配置。

原版有长按区别：松开会调用 `endJump()`，但只有已达到最小上升门槛且仍以快于 -5 的速度上升时才截短；继续按住则保留上升过程，直到高度门槛触发同一处理。空中下蹲先把速度设为 +1，随后纵向位移乘以 3；地面下蹲切换尺寸与碰撞盒。每次跳跃更新先把 `round(速度 × framesElapsed)` 加到 y，再累加重力，因此直接移植数值到另一种积分器也不保证同一轨迹。[startJump / endJump / updateJump / setSpeedDrop](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/trex.ts#n465)

## 障碍物与间距

| 普通模式类型 | 单体尺寸 | y 坐标 | 出现速度 minSpeed | 允许组合的 multipleSpeed | minGap |
| --- | --- | --- | ---: | ---: | ---: |
| 小仙人掌 | 17 × 35 | 105 | 0 | 4 | 120 |
| 大仙人掌 | 25 × 50 | 90 | 0 | 7 | 120 |
| 翼龙 | 46 × 40 | 桌面 100/75/50；移动端 100/50 | 8.5 | 999 | 150 |

普通障碍组最多 3 个，先随机选 1～3 个，再在速度未达到 multipleSpeed 时降为单体。翼龙使用 ±0.8 的相对速度偏移、2 帧动画，每帧 1000/6 ms。精灵配置中还包含 collectable，属于特殊游戏模式的切换机制，普通 Dino 不按常规障碍生成它。[精灵与碰撞配置](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline_sprite_definitions.ts#n143)

普通模式最小间距：`round(整组宽度 × speed + 类型 minGap × 0.6)`；最大间距为最小值的 1.5 倍再四舍五入，最终在两者间均匀取整数。音频辅助模式会再将最终间距加倍。例如小仙人掌单体 speed=6 时，最小间距 `17×6 + 120×0.6 = 174`，最大 261。下一组的生成还要求上一组的右边界加 gap 已进入可视范围；同类型最多连续出现两次。[Obstacle.getGap](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/obstacle.ts#n204)、[Horizon](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/horizon.ts#n250)

慢速模式必须区分“声明常量”和“当前代码实际状态”：Horizon 构造时保存 gapCoefficient，而后续 `toggleSpeed()` 只更新 Runner.config，没有更新这个已保存的值。普通初始化后第一次切慢速，Obstacle 实际收到的是原先 0.6，再乘 2，成为 1.2；不能声称一定等于 `slowModeConfig.gapCoefficient(0.3)×2=0.6`。同时 `adjustObstacleSpeed()` 会就地把 multipleSpeed 与 minSpeed 除以 2、minGap 乘以 1.5，并将可变 y 固定到数组首项。该方法在非慢速分支没有还原操作，反复切换还会受已有状态影响。归档 JSON 保留原始声明值，并单独记录这些调用链差异。[toggleSpeed](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline.ts#n980)、[adjustObstacleSpeed](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/horizon.ts#n86)、[Obstacle 构造](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/obstacle.ts#n60)

## 白边、碰撞与夜景的官方依据

源码明确说明恐龙与障碍有 1 像素白边。原版粗碰撞盒从图像位置 x+1、y+1 开始，宽高各减 2，随后以此为原点继续检查细分碰撞盒；使用严格 `<` / `>`，仅边缘相等不算重叠。细分碰撞盒不是按每个不透明像素检测；成组仙人掌还会拉宽中间碰撞盒。完整矩形数据已保存于 `parameters.json` 的 `trex.collisionBoxes` 与 `sprites.original.obstacles[*].collisionBoxes`，原版的判定函数也在归档中。这个证据说明原版怎样处理白边，不能单靠白边的存在证明某次死亡是否误判。[checkForCollision](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline.ts#n1581)

原版夜间用 CSS `invert(1)` 反色，白色像素也随之变黑；系统深色模式有另一组反色规则。夜间由实际分数到达 700 的整数倍触发，慢速为 350；计分是 `round(ceil(distanceRan)×0.025)`。`invertFadeDuration=12000` 在 Runner 中控制夜间计时，并非“每 12 秒昼夜轮流切换”，也不是 CSS 过渡时长。月亮与星星配置、月相数组、地平线的原图位置及实现均已归档。[夜间触发](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/dino_game/offline.ts#n903)、[CSS 反色](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/components/neterror/resources/neterror.css#n347)

## 与本项目当前实现的可比差异

以下按本次查看的 `bd7acb6` 工作树源码，不表示已经调整为 Chromium 规则。当前 [DinoRules.cs](../Assets/Scripts/Core/Dino/DinoRules.cs) 固定 60 Hz 模拟，初速 180 世界单位/s，加速度 24 世界单位/s²，速度上限按实际视野计算再乘 4。文档记录的典型竖屏上限为 1069，宽横屏为 1440；其他视野应读实际计算结果。

精灵在世界中放大了 1.35 倍，而 [DinoPageLayout.cs](../Assets/Scripts/Runtime/Dino/DinoPageLayout.cs) 再使用 1.2 的 UI/world 比例。为了和 Chromium 的 44 px 角色作长度尺度比较，下表将世界速度除以 1.35；UI 的 1.2 同时放大角色与位移，在这种角色尺寸归一化中抵消。不能直接把 180 或 1440 与原版 6 或 13 比大小。

| 项目 | 本项目世界单位 | 按原图角色尺寸归一化 | Chromium 普通、60 次更新/s |
| --- | ---: | ---: | ---: |
| 初速 | 180/s | 约 133.33 px/s | 宽视野 360 px/s；360px 窄视野 259.2 px/s |
| 典型竖屏上限 | 1069/s | 约 791.85 px/s | 780 px/s |
| 宽横屏上限 | 1440/s | 约 1066.67 px/s | 780 px/s |
| 加速度 | 24/s² | 约 17.78 px/s² | 3.6 px/s² |
| 典型到顶时间 | 竖屏 37.05s；宽横屏 52.5s | 时间无需长度换算 | 宽视野约 116.7s |

当前实现的初段相对角色尺寸较慢，但加速度与宽横屏上限已经高于原版在 60 次更新/s 假设下的值。因此“感觉仍慢”不能简单解释成原版 acceleration 数字更大，也不适合再盲目翻倍。本轮保留用户已经选择的高难度调校，只提供可核对的官方依据。

手感仍有多项独立差异：本项目以固定时间步和 4 个子步做模拟、采用独立跳跃物理、按视野与真实通行规划生成障碍，并使用自己的碰撞算法；Chromium 有按回调次数加速、位移取整、速度相关起跳、最小跳高截短、成组碰撞盒、类型解锁等规则。以上归一化只是长度与时间尺度对照，**同一组数字并不等于同一手感**。如果后续要求原版手感，应把这些规则作为一个整体移植和验证。

## 本次验证范围

已完成 17 份上游原文件逐字节 SHA-256 校验、源码参数重新提取比对、上述桌面 Chrome tag 的四份核心文件对比，以及脚本语法与文档空白检查。另在独立输出目录重新联网抓取并校验，提取的参数 JSON 与归档完全一致。没有运行 Unity、构建 APK、安装应用或修改运行配置。没有实测设备 Chrome 更新频率；当前模型的白边与碰撞复现见[诊断记录](dino-collision-diagnosis.md)。
