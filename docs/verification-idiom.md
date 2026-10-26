# 成语接龙开发验证记录

日期：2026-10-26。本页保留开发阶段独立测试包记录；后续正式签名包与覆盖升级结果见 [v0.3.0 发布记录](releases/v0.3.0.md)。实现范围见 [玩法与页面](idiom-game.md)，数据处理见 [词库说明](idiom-data.md)。

## 工作树与安装隔离

- 当前工作树：`/Users/bytedance/.codex/worktrees/20ab/XanaduYonder`，基线 `42896603043a2a4b8dc4d8c3490f2399880adc5f`，保持 detached HEAD。
- 仅修改、导入和构建当前工作树；未切换、合并或推送数独、跳一跳诊断等其他工作树的分支。未复制其他工作树的 Library/Temp。
- Android 开发包使用独立标识 `com.duanyao.xanaduyonder.idiomtest`，显示名“Xanadu 成语测试”；正常构建的标识、图标和启动画面保持原配置。测试升级保留独立包数据，不覆盖手机原应用。
- 物理设备：OPPO PKM110，1080 × 2373。补充模拟器：当前任务独立的 Android 36 ARM64 AVD，1080 × 1920，端口 5586。未操作已有端口 5554 的其他任务模拟器。

## 自动检查与构建

| 范围 | 结果与证据 |
| --- | --- |
| 词库清洗 | `scripts/test-idiom-data.sh`，52 项通过；含规范化、跨库及单库重复、冲突和字体覆盖。 |
| 接龙规则 | `scripts/test-idiom.sh --catalog artifacts/idiom-cleaned/game-index.json`，26 项通过；双模式、开局、跨双方去重、提示复用/第三次结束、随机状态与严格恢复。完整词库 16 局共 15,797 条消息，最长 1,001 条。 |
| 对局与生词本存档 | `scripts/test-idiom-persistence.sh`，24 项通过；含坏档保护、备份、归档失败、版本变化、JSON 严格验证、容量及写失败。 |
| 运行资源 | `scripts/test-idiom-resources.sh`，5 项通过；全部 42,851 条目录唯一，分片引用/义项/拼音对应，所有资源校验和一致。 |
| Unity 实际加载 | `XanaduYonder.EditorTools.IdiomResourceChecks.Run`，实际异步 Resources 加载及 42,851 条释义读取通过；40 局完整词库对局和恢复通过。 |
| Android 桥接 | Java 源码以 API 36 的 android.jar 编译通过；Unity IL2CPP Android 构建通过。 |
| 既有游戏回归 | 172 项通过：2048 Core 43、Input 10、Persistence 9；跳一跳 Core 25、Persistence 18；旸了个旸 Core 31、Persistence 26、Layouts 10。开发前基线和集成后均通过。 |

自动用例共 279 项。日志位于当前工作树 `artifacts/logs/test-idiom*.log`、`regression-*.log`、`idiom-resources.log`、`build-idiom-validation.log`。桌面 .NET 对局计时不作为真机耗时。

Unity 使用 `6000.3.27f1`。独立包构建命令：

```bash
./scripts/build-idiom-validation.sh
```

资源检查运行 Unity 时不加 `-quit`，使用 `-executeMethod XanaduYonder.EditorTools.IdiomResourceChecks.Run`，由检查回调退出；同一工作树不能同时启动两次 Unity。

## 交互与视觉验收

