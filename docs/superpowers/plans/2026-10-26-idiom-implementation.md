# 成语接龙开发实施计划

> **For agentic workers:** Use subagent-driven development with explicit file ownership. Follow the approved `docs/idiom-game.md`; tests exercise production sources. Do not modify any other worktree or branch.

**Goal:** 在当前独立工作树完成离线成语接龙、双模式双开局、三次提示小结、生词本和一致的移动 UI，并产出可验证的 Android 开发包。

**Architecture:** Core 只拥有词库索引和对局规则；Runtime 拥有资源加载、页面、键盘与独立持久化。AppController 只接入大厅、生命周期和公共模态容器；既有游戏规则及存档格式不变。

**Tech Stack:** Unity 6000.3.27f1、UGUI、C# 7.3 兼容代码、已有 Newtonsoft.Json、Android IME 区域桥接、Python 资源导出脚本。

## 隔离与验证

当前工作树 `/Users/bytedance/.codex/worktrees/20ab/XanaduYonder`，基线 `4289660`，外部管理的 detached HEAD。数独及跳一跳诊断所在工作树不写入、不切分支、不合并。不复用它们的 Library/Temp 或构建目录，不结束它们的进程。所有产物在当前树的 `artifacts/`。

手机验收使用独立应用标识 `com.duanyao.xanaduyonder.idiomtest` 的开发包，正式构建仍保留原应用标识。构建配置在本树切换并恢复；验证包不覆盖手机已有正式包及其他分支的安装。

## 模块接口（各负责人遵守；变化先通知调用方）

`XanaduYonder.Core.Idiom`：

- `IdiomMode { SameCharacter=0, SameSound=1 }`；`IdiomActor { None=0, App=1, Player=2 }`。
- `IdiomEndReason { None=0, HintLimit=1, PlayerNoMoves=2, AppNoMoves=3, EndedByPlayer=4, DictionaryExhausted=5 }`。
- `IdiomEntry(id, word, pinyin, firstKey, lastKey, senseId, detailChunk, common)`，只读 `Id/Word/Pinyin/FirstKey/LastKey/SenseId/DetailChunk/Common/FirstCharacter/LastCharacter`。汉字按 Unicode scalar 处理，不把代理对当两个字。
- `IdiomLexicon(IEnumerable<IdiomEntry>, string version)`；`Count/Version/Entries`；`TryGetById(id,out entry)`、`TryGetWord(word,out entry)`。
- `IdiomGame(lexicon, mode, uint seed)`；`IdiomGame(lexicon, IdiomSnapshot)` 严格恢复；`Mode/FirstActor/EndReason/IsStarted/IsEnded/PlayerCount/HintCount/History/LastEntry/CurrentHint`；`Start(actor)`、`Submit(text)` 返回 `IdiomActionResult {Success,Error}`；`RequestHint()` 返回 `IdiomHint`；`Finish()`；`GetSnapshot()`。
- `History` 为只读词条 ID 列表，角色按 FirstActor 和序号派生。`IdiomHint` 包含 `TurnIndex`（提示时历史长度）、只读 `EntryIds`。
- `IdiomSnapshot` 保存 `Mode/FirstActor/EndReason/RandomState/History/Hints`，防御复制；错误码 `None/NotStarted/AlreadyStarted/AlreadyEnded/InvalidWord/UnknownWord/RepeatedWord/WrongStart`。

`XanaduYonder.Runtime.Idiom`：

- `IdiomCatalogLoader.Load(Action<IdiomLexicon>, Action<string>)` 返回 IEnumerator；`TryGetDefinition(id,out definition,out error)`；`ClearDetails()`。`IdiomDefinition` 有 `Id/SenseId/Word/Pinyin/Meaning/Stories/Origins/Examples/SourceLabel`；引用对象 `IdiomCitation {Text,Reference}`。
- `IdiomSaveData { ActiveMode, Character, Sound }`；`IdiomModeSave { Game:IdiomSnapshot, Draft:string, Best:int, NewBestInRun:bool }`。
- `IdiomSaveStore(directory,lexicon)`：`Load(out warning)`、`TrySave(data,out error)`、`CanWrite/NeedsRecovery/SavePath`、`TryResetProtected(out error)`。
- `IdiomNotebookData { Entries:List<IdiomNotebookItem> }`；条目 `Id/SenseId/Word/Pinyin/Meaning/DictionaryVersion/AddedAt`，AddedAt 为 UTC Unix 秒。`IdiomNotebookStore(directory)` 提供同样 Load/TrySave/CanWrite/NeedsRecovery/TryResetProtected 接口。
- `IdiomPage.Initialize(root, goLobby, canInteract, setStatus, openModal, closeModal)`；`openModal` 类型 `Func<string,float,Action,RectTransform>`，返回带标题的居中公共面板，内容从 y=104 开始；`closeModal` 关闭而不调用回退。页面提供 `Enter/Leave/Suspend/PauseForBackground/Save/CheckEndState/HandleBack`、`BestLabel/SaveError/Changed`，`HandleBack()` 返回是否消费。
- 页面通过 `IdiomKeyboardViewport` 获取键盘可见性/前台状态/底部遮挡并执行收键盘；具体公共属性由主负责人补充，页面 UI 不直接依赖 Android 类。

## 责任范围与任务

- [x] 1. 运行既有 Core/Input/Persistence/Jump/Sheep 测试作为基线，记录结果；不触碰其他工作树。
- [x] 2. Core 负责人先写 `Tests/Idiom` 的失败用例，再实现 `Assets/Scripts/Core/Idiom/`：双模式、开局、跨双方去重、同回合提示复用、第三次结束、确定性回复、完整回合与恢复校验。
- [x] 3. 存档负责人先写 `Tests/IdiomPersistence`，实现 `Runtime/Idiom/Storage/`：两个模式、严格 JSON、备份恢复、保护坏档、独立生词本及失败不假报成功。
- [x] 4. 页面负责人实现 `Runtime/Idiom/Presentation/`：开局、复用气泡、输入、提示、详情、小结、生词本，所有外部文本关闭富文本，保持 UI 风格一致。
- [x] 5. 主负责人导出压缩目录/详情与推荐词表，新增 Runtime 加载器及最小 Android 键盘桥接；用当前完整词库做格式、引用与实际加载验证。
- [x] 6. 主负责人接入本工作树 AppController 的列表式卡片位置、可定高弹窗、返回/保存/前台 IME 失焦处理；原有游戏规则不变。
- [x] 7. Unity 编译及独立测试包构建；手机验证双模式双开局、真实中文 IME、聊天滚动、第三次提示、小结/详情返回、生词本和恢复；短屏布局补充验证。
- [x] 8. 做独立代码审查，修复有依据的问题；回归既有游戏和保存文件隔离，更新玩法与验证文档，提供当前工作树中的 APK。

## 验收边界

只有工具实际完成的测试、构建和设备操作可记为通过。若手机锁屏或 IME 需要用户输入，保持已完成的独立工作并明确需要的操作。不把 ADB 文本注入当作中文选字验证，不把数据结构检查当作真机 UI 验收。
