# Zuma 实现与验证

后续更新见 [2026-10-28 导航、说明与瞄准优化验证](verification-zuma-help.md)；下文包体与开发模式仅记录 10 月 27 日历史产物。

日期：2026-10-27。本记录区分纯规则测试、Unity 构建和实际设备操作；自动测试覆盖组合不代表人工通关全部地图。

## 代码与隔离

- 基线：`42896603043a2a4b8dc4d8c3490f2399880adc5f`，v0.3.0。
- 工作分支：`p/duanyao.0516/feature/zuma-original-ui`。
- 独立工作树：`/Users/bytedance/.codex/worktrees/zuma-original-ui/XanaduYonder`。
- 原工作区保留原分支、README/architecture 未提交修改、数独设计及 Zuma 设计，没有切分支、清理或合并。
- 上游固定：`GalaxyShad/Zuma-Deluxe-HD@28001e0e5061341d502116586626150027242cce`。
- 游戏显示名统一 **Zuma**。实现范围为 18 张单链地图 × 4 档难度，原素材/菜单、横屏、触摸瞄准和松手单发、SWAP、独立续局与共用设置。

## 自动测试

测试编译实际生产代码；持久化使用项目实际 Newtonsoft.Json 13.0.2，真实临时文件及原子替换路径。

| 命令 | 通过数 | 主要证据 |
| --- | ---: | --- |
| `scripts/test-core.sh` | 43 | 原 2048 规则 |
| `scripts/test-input.sh` | 10 | 原手势 |
| `scripts/test-persistence.sh` | 9 | 原公共存档 |
| `scripts/test-jump.sh` | 25 | 原跳一跳规则 |
| `scripts/test-jump-persistence.sh` | 18 | 原跳一跳存档 |
| `scripts/test-sheep.sh` | 31 | 原旸了个旸规则 |
| `scripts/test-sheep-persistence.sh` | 26 | 原旸了个旸存档 |
| `scripts/test-sheep-layouts.sh` | 10 | 原分堆布局 |
| `scripts/test-zuma.sh` | 29 | 72 组合自然败局、隧道地图持续射击、中断重放、计分/连锁/Outro、PRNG、容量、阶段与时间边界 |
| `scripts/test-zuma-input.sh` | 18 | 单指所有权、取消/区域外松手、UI 拦截、冷却、零向量、横屏安全区逆变换 |
| `scripts/test-zuma-persistence.sh` | 32 | 完整快照、主备恢复、未来版本保护、I/O 失败、结果幂等、时间上界、退出后重试、旧九个文件与 PC 文件隔离 |
| `python3 scripts/test-zuma-assets.py` | 8 | DAT 端点/标记、manifest、模块音乐、48 音频解码、尺寸对齐和原像素一致 |

合计 **259 项**。日志位于本地 `artifacts/verification/zuma/tests/`。未运行原 C 可执行程序做逐帧 oracle 对比；固定步、输入顺序和颜色集差异见技术设计 `rulesVersion=1` 说明。

## 审查与实测修复

规则、资源和 Runtime 完成独立规格审查及质量审查，有证据的问题均补回归并修复：

1. 地图数据与视觉资源分开持有，换图保存失败后退出仍能校验旧、新地图并重试；资源失败使用 I/O 语义，不能误判无存档。
2. 新游戏先完成资源加载，再替换权威对局；加载失败、导航失败与保存失败分别显示。
3. 低分停止生成的空链状态被拒绝，合法空链继续补球；避免恢复后永远无法结束。
4. 超限且不能可靠判断版本的文件受写保护；任意精度正整数版本在 Int32 收窄前判断，保护主档和备份。
5. `PlayTicks` 统一上限并饱和，最大可接受快照能结算、保存和确认，不会在退出路径溢出。
6. 真机发现 `frogland2` 等 11 个 Vorbis 1.0 RC1 音效无法被 Unity FSBTool 导入。现从原文件解码 PCM WAV，由 Unity 压缩；48 个音效/音乐片段均在构建前实际加载，不能仅凭源文件存在宣布成功。
7. APK 审计发现非四倍数纹理回落 RGB24/RGBA32。28 张纹理仅向右/下填边，原像素、字形和切片坐标不变；缩略图显示裁切原 476×305 区域。导入器新增实际 Texture 格式校验。
8. 原标题字库将 C/M/V 存在小写键下，新提示标题复用对应原字形，避免 CONTINUE/SAVE/NEW GAME 缺字。

## 资源与构建

57 张纹理、18 张地图、55,874 个路径顶点、38 个源音效、8 个模块音乐段落（load/main 各分 intro/loop，合计 10 个音乐文件）。共 132 个被哈希校验的输出文件。原图未缩放；禁用 mipmap 和 Read/Write。APK 内逐个核对纹理序列化数据：21 张 ETC2 RGBA8（format 47）、36 张 ETC2 RGB4（format 45），无回落；图像数据合计 **29,626,416 字节**，与理论值完全一致。BuildReport 全项目纹理从 71.3 MiB 降为 32.9 MiB，不能将该数字当成场景峰值内存。

资源 manifest SHA-256：`bd002888e432a18a9abe341fcaa88e54d2797d428f3c6129d81e4f593746bfdd`。上游源码 MIT 与 PopCap/EA 素材权利分开记录，见 `THIRD_PARTY_NOTICES.md`；本轮未公开发布。