- 大厅保持 350 设计单位的卡片高度、24 单位间距，通过滚动访问成语接龙。米白/绿色、圆角按钮、公共弹窗与已有游戏一致。
- 两种模式各自续局；机器开局与人先开始均已在设备执行。人先开始的首词正确计入玩家数量。
- 真机使用中文九键输入法完成拼音候选选字“一心一意”，未把 ADB 英文注入当作中文输入验收。输入法确认/回车不自动发词；点发送后出现右侧用户词和左侧 APP 回复。
- 键盘保持打开时，单次点击发送即可完成一回合。修复了 UGUI 在按下时收键盘、输入栏重排导致抬起时按钮点击丢失的问题；物理手机与短屏模拟器均已复验。
- 前两次提示可填入后发送，同一回合反复打开不多计；第三次直接显示鼓励小结，保留候选详情，玩家数量不包含 APP 或未提交候选。
- 聊天、提示、小结、生词本共用详情弹窗；加入后显示已加入，跨模式共用收藏。从详情回到原提示/小结，从生词本回到原页面。
- 短屏验证了 1,001 条真实合法历史（玩家 500、APP 501）的计数、滚动和点击对应释义；拖动未误开详情。此项是操作验收，没有给出 FPS 保证。
- 模拟器完成 2048 滑动出新块、跳一跳得分、旸了个旸取牌及各自大厅往返；成语存档前后字节一致。成语自动存档不改其他游戏文件。

截图和状态快照保留在 `artifacts/idiom-device/`。关键示例：`physical-first-turn.png`、`physical-ime-enter-no-submit.png`、`physical-after-single-send.png`、`short-fixed-third-hint-summary.png`、`short-long-summary.png`、`short-long-row-detail.png`。

## 最终安装包

`artifacts/builds/XanaduYonder-0.3.0-idiom-validation.apk`，71,518,881 字节（约 68.2 MiB）。仅为独立标识的本地开发测试包，未发布 Release。

SHA-256：`cd94a38d3b9f01597e3947d71202bdd3ade7151f7bd4f8f6ff25a8e271bdea3c`，同目录附 `.sha256` 文件。

## 恢复、离线与存档隔离

- 原生键盘的 Home/Back 回写旧文字问题已修复：切后台前结束键盘会话，并在 UGUI 同步取消文本的两个阶段保护当前草稿。最终包在 OPPO 与独立模拟器均验证新增草稿在 Home/Back 后保留、页面不误退；两设备继续编辑并单次发送有效词成功，玩家数量正确、草稿清空。证据为 `physical-nativefix-*` 和 `short-final-*`。
- 在独立模拟器中，对运行中的成语主档写入损坏内容，再通过页面菜单归档并保存当前状态；恢复后双模式 JSON 与注入前一致，1,001 条历史和玩家 500 个计数完整。损坏原文和原备份均逐字核对保留。
- 对已加载生词本的主档做同样损坏测试；首次移除操作报告保存失败且保留已有词，恢复确认后当前 1 个收藏原样保存，不静默清空或顺带执行删除。证据为 `short-runtime-*` 和 `short-runtime-archives/`。
- 禁用自有模拟器 Wi-Fi/数据后，系统确认无默认网络且路由为空；强制停止测试包并冷启动，实际载入词库、历史和“一夕一朝”详情成功。网络设置随后恢复。证据为 `short-offline-*`。
- 该模拟器 1,001 条历史及一个详情场景的单次内存采样为 PSS 257,251 KB（约 251 MiB）、RSS 387,444 KB（约 378 MiB）。这是具体场景采样，不是峰值，也不代表物理手机性能。

模拟器故障注入后已还原正常成语/生词本记录，并在完成验证后关闭本任务的 5586 模拟器；全部 10 个主/备份存档逐字对照一致。详细设备步骤见本地 `artifacts/idiom-device/short-qa-report.md`。

## 数据和边界

运行词库版本 `d3a64eb110d46b49c1669d53`：42,851 条可玩成语、431 条人工整理的推荐常用词，目录/168 个详情分片/许可/清单合计 11,687,267 字节（约 11.1 MiB）。安装后规则、提示与释义均来自本地，不调用在线大模型。

清洗自动检查通过不代表逐条人工考据完成；未能安全确定读音/义项的词不进入可玩子集。当前验收覆盖上述 OPPO 和 Android 36 模拟器；未验证全部 Android 厂商、系统和输入法，也未进行 iOS 构建。没有把模拟器操作或桌面耗时作为所有真机性能保证。
