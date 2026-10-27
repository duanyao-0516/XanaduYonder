# 成语选词与个人学习验证（2026-10-27）

## 交付内容

当前工作树实现了加权随机选词、生词复习、仅人机积累的个人熟悉词、“我记住了”、当前回合辅助来源，以及四对局/生词本/个人资料统一原子保存。公共常用表从 431 扩充到 1,534 个。完整决策见 [选词设计](idiom-selection.md)，词表范围与审计见 [常用词库](idiom-common-vocabulary.md)。

工作区为 `20ab/XanaduYonder`，分支 `p/duanyao.0516/feature/idiom-local-versus`，在已有双人/UI 工作之上完成。其他 checkout 没有被切换、写入或构建；前后 `git worktree list --porcelain` 内容一致。当前任务的测试包使用独立包名，未覆盖标准应用或其他工作区的测试包。

## 自动检查

| 检查 | 结果与边界 |
| --- | --- |
| `scripts/test-idiom.sh` | 45 项通过：候选质量、正权随机、80/20 分支、24h 边界、近期降权、2+1 提示、独立随机数、辅助/熟悉/生词边界、双人例外与恢复。 |
| `scripts/test-idiom.sh --catalog artifacts/idiom-cleaned/game-index.json` | 含真实词库检查共 46 项通过；42,851 词、公共 1,534。16 个人机局与 4 个双人长局，合计 6,458 条消息；4 个双人局各 1,000 条历史可恢复。 |
| `scripts/test-idiom-persistence.sh` | 34 项通过：四槽位、学习/生词身份一致性、坏档保护、原子失败回滚/重试、辅助及双随机状态、推荐穷尽历史恢复；旧文件不读不改。 |
| `scripts/test-idiom-resources.sh` | 16 项通过，42,851 个详情精确引用全部通过；重复打包 172 个资源文件字节一致。 |
| `scripts/test-idiom-ui.sh` | 真实 Unity Canvas/GraphicRaycaster 共 1,426 项断言通过；720×1280、720×1582、900×1582，包含居中、边界、镜像发送、圆形关闭、遮罩、记住/移除、失败事务及返回链。 |
| `IdiomResourceChecks.Run` | Unity 实际异步加载 42,851 份精确详情，80 局字/音×人机/双人玩法及恢复通过。 |
| 其他游戏回归 | Core 43、输入 10、通用保存 9、跳跃 25、跳跃保存 18、三消 31、三消保存 26、三消布局 10 项，合计 172 项通过。 |
| Android 构建 | `scripts/build-idiom-validation.sh`，Unity 6000.3.27f1、ARM64 IL2CPP Development 构建成功。 |

两个独立审查均完成。UI 审查发现“成功保存仍留错误提示”和“恢复确认丢失来源返回链”，先用真实 UI 检查复现后修复。预期失败日志分别为 `idiom-learning-recovery-red.log`（第 469 断言）和 `idiom-learning-recovery-return-red.log`（第 471 断言），最后完整检查通过。另补充从未熟悉的词失败“记住”不泄露 F，以及打开生词释义、恢复、提交后仅累计辅助使用的断言。

规则概率与 24 小时边界使用固定种子/注入时钟测试，不声称在设备上等待了 24 小时。完整词库中的“同字一望无际”会在空个人资料下中性结算；显式熟悉符合质量条件的“际…”词后实测 3 种回复，同音默认实测 8 种回复。没有把稀有词偷偷标为公共常用来绕过门槛。

## 模拟器操作

只使用本任务 `emulator-5586` / `XanaduIdiomValidation20abApi36`，未操作其他任务的 `emulator-5554`。验证 1080×2373 和 1080×1920，结束后恢复物理分辨率并关闭本任务模拟器。

为稳定重现概率与输入来源，使用实际 Core 和 Codec 生成隔离测试存档，预置待提交草稿与生词。随后通过真实界面点击、读回保存文件核对；预置草稿不冒充手工中文键入。

- 人机提交“一心一意”后，独立使用计数为 1，加入 F；种子 3 的该场景 APP 从 N 选出“意味深长”，记录展示时间而不自动移出 N。
- 从生词本查看“意味深长”，点击“我记住了”：N/列表清空，F 加入该词。强制停止并冷启动恢复后，对局和个人状态保持。
- 提示关闭再打开，整份当前对局快照相同，提示次数仍为 1，APP 随机状态未消耗。选择“长年累月”，真实键盘弹出且发送可点击；提交只累计辅助使用，不进入 F。后续推荐用尽正常进入中性小结。
- 生词本查看“一心一意”后直接移出，N/F 都为空；当前回合辅助标记保留。冷启动恢复后发送预存草稿，辅助使用为 1、独立使用为 0、F 为空。
- 双人左方提交“一心一意”后转到右方，发送/提示位置换边；个人 F、使用统计、APP 展示记录均为空。
- 短屏查看详情、加入生词本，按钮/正文滚动/来源/关闭范围正常；本次还检查了真实键盘展开后发送、遮罩关闭、Home 返回和冷启动。字/音、双人及三次提示的完整规则由 Core、资源运行和此前双人验证共同覆盖。

截图与读回快照位于 ignored `artifacts/idiom-learning-device/`：`review-reply.png`、`review-definition.png`、`remembered.json`、`cold-restored.json`、`hints-reopened.json`、`hint-filled-keyboard.png`、`hint-submitted.json`、`removed.json`、`definition-assisted-submit.json`、`local-after-submit.json`、`short-added.png`。

## 手机安装与核验

OPPO PKM110（`3B6F4NE8A66ZXTRL`）覆盖安装同一独立测试包成功。实际打开大厅与成语页，由 APP 开局得到“一见如故”，打开释义并加入生词本，再点击外侧遮罩返回。读回统一保存文件，Notebook 与 Profile 均记录了相同 ID 和加入时间。

- 应用名称：**Xanadu 成语测试**。
- 包名：`com.duanyao.xanaduyonder.idiomtest`。
- 构建文件：`artifacts/builds/XanaduYonder-0.3.0-idiom-validation.apk`。
- 大小：**71,556,068 字节**（约 71.6 MB）。
- SHA-256：`918c601627d59928cad9de8ea81ad66c54a3a7c5e46f68f2379d3aac3b49a2fb`。

模拟器与手机安装目录中 APK 的 SHA-256 均与上述文件相同。测试前已备份各自独立测试应用的数据；两端的 `xanaduyonder-v1.json`、`jump-v1.json`、`sheep-v1.json`、`idiom-v1.json`、`idiom-notebook-v1.json` 均与测试前字节一致。新功能使用新的成语学习文件，未做旧协议迁移。

本轮设备日志按当前应用 PID 过滤，均未匹配 Exception、FATAL、save failed 或 load failed；手机全量历史日志中的其他日期/进程错误不作为本版本错误。手机只做上述安装与基础操作核验，不将其描述为完整机型兼容测试。
