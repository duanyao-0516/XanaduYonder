# 飞花令离线诗库

第二版底库纳入 27,000 首作品。此后先补录欧阳修《生查子·元夕》和李煜《虞美人》，再按名篇核查补录或校订 50 个来源版本。当前共 27,052 首和 253,739 条分句记录。构建读取固定提交中的全部 58 个唐诗、255 个宋诗、22 个宋词正文分片和两个选集。340 个锁定输入含一份许可证、用于个别南唐词的正文文件和《水墨唐诗》选集文件。实际读取的路径、字节数和 SHA-256 在 [source-lock.json](source-lock.json)。[chinese-poetry 固定提交](https://github.com/chinese-poetry/chinese-poetry/tree/b8594f81a89752241442f2ce267d6f66f96704ee)

每首作品的稳定 ID、类别、原文件路径和从 0 开始的数组下标可在 [work-sources.tsv.gz](work-sources.tsv.gz) 对查。先纳入明确指定的名篇、主题作品和覆盖到的通行异文，再加入通过检查的《唐诗三百首》《宋词三百首》。同类其余名额按作者轮流选取，唐宋诗优先选分句齐整的五言、七言，同等条目按正文哈希排序。正文汉字串或上游作品 ID 相同的条目保留一个来源，明确指定的出处优先。

## 名篇和主题审核

- [课标回归矩阵](curriculum-2022.json) 覆盖教育部 2022 年版附录一中本项目所选年代和体裁的 96 条，范围为 1–6 年级第 4–69 项和 7–9 年级第 7–36 项。修订前以所列作者检查课标标示句，73 条已可答，23 条缺少原题句。本轮按作品、出处和确切判题键核对全部 96 条及 573 个采用版本分句。汉乐府、先秦诗文、元曲、明清作品和散文不在这个矩阵范围内。[教育部语文课标](https://www.moe.gov.cn/srcsite/A26/s8001/202204/W020220420582344386456.pdf)
- [初版名篇清单](editorial-canon.json) 保留原选题依据。白居易《大林寺桃花》完整收录，「山寺桃花始盛开」可用于「山」和「花」。杜牧《清明》由原全唐诗正文分片之外的同一固定提交《水墨唐诗》补录，并与课标和公开诵读本对照。诗库只使用古诗原文。[大林寺桃花](https://yllhj.beijing.gov.cn/ztxx/bjhx/bzhz/201903/t20190321_759352.shtml) · [清明](https://yllhj.beijing.gov.cn/ztxx/lhysh/st/202104/t20210408_2349374.shtml)
- [基础编辑记录](editorial.json) 和 [新增主题记录](editorial-themes.json) 保留主题口径、出处、分句、入选依据和反例。十一张主题卡的候选来自逐条校核上下文的编辑记录。主题依据与所采用的正文一起打包。其余正文经过批量筛选和繁简转换，逐句校核限于上述记录。
- 字位题的导出检查为每张卡找到两个完整轮次的可解路线，所有轮次使用不同答案组。[构建清单](manifest.json) 记录实际题卡数量、各字位容量、常见候选、出处数和路线。同机场景交换先手时沿用同一顺序。

[补录台账](editorial-supplements.json) 在底库验明内容版本后追加两首，保留底库作品、分句、重复组和候选的既有位置。「月上柳梢头」采用所引通行本，同时接受源本的「月到柳梢头」。李煜词取固定提交的南唐正文，完整收录八个分句。[《生查子·元夕》读本](https://www.jsxc.gov.cn/zt/jieri/yuanxiao/shici/202602/t20260228_90752.shtml) · [教育部名篇推荐](https://www.moe.gov.cn/s78/A07/zcs_ztzl/2017_zt06/17zt06_bznr/bznr_zzggkdg/202008/P020200821550330709003.pdf)

## 选集核查结果

[选集机器核查](anthology-audit.json) 逐条对照固定选集的源文件坐标、作者和通行的判题键，并列出未匹配的源标点分句。采用版本和原刊本的标点或异文可能不同，原刊本中括注文字、缺字残句和不成立的断点不直接作为答句。

| 固定选集 | 源记录 | 核查结果 |
| --- | ---: | --- |
| 《唐诗三百首》 | 366 | 348 条直接对应，11 条同作者同正文重印，5 条正文相同而署名或题名不同，1 条《蜀道难》异本，1 条依据旧编辑台账排除 |
| 《宋词三百首》 | 280 | 280 条直接对应，补录了秦观《阮郎归》、贺铸《天门谣》和周邦彦《蝶恋花》 |

部分署名冲突仍保留出处供复查，例如元稹《行宫》与王建《故行宫》的同文条目。《蜀道难》异本第 337 条与入库第 122 条对查，未据此自动接受其余异文。黄拱条目和贺知章《回乡偶书》的关系见[原编辑台账](editorial.json)。卢纶「月黑雁飞高」与固定源本「月黑鴈飞高」仍可用，既有诗库中这两种写法原属两组，此次保留原组以兼容旧局。

[第三轮补订台账](editorial-classics-amendments.json) 记录字形、出处和明确的长句边界。《蜀道难》和《宣州谢朓楼饯别校书叔云》的长句按引文与源本收录。《念奴娇》保留「浪淘尽、千古风流人物」为一条，《满江红》保留「莫等闲、白了少年头」为一条，并改为以「驾长车、踏破贺兰山缺」整句判题。旧局对应的两条误切记录保留原编号，新局的可答索引排除这两条。追加版保留旧作品、分句、重复组、别名及题卡候选排序的全部既有行。[赤壁怀古参考本](https://www.edb.gov.hk/attachment/tc/curriculum-development/kla/chi-edu/recommended-passages/ks4_17a_text.pdf) · [满江红参考本](https://www.edb.gov.hk/attachment/tc/curriculum-development/kla/chi-edu/resources/literary_and_cultural_journey/Reading%20materials_Hangzhou.pdf)

## 分句和异文规则

正文保留固定输入的段落。诗按行和句读分句，至少三字、至多十字。词按词本行和大分隔标点分句，保留句内顿号，至少二字、至多十六字。整篇至少含四条有效分句。残缺、正文含附注或边界无法按规则确认的条目退出选择。个别编辑确认的诵读边界另列于编辑台账，例如《咏鹅》的三次连续呼唤。作者、篇名、正文和可展示别名通过项目现有 Noto Sans CJK SC 字体覆盖检查。

桌面 ICU 78.3 转为简体展示。答句同时保存上游原字形和逐条登记的异文。判题键按 NFC 与标点宽度归一，并忽略一个句末分隔符。完全相同的登记输入和明确登记的异文关联同一去重组。两个答案组争用同一个自动别名时移除争议别名。词中停顿仅在登记等价写法时可以省略。单字、双字和字位条件按实际提交写法检查。

## 复现和验收

使用 Python 3.9 或以上及 ICU 78.3 的 `uconv`。把固定提交的 GitHub 源码归档解到桌面，再把包含 `全唐诗/` 和 `宋词/` 的目录传给脚本。已有单文件缓存时直接复用，首次联网补缺才显式加 `--fetch`。每个源文件先按清单校验。生成版本依据解压后的内容哈希计算，gzip 使用零时间戳。压缩工具版本见 [构建清单](manifest.json)。

```bash
scripts/import-feihualing.py --source-cache /absolute/path/to/poetry-cache --uconv /absolute/path/to/uconv
scripts/audit-feihualing-anthologies.py --source-cache /absolute/path/to/poetry-cache --uconv /absolute/path/to/uconv
python3 -m unittest discover -s Data/Feihualing -v
```

Unity 直接打包 [catalog.bytes](../../Assets/Resources/Feihualing/catalog.bytes) 和 [上游 MIT 许可](../../Assets/Resources/Feihualing/Licenses/Chinese-Poetry-MIT.txt)。源码分片、缓存和审计文件留在构建资源之外。压缩资源字节数见清单。APK 增量以同基线、同设置的两个成品对比。
