# 战争进化史一代和二代玩法素材与接入调研

调研日期：2026-10-30。目标项目：XanaduYonder，Unity 6000.3.27f1、C#、Android ARM64、离线小游戏合集。

本次从**本地 `master` 的 `41a235dd81adb66c45f380f5b6c837e0a92dead0`** 创建工作树，研究分支为 `p/duanyao.0516/feature/age-of-war-research`。原目录的未提交文件未带入。本报告讨论接入可行性，不包含游戏实现。

> 后续实证：已完成 7k7k 15793/28601 两份 SWF 的反编译、内嵌资源展开、动画抽样与 Chrome 本地战斗验证。更新结论、存档缺口和接入决策见[源码、素材与接入报告](2026-10-30-age-of-war-source-assets-integration.md)。本文件保留前期公开仓库调研与预算口径。

## 结论

**适合先做一代，再用同一个战斗内核扩展二代。** 现有 app 已有 Unity 引擎、大厅、设置和本地存档基础，新增成本主要是实时战斗、横屏交互、角色动画、数值验证和资源管理。二代还有兵种克制、升级与解锁、人口和技能资源取舍，不能仅增加两个时代便算完成。

可以考虑两条路线：

- **Unity 原生实现**：更适合接入现有离线合集，便于触控、完整续局、声音设置和后续改造。参考复刻项目的逻辑，按本项目结构实现；原版素材或原创替代素材另行选定。
- **原版 SWF 加 Ruffle**：适合优先验证原版观感与操作。两部官方 SWF 合计只有 **14.35 MiB**，但加上完整网页运行资源后，模拟 ZIP 为 **24.04 MiB**，展开为 **42.22 MiB**，还要加 WebView 桥接。此路线尚未在本 app 或 Android 设备运行，不能把它当作已可直接接入的方案。

原版游戏、美术和音乐的使用范围需要分别核清；开源模拟器或复刻仓库的代码许可不会自动覆盖原作素材。没有发现可直接导入本项目、并已证明两代玩法完整和素材授权齐全的现成模块。

## 两篇微信参考文章

微信链接直接访问均返回验证页；用户随后提供完整长截图，本报告依据截图读取正文。图中尚未加载出来的媒体不视为已读取。截图仅用于调研，未复制到项目资源中；文件哈希记录在[测量数据](2026-10-30-age-of-war-measurements.json)。

### 一代通关策略研究

