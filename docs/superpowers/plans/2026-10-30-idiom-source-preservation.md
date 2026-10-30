# 成语多来源原文保留实施方案

> 使用 writing-plans、test-driven-development 和 subagent-driven-development 工作流执行。用户已明确批准：不裁定释义正误、冲突不删除词条、各来源原文分别展示。最新要求：所有合法词保留选择机会，第三次提示后仍继续对战。

**目标：** 四字成语不因释义、读音差异被整词排除；释义及生词本保留各来源资料，接龙依据可机械识别的读音而不猜补。

**架构：** 稳定词 ID 与来源资料分离。Python 清洗保留全部源记录，Core 只持有四字正文和可解析的成对首尾音，Runtime 按来源展示，Notebook 保存完整带来源的文本快照。

**技术栈：** Python 标准库、纯 C#/.NET 10、Unity 6000.3.27f1 UGUI、Newtonsoft.Json。

## 已确定契约

- 全量 49,676 个唯一词保持在清洗全量目录；45,776 个四字词全部进入同字游戏。非四字词不改变四字接龙的玩法边界。
- 不选权威释义、不合成标准解释。新增 `sourceVariants` 保存每条源记录独立的来源、原拼音、释义、典故、出处、例句；原有 `senses` 仅保留审计用途，不作为展示答案。
- 原 `quality.reasons` 保留为审计信息，只有正文不满足四汉字时不进入四字游戏。显式隔离表也不再禁用四字词。
- `gameReadings` 收集各来源能通过现有四音节解析的读音，允许无调原文；不猜字母、不更改显示文字。没有可解析读音仍保留词条，同音提交给出明确读音不可用提示。
- 移动目录扩展为 9 列：`[id,word,pinyinDisplay,firstKey,lastKey,definitionId,chunk,common,[[first,last],...]]`。前两个单音键仅作旧接口兼容展示；实际索引/接龙使用第九列的音对。无读音时两旧音键为空、音对为空。
- `pinyinDisplay` 为各来源不同的原拼音按稳定来源顺序展示；详情逐来源完整呈现。旧单义 `senseId` 可继续作内容身份，多义使用稳定的资料集合 ID，不代表主释义。
- 详情保留既有根字段以兼容测试和旧展示组件，新增 `Sources`：`SourceKey, SourceLabel, SourceRecordId, Pinyin, Meaning, Stories, Origins, Examples`。根 Meaning 为带来源标记的全部词义，根典故/引用数组不混合来源。
- 多音不能拆为任意首尾组合。每回合仅保留与上一尾音匹配的读音的尾音；历史恢复、提示、候选后继检查遵循同一状态。
- APP 与提示不因普通后继为零而过滤合法词。APP 在优先池与其他合法池均非空时按 95%/5% 分支，优先分支内普通/到期复习按 80%/20%；池为空即使用另一池。冷却影响优先身份，不删除资格。提示前两项优先普通，第三项优先生词且有 5% 其他合法探索，空位全库补足。
- 最新用户规则覆盖原约定：每位人类玩家每局最多 3 组新提示，第三组仍能选择、发送并继续。用满后按钮改为认输（Surrendered=7）；双人允许合计 6 次提示。只有全库无合法词或人主动认输/结束才结算，不再新建 HintLimit/RecommendationExhausted。旧枚举仅用于恢复已有历史。
- 生词本新加入时 `Meaning` 保存完整 `DefinitionText(..., true)`，包括所有来源原文和引用；不截断。继续使用已有事务保存和长度上限，超限明确报错，不静默丢来源。
- 沿用此前用户“不需要旧存档兼容”的范围：新目录版本仍由现有版本保护处理，旧档不自动清空、不绕过校验；不在本次增加跨目录对局迁移。

## 执行与验证

- [x] 数据：先增加当仁不让、多义、多读音、无解析读音仍保留四字词的失败测试；再调整 `merge.py`、`import-idioms.py`、`pack-idioms.py` 与资源检查。
- [x] Core：先增加普通后继为零仍可回复、全库兜底、提示兜底、多读音不能跨配对、无读音同字可用的失败测试；再修改 `IdiomLexicon.cs`、`IdiomGame.cs` 与真实词库检查。
- [x] Runtime：增加多来源格式与生词快照的失败测试；更新 DTO/loader/link.xml、按来源渲染、顶部拼音布局和保存全文。
- [x] 资源：用固定源重新导出到新的 ignored 清洗目录；重新打包所有详情，重新计算词表覆盖报告；验证旧词无丢失、所有来源文字可追溯和重复打包一致。
- [x] 文档：更新当前数据数量、算法、清洗规则与版本保护说明，不将旧验证结果冒充新结果。
- [x] 集成：执行数据、资源、规则、存储测试，再运行真实 Unity 资源加载及 UI 检查；独立审查来源完整性与接龙边界。只在本任务工作树构建，不切换其他工作区。

测试入口：`scripts/test-idiom-data.sh`、`scripts/test-idiom-resources.sh`、`scripts/test-idiom.sh`、`scripts/test-idiom-persistence.sh`、`scripts/test-idiom-ui.sh`。真实词库测试通过 `--catalog artifacts/idiom-cleaned-preserved/game-index.json` 接入重新生成的目录。

验证结果见 [本次验证记录](../../verification-idiom-source-preservation.md)。
