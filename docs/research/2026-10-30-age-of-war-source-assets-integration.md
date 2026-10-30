# 战争进化史两代：源码、素材完整性与 App 接入

日期：2026-10-30。分析对象为 7k7k 的一代 **15793**、二代 **28601**。本轮完成实际解包、反编译、引用检查、动画抽样及 Chrome 本地运行；未修改原 SWF，未实现 App 页面、构建 APK 或做 Android 真机测试。

工作树基于本地 master `41a235dd81adb66c45f380f5b6c837e0a92dead0`，分支 `p/duanyao.0516/feature/age-of-war-research`。本报告更新[前期玩法、GitHub 复刻及增量调研](2026-10-30-age-of-war-feasibility.md)中的待核验项。

## 结论与建议

**这两份下载是包含主体游戏的客户端，主要规则和美术没有缺包。** 一代资源直接在主文件内；二代有 30 个内嵌 SWF，已经全部找到、展开，不能因顶层没有图片就判断下载不完整。两代均已在本地 Chrome 的 Ruffle 中进入实际战斗，无须安装 Flash 插件或其他浏览器。

但“完备”需要分层：

| 层级 | 结果 | 边界 |
| --- | --- | --- |
| 下载文件完整 | 通过 | 压缩流结束、声明长度、哈希均核对；版本固定到下表 |
| 主要脚本和素材包含在本地 | 通过静态检查及战斗抽样 | 共 32 个 SWF（两份主文件及二代 30 内嵌包）的数值字符引用无缺失；另查主要动态名称 |
| 所有素材独立导出、可直接生产使用 | 尚未完成 | 字体子集/设备字体、时间轴事件和坐标仍需处理；未完成所有动画的转换验收 |
| 原作者源码工程 | 没有取得 | 得到的是反编译 AS 与发布资源，没有原始 FLA、完整创作层或可直接编译的 Unity 工程 |
| 全玩法兼容及 App 接入 | 尚未证明 | 桌面战斗成功不等于全部时代、难度、声音、Android WebView 和续局都通过 |

**正式接入建议采用 Unity C# 重建规则并转换素材；原 SWF + Ruffle 保留为原版对照和短期兼容性原型。** 原因是当前 App 每款游戏都有独立续局契约，而两份 SWF 均未找到实际战场持久化实现。若产品允许进程退出后重新开局，Ruffle 才是明显更短的发布路线。原版素材的再分发范围仍沿用前期报告的来源与许可核验结论，本轮解包不改变其许可。

## 文件身份与证据目录