[战争进化史 Age of War 通关策略研究](https://mp.weixin.qq.com/s/_9KkwL3KOYFqMcWOd3-HKA)，截图显示公众号“知一轩”，2024-04-16 11:55。内容聚焦一代 Impossible 难度。

文章给出以下配置候选，适合后续与固定版本原程序逐项核对：

| 配置 | 文章数值 | 用途和边界 |
| --- | --- | --- |
| 时代 1→2、2→3、3→4、4→5 | XP 4,000 / 14,000 / 45,000 / 200,000 | 只作为一代目标版本的核验清单，不外推到二代 |
| 新增第 1、2、3 个塔位 | 金币 1,000 / 3,000 / 7,500 | 是三个新增塔位，不能据此说全局只有三个塔位 |
| 初期低价兵 | Club man 15；Slingshot man 25 | 用于对照出兵间隔、前后排和收益节奏 |
| 末期单位 | Super Soldier 150,000 | 攻略以积累资金后出多个超级士兵结束对局 |

文章的 Club man / Slingshot man 持续出兵、后排叠加、炮塔防守、积累经济再进化，是有用的玩家策略样本。后续对 7k7k 一代固定文件的分析确认：暂停时特殊技充能和训练仍继续；XP 是累计进化门槛、不扣费。Ion Ray 空场直接锁定敌基地不符合该样本的射程判断，流弹等其他路径尚未运行验证。具体源码与版本边界见后续实证报告。

原作者的一代更新记录明确修过“暂停时特殊技继续装填”和“终极炮塔打到敌方基地”；当前 7k7k 文件的静态行为说明不能用这条记录否定所有旧构建。复刻应固定具体文件，不把不同版本混用。[原作者更新记录](https://www.newgrounds.com/portal/view/408209)

另外，已在 MIT Unity 复刻的固定版本 [Data.cs](https://github.com/erupturatis/Age-of-war-unity-clone/blob/c6a2999193ab162298c76ca92279b4a5cc92bfd4/Assets/scripts/Data.cs) 中读到相同的四个 XP 值和三次塔位价格；该复刻将升级实现为累计 XP 门槛检查，不扣 XP。但同文件的 Egg Automatic／Fire Catapult 价格是 200／750，与文章表中的 100／500 不同。复刻作者也在炮塔时间参数旁注明部分数值经过调整，因此这些仍是交叉参考，不是原版程序的最终裁定。

### 一部战争的进化史

[经典游戏丨一部战争的进化史](https://mp.weixin.qq.com/s/yDI3qKYnJXNDmp6bgRHQtA)，小 K／7k7k 游戏，2026-08-19 18:30。

这篇的价值在于画面和体验比较：一代以蓝天、卡通剪影和较少操作建立循环；二代背景、单位和特效更丰富，增加兵种克制、属性升级和加速按钮。文章按前六个时代各四种单位、未来时代五种单位描述二代，共 29 种；这与官方移动版的总数一致，但仍不能替代 Flash 各版本的单位配置核验。

需要纠正或限定的描述：

- 文章将一代 Special 写成每次消耗经验。发行商的二代攻略明确支持这一规则；后续已确认目标一代 SWF 按帧充能、不扣经验，不能套用二代规则。
- “一代等离子炮攻击覆盖全图”与原作者修复终极炮塔射程的记录存在版本差异。
- 文中二代“钱袋升级后击杀收益增加 30%”和双倍速可进入待验证列表，不直接作为已验证的数值契约。
- “第三部”的虫巢和建筑自动出兵内容对应同作者后续作品 **Army of Ages**，中文门户常将其归入同系列，不能直接当作 Max Games 正式 Age of War 3。本次不将它连带加入工程量。[发行商游戏页](https://armorgames.com/play/11789/army-of-ages)

## 玩法与版本范围

| 维度 | 一代 Age of War | 二代 Age of War 2 |
| --- | --- | --- |
| 基本循环 | 出兵自动前进作战，击杀获取资源，炮塔守家，推进时代，摧毁敌方基地 | 同一骨架，增加更强的兵种与升级决策 |
| 内容规模 | 官方作者页明确 5 时代、16 单位、15 炮塔 | 7 时代；29 单位有官方移动版说明及微信截图佐证；后续固定 7k7k 文件核实为 29 单位、17 炮塔 |
| 时代 | 原始、中世纪、文艺复兴、现代、未来 | 原始、斯巴达、埃及、中世纪、文艺复兴、现代、未来 |
| 兵种 | 各时代近战、远程、重型等，未来含超级士兵 | 远程、步兵、反装甲、装甲等类别及克制，部分单位有解锁条件 |
| 防御 | 购买／出售炮塔、扩展塔位；不要假定炮塔能攻击敌方基地 | 官方攻略明确最多 4 个塔位，购买／出售与更换 |
| 资源 | 金币出兵和购买防御，经验推进时代；技能按目标版核验 | 金币还用于兵种升级；**进化与特殊技均花 XP**，频繁技能会拖慢进化 |
| 战斗收益 | 精确奖励、训练时间、难度倍率待原程序对照 | 官方攻略确认己方单位死亡也获得 XP，不是纯龟缩就没有机会成本 |
| 适合重点复现 | 出兵节奏、前后排、炮塔性价比、时代压制、战斗反馈 | 再加克制、升级、解锁、人口、技能与进化的资源竞争 |

来源：[一代作者页](https://www.newgrounds.com/portal/view/408209)、[二代发行商页面](https://www.maxgames.com/play/age-of-war-2.html)、[二代官方攻略](https://www.maxgames.com/guides/age-of-war-2.html)、[二代官方移动版](https://apps.apple.com/us/app/age-of-war-2/id1194118663)，以及上述两篇用户提供的文章截图。时代中文名称按文章用语归并。

两代均以原 Flash 单机玩法为调查基线。现代官方移动版中的 Generals 模式、付费修改模式、皮肤、成就等，应单独立项，不能默认算入经典版。二代 Newgrounds 页面上传于 2010-05-31，但作者早在 2010 年初就提到 Max Games 先行发布，因此该日期不宜写作全网首发日期。[作者完成公告](https://louissi.newgrounds.com/news/post/423820/1)、[二代作者页](https://www.newgrounds.com/portal/view/537808)

## 素材清单与可用来源

### 一代和二代都需要什么

| 类别 | 一代最低完整规格 | 二代的额外成本 |
| --- | --- | --- |
| 单位设计 | 16 种角色／载具、武器、头像及敌我区分 | 按 29 单位规模预算，新增时代和更多兵种体型 |
| 动画 | 每单位至少待机、移动、攻击、死亡；部分需分近战／远程动作 | 相同规则下动作规格从约 64 段增至 116 段，复杂载具和法术更多 |
| 基地 | 5 套时代外观、血量锚点、塔位、受击／毁坏效果 | 7 套时代外观；敌我可共用后镜像或改色 |
| 炮塔与弹体 | 15 炮塔身份、开火、子弹／石块／箭／炮弹／能量弹、命中效果 | 后续从固定 7k7k XML 核实 17 炮塔 |
| 场景 | 横向天空、远景、地面及前景装饰 | 分层更丰富；不必每个时代重新制作整套背景 |
| 技能 | 各时代技能的预警、发射、命中、持续效果、冷却表现 | 更复杂技能以及经验不足和进化竞争的提示 |
| UI | 出兵、炮塔、出售、塔位、进化、技能，金币／XP／HP，难度、暂停、胜负 | 克制说明、升级等级、解锁条件、人口和加速状态 |
| 音频 | 战斗 BGM、按钮、出兵、攻击、命中、死亡、基地受损、技能与胜负提示 | 更多武器和技能音色，必要时增加音乐 |

64／116 是“单位数 × 四种动作”的制作规划，不是从原游戏导出的动画数量；通过部件动画、镜像、共享骨骼或小图集可以减少资源。高分辨率逐帧序列会明显放大包体，不能把 SWF 的压缩体积当成 Unity 图集体积。

### 原版素材的现实情况

目前找到的官方游戏文件是 SWF，**没有核实到可直接给第三方 App 使用的、授权完整的原作 Unity 素材工程**。SWF 包含程序和媒体，文件能下载不等于可以任意拆分、改作或再分发。

Max Games 的 [Free Games For Your Website](https://www.maxgames.com/free-games.html) 面向网站托管分发，不能从该文字直接推导原美术的独立 App 授权。主题曲 Waterflame 的 [Glorious Morning](https://www.newgrounds.com/audio/listen/91476) 页面要求署名、非商业和相同方式共享，并写明商业项目联系作者。另一个 [Age of War 2 Edition](https://www.newgrounds.com/audio/listen/1141858) 是为 2017 年移动版制作的改编，应与 Flash 原声分开记录。

如果最终目标是原版观感，应先确定目标版本与素材使用范围，再建立“源 URL／固定版本／文件哈希／作者／许可范围”的清单。若选原创同类玩法，素材可以独立重做。

### 可替代素材

| 来源 | 可借鉴用途 | 限制 |
| --- | --- | --- |
| [Kenney Platformer Characters](https://kenney.nl/assets/platformer-characters) | CC0 侧视角色原型 | 不覆盖全部历史时代和战争动作 |
| [Trainee Knight](https://opengameart.org/content/trainee-knight-character) | 作者标 CC0，骑士移动／攻击／受伤／死亡动作 | 只补一个时代，需统一风格 |
| [Kenney Background Elements](https://kenney.nl/assets/background-elements) | CC0 天空、远景组合 | 需重排横向战场 |
| [Kenney Medieval RTS](https://kenney.nl/assets/medieval-rts) | CC0 建筑与战争元素原型 | 视角未必适合直接放入侧视战斗 |
| [Kenney UI Pack](https://kenney.nl/assets/ui-pack) / [Particle Pack](https://kenney.nl/assets/particle-pack) | CC0 按钮和基础特效 | 按本 app 和游戏的视觉语言调整 |
| [UI Audio](https://kenney.nl/assets/ui-audio) / [Impact Sounds](https://kenney.nl/assets/impact-sounds) / [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) | CC0 UI、命中、未来武器声音 | 仍需补足原始／中世纪／现代兵器和原创配乐 |

这些资源可以降低原型成本，**尚不构成风格统一的五／七时代完整美术包**。[Kenney 官方许可说明](https://kenney.nl/support)

## GitHub 复刻候选

检索了 `Age of War clone`、`Age of War 2 source`、`战争进化史`、Unity／Godot／LICENSE 等组合。以下六项均有实际工程文件，不是只存放 SWF 的镜像。已锁定默认分支提交、递归读取文件树，并抽查部分源码和许可证；没有运行或构建候选。**本轮未找到可核实为完整二代复刻、且授权明确的现成源码。**

| 项目 | 技术与完成度证据 | 可借鉴内容 | 接入限制 |
| --- | --- | --- | --- |
| [erupturatis/Age-of-war-unity-clone](https://github.com/erupturatis/Age-of-war-unity-clone) | Unity **2021.3.7f1**、C#；为 AI 训练制作的精简数值模拟；原始 `LICENSE` 已确认 **MIT** | `Data.cs` 的单位／塔／经验配置，`GameManager.cs` 的出兵队列、购买、时代切换，`Troop.cs` 的战斗 | 不具备完整动画表现；存在训练用途和近似参数；原作精灵不因 MIT 自动获授权。优先作为规则参考 |
| [Thommynator/AgeOfWarGame](https://github.com/Thommynator/AgeOfWarGame) | Unity **2019.4.20f1**、C#、WebGL 演示；README 明确“尚未完成” | 2D 场景、单位与 UI 组织方式 | 未找到项目级许可证；文件树中的 LeanTween 许可只覆盖该依赖。时代／技能完整度和 Unity 6 兼容性待验证 |
| [MrPio/AgeOfWarRemake](https://github.com/MrPio/AgeOfWarRemake) | Unity **6000.2.7f2**，3D 多人重制；GameManager 继承 `NetworkBehaviour` 并使用 Server RPC | 较新的 Unity 工程、角色模型、场景／技能／资源组织 | 与目标 2D 单机路线不同，有真实网络耦合；未找到项目级许可证，自制模型也需要明确复用许可 |
| [apiotrowski255/age-of-war](https://github.com/apiotrowski255/age-of-war) | Godot **4.3**、GDScript；有五时代单位、特殊技和原作风格图片目录 | 一代单位／特效的文件组织、技能与基地攻防实现 | 未找到许可证；不能把 GDScript 直接接入 Unity。抽查到 `_process` 中按帧增加治疗值，不能视为经过完整校准的原版规格 |
| [lawoflarge/age-of-war](https://github.com/lawoflarge/age-of-war) | Phaser 3＋TypeScript，Eras of War；`LICENSES.md` 已确认代码 **MIT**；当前美术为运行时形状占位 | 数据驱动单位、轻量原创同类玩法；作者说明五时代、20 单位 | 数值粗调、桌面优先，不是原版美术复刻；无需为它给现有 Unity app 引入整套 Web 层 |
| [ataberkus/age-of-war-clone](https://github.com/ataberkus/age-of-war-clone) | React＋TypeScript＋Canvas 2D，程序化音频；README 描述触屏、五时代、四档 AI、Supabase 联机 | 独立模拟／渲染／生产队列的分层思路 | 未找到许可证；功能完整度是作者声明，未做运行验证。联机部分与本项目离线范围不同 |

优先顺序：**规则参考看 erupturatis；原版风格内容组织看 apiotrowski255；Unity 现代工程组织看 MrPio；原创轻量方案看 lawoflarge。** 只有确认相应许可和来源后，才能进一步决定复制哪些文件；研究架构不等于整项目导入。

### 固定版本与体积口径

| 仓库 | 本次固定提交 | 跟踪文件总字节数 |
| --- | --- | --- |
| erupturatis/Age-of-war-unity-clone | `c6a2999193ab162298c76ca92279b4a5cc92bfd4` | 1,332,795,522 |
| Thommynator/AgeOfWarGame | `57f3a790a114c35e62d614f7274f3e7e460c89c3` | 45,110,330 |
| MrPio/AgeOfWarRemake | `6237603d1cbb0b5a08cd67f5b97de706739f70b6` | 177,710,304 |
| apiotrowski255/age-of-war | `c6f2d0f6954a5e83e28e0809115b3b3a2af04a39` | 133,375,851 |
| lawoflarge/age-of-war | `36dee40fa0467ff231538162da2959adab9c1898` | 235,802 |
| ataberkus/age-of-war-clone | `f986be20a689da243981af1a7b37b8447bc6def0` | 229,906 |

这些是 Git 树中当前版本 blob 大小之和，**不是源码净体积、下载 ZIP、Unity 构建或 APK 增量**。例如 erupturatis 含训练结果等非发布文件；Godot 工程按扩展名统计有 4,979 个图片文件、合计 121,336,354 字节，可能包含逐帧图片和辅助内容，不能当成已清理、已授权的游戏素材清单。Web 项目当前树仅约 230 KiB，也不包括安装依赖及构建后的引擎代码。统计方法及分类见[测量数据](2026-10-30-age-of-war-measurements.json)。

关键许可证和代码入口：[erupturatis MIT](https://github.com/erupturatis/Age-of-war-unity-clone/blob/c6a2999193ab162298c76ca92279b4a5cc92bfd4/LICENSE)、[其战斗配置](https://github.com/erupturatis/Age-of-war-unity-clone/blob/c6a2999193ab162298c76ca92279b4a5cc92bfd4/Assets/scripts/Data.cs)、[MrPio GameManager](https://github.com/MrPio/AgeOfWarRemake/blob/6237603d1cbb0b5a08cd67f5b97de706739f70b6/Assets/Scripts/Managers/GameManager.cs)、[Godot 主场景逻辑](https://github.com/apiotrowski255/age-of-war/blob/c6f2d0f6954a5e83e28e0809115b3b3a2af04a39/main_game.gd)、[Eras of War 许可](https://github.com/lawoflarge/age-of-war/blob/36dee40fa0467ff231538162da2959adab9c1898/LICENSES.md)。

另有 [Age-of-war-AI](https://github.com/erupturatis/Age-of-war-AI)，主要用于自动操作与 AI 训练，不能作为现成游戏模块。网页试玩、原 SWF 镜像与模拟器仓库也应分别分类，避免把“能在线玩”误记为“有可移植游戏源码”。

## 接入现有 app 的代码边界

| 当前事实 | 需要增加的工作 | 本仓证据 |
| --- | --- | --- |
| 已有 Unity、IL2CPP、Android ARM64、UGUI、音频和 Newtonsoft | 原生方案共享运行时，无需再装一套引擎 | [ProjectBuild.cs](../../Assets/Editor/ProjectBuild.cs)、[manifest.json](../../Packages/manifest.json) |
| 构建入口明确强制竖屏并关闭横屏 | 战争页独立横屏根布局，进入和离开时切换／恢复方向，处理安全区与系统返回 | `ProjectBuild.cs:208–214`；[UiFactory.cs](../../Assets/Scripts/Runtime/UiFactory.cs) 的 720×1320 布局 |
| AppController 显式管理各页，没有通用游戏注册系统 | 增加大厅入口、进入／离开／保存／暂停／设置／返回接线；不为这次研究重写整个宿主 | [AppController.cs](../../Assets/Scripts/Runtime/AppController.cs)，`Save`、`StopAnimation`、`Update` 中的返回处理及焦点／暂停回调 |
| Core 不依赖 Unity，每款游戏独立持有规则和存档 | 新建 `Core/AgeOfWar` 战斗、经济、AI、版本规则；`Runtime/AgeOfWar` 负责显示、音效和触控 | [架构与存档契约](../architecture.md) |
| 现有共用音效主要服务 2048 和跳一跳 | 共用声音／触感开关；战争 BGM、音效池及其暂停释放由战争页拥有 | [GameFeedback.cs](../../Assets/Scripts/Runtime/GameFeedback.cs) |
| 已有延迟构建和局部资源加载的示例，但隐藏页面不等于卸载纹理 | 进入时加载、按时代管理图集、退出后释放或有上限地缓存，避免所有游戏资源常驻 | [WatermelonPage.cs](../../Assets/Scripts/Runtime/Watermelon/WatermelonPage.cs)、[IdiomCatalogLoader.cs](../../Assets/Scripts/Runtime/Idiom/Resources/IdiomCatalogLoader.cs) |
| 当前仓库没有 Ruffle／WebView 容器 | Flash 路线另建 Android WebView 桥、本地资源映射、Unity 焦点与音频生命周期 | `Assets/Plugins/Android` 与 `Packages/manifest.json` 的静态检查 |

建议两代共享狭窄的战斗核心，通过独立规则集表达时代、兵种、升级、资源消耗和 AI 差异，不把二代规则写成散落在公共宿主里的判断。单线前进和范围攻击优先用固定步长逻辑处理，无须为了“战争游戏”先引入完整 RTS 导航或联网框架。

原生续局应使用独立的 `age-of-war-v1.json` 一类文件，并明确一、二代各自的对局槽；此名称只是方案，尚未实现。必须保存双方时代、金币／XP、基地血量、单位稳定 ID／位置／HP／冷却、训练队列、投射物、技能、升级、AI／RNG 和胜负结算标志。只存金币和时代不能恢复正在进行的战斗。继续沿用严格 schema／类型校验、临时文件替换、合法备份恢复，不改 2048 的旧存档。

进入设置、失焦、后台和返回大厅时应停模拟、取消尚未提交的输入、停音频并保存已提交状态；恢复后不补算离线战斗时间。尤其需要验证出兵扣款、击杀奖励和终局记录不会因恢复重复发生。原子写盘及周期保存可参考西瓜模块，具体战争字段和保存节奏由新模块拥有。

## Unity 原生实现的增量预算

以下为规划假设：离线单机、横屏 2D、完整主体玩法、手机可用 UI、独立续局、同一现有 Unity 运行时；不包括将军模式、内购、联机、广告或逐帧原版一致性承诺。资源采用图集／部件复用，避免无节制导出高清逐帧 PNG。

### 包体和内存

| 指标 | 一代 | 二代单独接入 | 两代共享实现 |
| --- | --- | --- | --- |
| APK 增量预算 | **20–45 MiB** | **40–90 MiB** | **50–110 MiB** |
| 安装占用增量预算 | 22–55 MiB | 42–100 MiB | 52–120 MiB |
| 进入游戏后增量内存工作预算 | 40–90 MiB | 55–130 MiB | 60–140 MiB，只运行和驻留一部 |

包体模型：每页 2048²、8 bit/pixel、无 mip 图集约 4 MiB；一代先预留 4–8 页，二代 8–16 页。分别加音频 3–8／6–16 MiB，以及编译代码、配置和场景 1–4／2–6 MiB。两代共同实现的预算假设可复用基础资源和音频。**页数和编码是尚未制作素材前的假设，不是对原作资产导入后的测量。** 实际 APK 还受构建压缩影响。[Unity 纹理格式说明](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-formats-reference.html)

安装预算假设 APK 内资源不再完整复制到用户目录，另留约 2–10 MiB 缓存／存档余量；没有可靠的“安装大小固定为 APK 的几倍”。内存预算包含当代纹理、解码音频、单位与弹体对象和渲染余量，不是 Android PSS 实测。若纹理全部常驻且回退到 RGBA32，仅一代图集就会变成 64–128 MiB，二代 128–256 MiB，上表内存预算随即失效。手机是否支持选定 GPU 压缩格式应在导入方案和真机验证中落实。

当前仓库的 [v0.4.0 发布记录](../releases/v0.4.0.md) 记载六款游戏正式 APK 为 **54,942,815 B，52.4 MiB**。这是历史包，不是当前含扫雷源码的实测基线；不能直接给出“本次接入后最终为多少 MB”的确定值。

### 工程量

估算按一名熟悉当前项目的 Unity／C# 工程师，以 8 小时为一个工程人日。包含接入与设备回归，假设已有可用素材输入，**不含绘画、动画制作、配乐与授权等待**。

| 工作 | 一代 | 二代单独实现 |
| --- | --- | --- |
| 战斗、经济、AI 和升级规则 | 8–12 人日 | 14–22 人日 |
| 战场、动画接线、HUD、触屏 | 10–16 | 14–22 |
| 大厅、横屏和宿主接线 | 3–5 | 3–5 |
| 完整存档、生命周期、音频管理 | 4–6 | 5–8 |
| 内容配置和数值调试 | 6–10 | 10–16 |
| 回归、性能和设备适配 | 6–10 | 10–16 |
| **合计** | **37–59 工程人日** | **56–89 工程人日** |

两代共享内核和工具，合计先预留 **70–110 工程人日**；这是完整接入的工作分解预算，不是根据代码行数或仓库大小换算，也不等于 AI 生成代码的耗时。若从头制作统一美术，可另预留一代约 15–35、二代约 25–50 美术人日；该项置信度低，应在确定单位动作与画面质量后重新询价。仅做少量单位的可玩原型会短得多，不能与完整五／七时代交付混用。

## 原版文件与 Ruffle 的体积实测

本节是前期发行商文件及 Ruffle 官方包的测量，当时只做静态分析。后续已将 7k7k 固定样本保存到 Downloads 并完成本地战斗验证，见后续报告；未导入 Unity 或把原作素材加入仓库。完整字节数、URL、SHA-256 与压缩模型见[测量 JSON](2026-10-30-age-of-war-measurements.json)。

| 对象 | 文件体积 | 观察 |
| --- | --- | --- |
| 一代官方 SWF | 3,628,478 B，**3.460 MiB** | CWS，SWF 9；FileAttributes 的 AS3 标志为 false，顶层有 DoAction；声明帧率 40 |
| 二代官方 SWF | 11,413,675 B，**10.885 MiB** | CWS，SWF 10；AS3 标志 true，顶层有 DoABC；声明帧率 30 |
| 两代 SWF 合计 | 15,042,153 B，**14.345 MiB** | 二代文件约为一代的 3.15 倍 |
| Ruffle v0.7.1 官方网页 ZIP | 10,527,427 B，约 10.04 MiB | 展开 30,739,514 B，含源码映射、两个 WASM 和配套 JS |
| Ruffle 运行文件与许可 | **27.876 MiB 展开／9.693 MiB 模拟 ZIP** | 保留全部 JS、WASM、许可证；去掉 `.map`、README 和包元数据 |

来源：[一代官方分发页](https://www.maxgames.com/play/age-of-war.html)、[二代官方分发页](https://www.maxgames.com/play/age-of-war-2.html)、[Ruffle 官方下载页](https://ruffle.rs/downloads)、[v0.7.1 网页包](https://github.com/ruffle-rs/ruffle/releases/download/v0.7.1/ruffle-0.7.1-web-selfhosted.zip)。SWF 的 AS3 位判定与 [Ruffle v0.7.1 类型定义](https://github.com/ruffle-rs/ruffle/blob/v0.7.1/swf/src/types.rs) 对照；这里只识别格式，不代表验证了运行兼容性。

### 离线承载的增量模型

下表用 Python `zipfile` 的 Deflate level 6，把运行资源与原 SWF 原样打包。两个 WASM 变体均保留，没有假定某个可以删除。

| 承载内容 | 模拟 ZIP | 未压缩资源合计 |
| --- | --- | --- |
| Ruffle 加一代 | **13.15 MiB** | **31.34 MiB** |
| Ruffle 加二代 | **20.58 MiB** | **38.76 MiB** |
| Ruffle 加两代，共享一个运行时 | **24.04 MiB** | **42.22 MiB** |
| 已有一代和 Ruffle，再加二代 | **约 10.89 MiB** | **约 10.88 MiB** |

这些是可复核的资源模型，**不是 APK 实测**。Android 构建可能压缩或直接存储资源，还涉及桥接代码、打包元数据和页面文件。两代完整离线承载可暂留 **25–45 MiB APK 增量预算**，最终以相同构建配置下的前后 APK 为准。若首次运行还把资源从 APK 复制到可写目录，设备磁盘会另增加最多约 42.22 MiB 的资源副本；是否复制取决于本地资源加载方案。

Ruffle 采用 MIT／Apache 2.0，但这是播放器的许可。官方兼容性说明仍列有 AVM1／AVM2 差异和未实现 API，且我们的两部文件恰好分属不同 VM。因此必须分别验证，不能用一代正常运行推断二代正常。[Ruffle 项目](https://ruffle.rs/)、[兼容性说明](https://ruffle.rs/compatibility)

网页嵌入方案还需要：本地 WASM／SWF 加载、横屏安全区、触摸模拟鼠标、按钮说明替代 hover、声音手势解锁、应用后台暂停、返回大厅停止音频，以及外链和联网调用的处理。保留原界面不自动解决手机上的小按钮问题。**暂停播放器、保留 SharedObject、保存任意时刻完整战场是三个不同能力**，未找到证据证明现成桥接已满足本 app 的完整续局契约。

Android 可通过本地资源映射提供离线网页内容；`WebView.onPause()` 不会自行暂停所有 JavaScript，`WebView.saveState()` 也不是游戏虚拟机快照。必须使用所锁定 Ruffle 版本的相应接口并验证杀进程恢复。[Android 离线内容](https://developer.android.com/develop/ui/views/layout/webapps/load-local-content)、[WebView.onPause](https://developer.android.com/reference/android/webkit/WebView#onPause())、[WebView.saveState](https://developer.android.com/reference/android/webkit/WebView#saveState(android.os.Bundle))

这条路线先预留 **2–4 人日**做两代兼容性原型；若主体玩法均正常，再预留 **10–20 人日**做宿主集成与设备回归。合计约 12–24 工程人日，但**不包含修 Ruffle 引擎、改原 SWF 或实现任意战场快照**；遇到这些问题需重新评估，不能用此预算承诺与原生方案相同的续局能力。

Ruffle 内存目前不能从下载文件大小反推。可先用单局额外 **100–400 MiB** 作宽范围容量规划，包含 WebView、WASM／JS 堆、解码媒体和渲染；置信度明显低于静态文件测量。实施时应统计 Unity 与相关 WebView 进程，分别测进入、后期大量单位、后台和返回大厅后的残留，而不是只读 Unity Profiler 的托管堆。

## 验证范围

本文件对应的前期阶段完成文章截图阅读、官方资料核对、公开仓库静态调查、当前 App 源码分析和文件体积测量。后续阶段另完成反编译、素材抽样和桌面本地战斗验证，见后续报告。仍未执行 Unity 导入、Android 构建或设备回归；工作量和原生包体均为带假设的预算，不是已交付能力。

后续若进入实施，先用一代的单时代完整战斗作为验证切片：近战／远程／重型单位、双方基地、一个炮塔、一个技能，覆盖胜负、暂停、后台和进程恢复。用同一签名、架构、纹理与构建模式比较 APK，并测量战斗峰值及退出后的内存，再展开五时代与二代。