Unity 固定 6000.3.27f1、Android ARM64/IL2CPP、GLES3、min SDK 26、target 36。Unity 导入/C# 编译、IL2CPP 和 APK 构建通过；48 个 AudioClip 经 AssetDatabase/Resources 实际加载，57 张纹理实际 ETC2 校验通过。

| 产物 | 字节数 | 用途 |
| --- | ---: | --- |
| `XanaduYonder-0.3.0-zuma-preview.apk` | **63,633,408**（63.63 MB） | 可并行安装的 Development 验证包 |
| `XanaduYonder-0.3.0-release-intermediate.apk` | **56,109,832**（56.11 MB） | 无 Development，调试签名，仅测量，不安装覆盖主 APP |

干净 preview SHA-256：`794eda779ee5b799bc9e627e6689cb05ae086eb4d0b07d2ee1baa5ba5cc67982`。ZIP 272 个有效条目间 gap=0，APK v2 签名验证成功；与 nondevelopment 包的 57 张纹理 payload 哈希逐项一致。全量 `.meta` 缺失 0、重复 GUID 0；Packages 无差异，ProjectSettings 仅增加 landscapeLeft 能力。

上述 nondevelopment 包 `aapt` 实查无任何 `uses-permission`、无 `debuggable`；`apksigner verify` 通过 APK v2，证书为 Android Debug。SHA-256：`c004214b0f8fadf077437537e3379b57fc0e351459cf4528ffe10104215cd632`。ZIP 271 个有效条目间无空洞，只有正常签名块和索引。

一轮增量 preview 曾遗留 31,436,912 字节 ZIP 空洞，文件约 95 MB，不能作为干净包体证据。重新打包前在 Unity 生成的 Gradle 工程执行 `:launcher:clean`，只清理该工作树生成的 launcher 构建缓存，再运行预览构建。禁止将增量空洞解释为图片压缩无效。

并行验证包使用 `com.duanyao.xanaduyonder.zumapreview`，显示为 **Xanadu Zuma Preview**，保持 v0.3.0/code 3。设备中原 APP 是 v0.4.0/code 4，因此不降级、替换或清空原 APP。preview 的数据与原 APP 完全分开。

`BuildZumaPreview` 在 finally 恢复正式 application identifier/productName。标准包名仍为 `com.duanyao.xanaduyonder`。Development 预览包含 Unity 开发用途的 INTERNET/debuggable，不能当作无网络权限的正式包。nondevelopment `release-intermediate` 使用调试签名，仅用于体积检查，不是发布签名产物。

历史 v0.3.0 开发 APK 为 58,839,927 字节，nondevelopment 已签名样本为 38,079,817 字节。新 nondevelopment 相比该历史样本约增 **18.03 MB**。历史样本未在本轮重新同批构建且签名方式不同，相减只作量级参考，不能称为严格净增量测量。APK、安装后占用、GPU 常驻、进程峰值内存和帧耗时是不同指标，构建日志的 Editor 内存不作运行时指标。

## Android 操作验证

ARM64 Android 模拟器 1080×2400 上，实际 ADB 触摸及读回 `zumapreview` 文件已验证：

- 竖屏大厅滚动到 Zuma，进入 2400×1080 横屏选图，原神庙菜单和暂停石框显示正常；返回大厅恢复竖屏。
- 拖动瞄准后松手单发，读回存档包含飞行弹丸、改变后的方向和 recoil；按 Android 返回键暂停。
- 在弹丸飞行快照暂停后强杀进程、重新进入，尚未点击继续时完整存档与强杀前一致，活跃弹丸未丢失。
- HOME 到后台，再回前台显示暂停；后台和前台读回的对局完全一致，计时没有补进。
- 暂停中的 SOUND 开关将公共存档 `soundState` 从 1→2→1，2048 的 game 状态保持一致。
- 修复音频后的操作期间未发现 Unity/AndroidRuntime 错误日志。
- 最终 63,633,408-byte 干净预览包重新安装成功，重新开局完成 intro，拖动发射后方向已更新、进入 play；读回保存有效。对应 `23-final-playing.png` 和 `final-playing-paused.json`；此次 Unity/AndroidRuntime 错误日志为空。

用明确标注的有效存档夹具验证两种终局 UI：自然无输入运行到 lost 的存档显示 GAME OVER；构造合法临胜状态后由设备实际 Core 运行 Outro，3001 分增长到 4801 分并显示 STATS，提交最佳成绩。点击 OK 后 `resultAcknowledged=true`、成绩仍为 4801，未重复奖励。该胜利用于结算链验证，不称为人工手打通关。夹具机制和哈希位于 `artifacts/verification/zuma/fixtures/fixture-provenance.json`，操作只涉及模拟器的独立 preview 数据，结束后还原原验证对局。

原物理设备 OPPO PKM110 已用并行包检查原 UI，并发现上述旧音频编码缺陷；最终包的真机长时体验不能据早期检查宣布通过。

尚未完成全部 72 组合人工通关、跨机型/低内存/折叠屏矩阵、长时音乐循环听感与触感、断电级文件系统故障注入、安装后完整占用和稳定帧率/峰值内存测量。自动测试、构建和关键流程已覆盖的范围不扩展成这些结论。