| 对象 | 一代 | 二代 |
| --- | --- | --- |
| 7k7k 页面 | [15793](https://www.7k7k.com/swf/15793.htm) | [28601](https://www.7k7k.com/swf/28601.htm)，用户指定页 |
| 实际文件 | [zhanzhengjinhuashi.swf](https://flash.7k7k.com/fl_5/20071210/zhanzhengjinhuashi.swf) | [age-of-war-2.swf](https://flash.7k7k.com/fl_8/20100528/age-of-war-2.swf) |
| 下载字节数 | 3,628,467 | 11,413,675 |
| SWF | CWS / 9 / AVM1 | CWS / 10 / AS3 / AVM2 |
| 舞台与名义帧率 | 650×450，40 fps | 720×480，30 fps |
| 解压总长 | 4,098,468 | 11,707,784 |
| SHA-256 | `5f88517846ff0eb75eee3bd6dfebbf9fad75edbb4dbe20ed8ea4c0afb3dfd3a1` | `b7dc9f9654add56e87234381e5272695592e3837bbd8489f836ed7225b81960c` |

一代 7k7k 文件与前期测量的 MaxGames 文件有字节差异，不能混用版本结论；二代与前期 MaxGames 样本逐字节一致。文件和下载清单在[下载目录](/Users/bytedance/Downloads/age-of-war-research-20261030)。

使用官方 **JPEXS FFDec 26.4.0 / Java 17** 导出脚本、图形、音频、字体、文本和符号映射；完整 SWF 保留时间轴。正式导出退出码均为 0，日志未见反编译失败标记。通过官方解析库的 `Tag.getNeededCharacters` 检查数值资源引用；这不是对任意动态脚本路径的形式化证明。工具依据：[CLI](https://github.com/jindrapetrik/jpexs-decompiler/wiki/Commandline-arguments)、[功能说明](https://github.com/jindrapetrik/jpexs-decompiler/wiki/Features)。

稳定证据目录：

- [一代清单与脚本](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow1)，含 `inventory.json`、`tag-inventory.json`、分析器、导出日志和动画样本。
- [二代清单与脚本](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow2)，含 `outer`、30 包映射、`embedded-swf`、`embedded`、`data.xml` 和 `reference-check.tsv`。
- [运行证据](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-evidence)、[本地预览包](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-preview.zip)。大文件留在 Downloads，没有加入 Unity Assets 或 Git。
- [仓库内测量摘要](2026-10-30-age-of-war-source-assets-measurements.json)保存输入指纹、导出数量和验证范围。

## 实际取得哪些内容

下表为文件计数，不能把形状数当角色数，也不能把导出目录大小当 APK 增量。

| 类别 | 一代 | 二代（外层 + 30 内嵌包） |
| --- | ---: | ---: |
| 反编译 AS | 315，79,747 B | 231 + 150 = 381，605,891 B |
| 位图 | 1 | 151 |
| SVG 形状 | 578 | 1,111 |
| morph SVG | 9 | 8 |
| 独立音频文件 | 28，2,507,855 B | 外层 54，3,491,119 B；另补片头 WAV 1，546,092 B |
| TTF 字体子集 | 8 | 25 |
| 文本导出 | 33 | 137 |
| 内嵌 SWF | 0 | 30，原始内嵌字节共 7,997,972 B |

一代基础导出共 973 文件、8,026,774 B。二代外层导出共 316 文件、12,052,203 B；30 包派生导出共 21,816,557 B。外层 binaryData 与内层内容存在包含关系，**不能相加当作运行必需资源大小**。二代统计排除了试导的重复 `embedded/AGE1_UNITS` 目录。

一代有 316 个 Sprite、43 个 linkage。数值字符引用缺失为 0；字面量 `attachMovie` 及已审查的 `bullet1..12`、`part1..12`、特殊弹体名称均可解析。二代外层与 30 包分别检查，31/31 本包引用闭合，单位 `u1..u29`、炮塔 `t1..t17` 均在对应时代符号表中。

二代 `EngineStart.as:18` 固定启用内嵌资源，`RessourceManager` 选择内嵌类，`MovieClipLoaderAsset.as:39` 使用 `loadBytes`。源码出现的 `assets/swf/...` 是另一加载模式的备用路径。`GameScreen.as:323–426` 在开战前加载 UI、效果、背景、基地、投射物及七时代单位和塔；本地进入战斗实际经过了这条初始化链。

### 还不能直接交给 Unity 的部分

1. **动画与伤害事件相互关联。** 一代 Club man 的行走 43 帧、攻击 40 帧已完整抽样导出，共 83 张透明 PNG、660,878 B。攻击第 20 帧调用 `hit()`，行走末帧回环，死亡末帧停止；PNG 不包含这些语义。必须保存帧事件、原点、父级矩阵、镜像、帧率与状态切换。
2. **帧图集可能显著放大内存。** 上述两段动作的未压缩 RGBA 像素共 7,230,240 B，约 6.90 MiB，尚无图集留白、mipmap 或共享压缩；只是像素模型，不是 GPU 实测。行走和攻击画布分别为 104×135、171×176，不能全用中心 pivot，否则换动作会跳动。原始矢量与部件结构应保留。
3. **字体不是完整字库。** 一代有 10 个 DefineFont3，Arial/Castellar 两项无嵌入字形，部分动态文本使用设备字体，实际只导出 8 个 TTF。二代 25 个 TTF 同样是嵌入子集，不能假定可用于中文 UI。
4. **流式声音已补提取，但尚未试听。** 二代 SPONSOR 的 sprite58 内含 1 个 SoundStreamHead、185 个流式块，默认 sound CLI 未生成文件；随后通过官方 `getRanges()` 和 `SoundExporter` 成功补提取为 546,092 B、44.1 kHz 单声道 PCM WAV。独立声音共 54 + 1 个文件，WAV 结构已验证；这解决了批量导出的已知遗漏，不等于所有声音已听感验收。
5. **没有全量动画正确性结论。** 只检查了一代 Club man 的行走/攻击和二代 Clubman 的待机前 12 帧（12 张 541×480 PNG、382,562 B，非空且像素各异）。二代该角色时间轴有 206 帧，攻击从 91、命中事件标签从 112、行走起步从 142、循环行走从 147、死亡从 171 帧开始；该样本没有覆盖这些动作。遮罩、形变、颜色变换、复杂技能、全部单位的命中点与效果仍需逐项验收；没有重编译 AS 或重建 FLA。

![一代 Club man 行走与攻击抽样](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow1/clubman-contact-sheet.png)

## 目标版本的规则已经能从代码确认

| 规则 | 一代 7k7k 样本 | 二代 7k7k 样本 |
| --- | --- | --- |
| 内容规模 | 5 时代、16 兵种、15 炮塔 | 7 时代、29 兵种、**17 炮塔**、22 投射物、7 特殊技 |
| 开局 | 金币 175，双方基地 HP 500 | XML 配置金币 2500、XP 1000、时代 1；覆盖类中的临时默认值 |
| 进化 | 累计 XP 门槛 4000/14000/45000/200000，**不扣 XP** | 进化消耗 XP，6 次有效升级 |
| 特殊技 | 2000 帧充能，**不扣 XP** | 消耗 XP，与进化竞争资源 |
| 防御/成长 | 默认 1 塔位，新增价格 1000/3000/7500；训练队列 5 格 | 4 塔位；18 升级项、58 个 level 节点 |
| AI | 按计时、数量门槛和随机出兵、进化，并非和玩家共用对称经济 | 内嵌 AI XML 驱动时间事件与出兵 |
| 加速 | 无本轮确认的二倍速入口 | HUD 切换 1/2 倍速，主战斗循环使用该倍率 |

一代数据入口：`analysis/aow1/export/scripts/frame_13/DoAction.as:2–8`，进化为 `DefineButton2_188/BUTTONCONDACTION on(press).as:4–9`。二代数据入口：`analysis/aow2/outer/scripts/net/louissi/Alpha1/Global/Ressources_DATA_CLASS.as`，XML 已原样提取到 [data.xml](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow2/data.xml)。详细路径和行号见两份子报告：[一代](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow1/summary.md)、[二代](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/aow2/summary.md)。

对微信文章的修正：

- 当前一代文件确实保留“游戏内暂停时特殊技充能、训练继续”的代码路径。其他发行版本修复记录不能否定此样本；App 后台必须暂停整个播放器或自己的模拟循环。
- 一代 Egg Automatic / Fire Catapult 是 200 / 750，不是文章中的 100 / 500。第三时代技能持续治疗存活单位，不是复活死兵。
- IonRay 射程 800，塔与敌基地水平距离至少 898，空场不能直接锁定基地；弹体碰撞仍可能涉及基地。“四塔直接拆家”没有运行证明，不能作为已验证策略。
- 二代 Income 升级最高 bonus=0.3、双倍速和技能扣 XP 均在当前脚本找到；一代不能套用二代资源消耗规则。

## 本地运行结果及剩余外部行为

使用锁定的 **Ruffle v0.7.1**，JS/WASM 和两份原 SWF 均从 `127.0.0.1` 加载。研究壳设置仅同源 CSP、关闭外链与游戏脚本访问。系统没有切飞行模式，因此准确结论是“本地资源运行且外部资源被页面策略拦截”，不是整机断网测试。

| 场景 | 实际观察 |
| --- | --- |
| 一代 Normal | 启动、菜单、进入战场、近战/远程出兵、敌军推进、战斗和金币/XP/HP 变化；播放器暂停与恢复成功 |
| 二代 Easy | 启动、菜单、难度、教程、出兵、购买塔位和建塔、攻击/收益/基地受损，最后进入 Defeat 画面 |
| 二代暂停 | 暂停约 75 秒时 HP 615、金币 650 保持；恢复后 HP、金币和敌基地状态继续变化 |
| 外部请求 | 二代 MochiBot 的 `http://core.mochibot.com/my/core.swf` 被 CSP 拦截，仍进入并持续进行上述对局；一代此抽样未记录到外部资源请求 |
| 声音 | 运行时统一静音；声音文件已导出，尚未试听、验证音频手势或手机后台漏音 |

证据：[一代战斗](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-evidence/aow1-battle.jpg)、[二代战斗](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-evidence/aow2-battle.jpg)、[暂停](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-evidence/aow2-paused.jpg)、[失败结算](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-evidence/aow2-defeat.jpg)。未覆盖全部时代、技能、进化、所有难度、胜利通关或移动设备。

**二代有一项模式限制：Insane 按加载地址判断 MaxGames 域名。** 整体启动域名锁为空，但 `Screens/Author.as:66–77` 设置 `onMaxGames`，`Difficulty.as:44–50,113–122` 决定真实 Insane 按钮或跳转按钮。Easy/Medium/Hard 不走此限制，实测仅 Easy。没有修改域名判断或原文件，不能承诺四种难度全部离线可用。

MochiBot 属于统计调用；Kongregate API 分支存在但构造参数关闭。作者、赞助商、攻略链接主要是可选导航。本次拦截下战斗成功说明所测路径不依赖这些服务，不代表全部错误处理和全流程已穷尽。

**两代均没有找到真实战场存档。** 一代没有 SharedObject 调用；二代只有 import 与一个 null 字段，未见 `getLocal/getRemote/flush`，也没有注册 `ExternalInterface.addCallback`。二代 Resume 只是恢复当前内存战斗。保留 WebView、暂停 VM、持久化战场是不同能力；开启 DOM Storage 不会凭空保存单位、弹体与 AI。

## 接入当前 App 的两条路线

当前项目为 Unity 6000.3.27f1、Android ARM64/IL2CPP；`Core` 无 Unity 依赖，[AppController](/Users/bytedance/.codex/worktrees/age-of-war-research/XanaduYonder/Assets/Scripts/Runtime/AppController.cs) 显式管理页面和保存。构建强制竖屏，公共 Canvas 为 720×1320，已有[独立存档契约](/Users/bytedance/.codex/worktrees/age-of-war-research/XanaduYonder/docs/architecture.md)。没有现成 WebView/Ruffle 容器。

### 路线 A：原 SWF + 本地 Ruffle + Android WebView

可最大程度保留原画面、时间轴与战斗逻辑，无须先把数千形状改成 Unity 资源。桌面样本已经证明有可行基础。实际增量是独立 Android 容器及宿主接线：

- 用 `WebViewAssetLoader` 或受控 PathHandler 提供 JS/WASM/SWF，处理正确 MIME；未匹配资源明确拒绝，同时阻止外部导航，不依赖 CDN。Android StreamingAssets 不是普通文件夹。[Android 本地内容](https://developer.android.com/develop/ui/views/layout/webapps/load-local-content)
- 进入战争页横屏，退出恢复竖屏；原生 WebView 触摸和 Back 回传，处理安全区、小按钮、hover 提示替代；公共 Unity 弹窗不能假定自动显示在原生 View 上方。
- 区分 Activity 后台与 WebView 获焦。锁定版本实际验证的是 `player.ruffle().suspend()/resume()` 以及 `volume`，不要混用另一层 API 的 `pause/play`。公共声音开关要传播到 Ruffle。
- `WebView.onPause()` 不停止所有 JS，`saveState()` 也不保存整个游戏 VM。后台必须显式暂停；返回大厅需明确保留还是销毁容器，并成对移除 View、音频与桥。[Android WebView API](https://developer.android.com/reference/android/webkit/WebView)
- Ruffle v0.7.1 源码明确 `NetworkingAccessMode.None` 尚未实现，不能只设置 `allowNetworking: 'none'` 就宣称离线；正式容器仍需原生请求及导航控制。[固定版本配置](https://github.com/ruffle-rs/ruffle/blob/v0.7.1/web/packages/core/src/public/config/load-options.ts)

**此路线没有现成的任意战场续局方案。** 如果追加 SWF 状态导出/恢复或模拟器 VM 快照，会形成新的实现和维护范围，不在“包一个 WebView”预算内。Insane 限制也要作为明确产品边界处理。

### 路线 B：转换原素材，Unity C# 重建规则

与当前 App 的续局、离线、设置和资源生命周期更一致，建议作为正式路线：

- `Core/AgeOfWar` 拥有固定步长战斗、训练/经济、时代、AI/RNG、投射物、技能与结算；一二代使用独立规则集，共享语义相同的能力。保留原 40/30 fps 的时间含义，不能直接当 60 fps 计数。
- `Runtime/AgeOfWar` 拥有横屏战场、触控 HUD、单位/炮塔表现、图集和音效。素材转换清单至少包含符号 ID、动作、帧率、pivot、帧事件和声音事件；先验证一个复杂单位与技能，再扩全量。
- 独立快照保存双方资源/时代/基地、单位稳定 ID/坐标/HP/冷却、队列、弹体、持续技能、升级、AI/RNG、终局已记账标志。沿用严格 Codec、原子 Store 和合法备份；恢复不能重复扣款、奖励或结算。
- AppController 只增加入口、Enter/Leave、Save、设置、暂停和返回分发。不要将战争规则塞进公共宿主，也无需先重写通用游戏注册框架。离开时仅 `SetActive(false)` 不等于释放图集。

完整宿主证据和行号见[宿主复核](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/app-integration-review.md)。

## 增量估算与实施验证顺序

实际生成的本地研究包包含 HTML、两份 SWF、Ruffle JS/WASM 和许可，共 **10 文件、44,275,614 B 展开（42.22 MiB）、25,211,341 B ZIP（24.043 MiB）**。去掉 source maps，保留两个 WASM 变体；ZIP 使用 Deflate 6。这是可复核的运行资源包，**不是 APK/AAB 增量、安装占用或运行内存**。测量见[本地 JSON](/Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-bundle-measurements.json)。

| 方案 | 当前可用预算 | 本轮能否把它升级为实测 |
| --- | --- | --- |
| Ruffle 两代共享 | APK 暂留 25–45 MiB；宿主和回归 12–24 工程人日 | 不能。只实测资源 ZIP；不含完整战场快照或引擎修复 |
| Unity 一代 | APK 20–45 MiB；37–59 工程人日 | 不能。尚未制作完整 Unity 图集或规则 |
| Unity 两代共享 | APK 50–110 MiB；70–110 工程人日 | 不能。资源编码/复用和全量动作仍未知 |

人日是带范围的规划，不包含美术重制、许可等待；完整假设见前期报告。本轮解包减少了“资源是否存在、规则在哪里”的不确定性，但没有证明 C# 重建和逐帧对齐会自动变得很小。尤其不能把 14.35 MiB SWF 当作 Unity 原生增量。

建议下一阶段按以下顺序验收，不同时铺开两代：

1. **原生一代最小完整切片**：原始时代、近远程兵、基地、一塔、一技能；实装一份带 pivot 和命中事件的动画。建立与固定 SWF 的行为对照。
2. **先验续局契约**：战斗中暂停/返回/后台/杀进程恢复，校验队列、弹体、随机、奖励与结算；快照稳定后展开五时代，再扩二代规则。
3. **按相同构建配置测增量**：先构建当前基线，再构建切片，测 APK、纹理实际格式、晚期单位压力、后台和返回大厅后的内存。WebView 路线需一起统计相关进程，不能只读 Unity 托管堆。

若产品最终选择保留原版、接受退出丢局，则改为先做 Android 离线 WebView 验证容器，重点验证横屏往返、Back、真实触摸、焦点、声音、断网与资源销毁。本轮已完成桌面资源验证，未开始上述 App 实现。

本地预览可用 Chrome 打开 `http://127.0.0.1:8766/`；当前页面保留并暂停。服务结束后，可在下载目录执行：

```sh
python3 -m http.server 8766 --bind 127.0.0.1 --directory /Users/bytedance/Downloads/age-of-war-research-20261030/analysis/runtime-preview
```
