# Zuma原版 UI 移植实施计划

> **For agentic workers:** 使用 subagent-driven-development 分工实施；共享工作树中按文件所有权隔离，并在集成后分别检查规格和代码质量。

**Goal:** 在现有 Unity 合集中交付原版素材的横屏Zuma，包含 18×4 组合、完整独立续局、声音和生命周期适配。

**Architecture:** Core 的纯 C# 模型拥有全部因果状态；Runtime 负责原 UI、触摸、音频和独立存档；AppController 只负责宿主路由、方向和公共设置。资源在编辑期转换、校验，运行时只加载白名单内容。

**Tech Stack:** Unity 6000.3.27f1、C#、UGUI、Newtonsoft JSON、Android ARM64 IL2CPP、ETC2。

## 隔离与基线

- 工作树：`zuma-original-ui/XanaduYonder`，基线 `42896603043a2a4b8dc4d8c3490f2399880adc5f`。
- 分支：`p/duanyao.0516/feature/zuma-original-ui`。
- 原工作区的数独设计与未提交文件不参与开发，不切换、清理或提交原工作区。
- 上游只读参考提交：`28001e0e5061341d502116586626150027242cce`。
- 约束详见已批准的 [技术设计](../specs/2026-10-27-zuma-design.md)。

## 1. 规则与可恢复模型

文件：`Assets/Scripts/Core/Zuma/`、`Tests/Zuma/`、`scripts/test-zuma.sh`。

- [x] 先建立路径采样、四难度、相同 seed 重放、发射/换球、插入消除、连锁与金币的失败测试。
- [x] 移植原固定步规则；处理隧道、速度、严格得分阈值和 Outro，纠正上游越界与单球标记丢失。
- [x] 生成和恢复完整强类型快照，覆盖弹丸、插入/爆炸进度、金币、PRNG、isSingle、最后清链位置和 Outro 奖励游标。
- [x] 用实际地图和模拟夹具验证中断前后逐步状态一致、胜败均可结束。
- [x] 运行 `scripts/test-zuma.sh`，记录真实结果。

## 2. 源素材与音频

文件：`scripts/import-zuma-assets.py`、`Assets/Editor/ZumaAssetImporter.cs`、`Assets/Resources/Zuma/`、`Assets/Scripts/Runtime/Zuma/ZumaAssets.cs`、第三方许可说明。

- [x] 校验源提交、18 图 XML/DAT 引用与坐标转换，生成稳定资源 manifest 和哈希。
- [x] 导入原菜单、字体、青蛙、球、HUD、背景、overlay 和缩略图，配置 ETC2、关闭 mipmap/ReadWrite。
- [x] 保持大图完整尺寸或等价切片；按 source rect/pivot 使用原素材。
- [x] 转换 MO3 音乐 order 段落和 OGG 音效，记录来源、循环点和输出校验；不引入桌面运行时。
- [x] 用导入校验脚本覆盖 18 图、路径标记和所有引用，保留源码许可证和素材权利声明。

## 3. 独立持久化

文件：`Assets/Scripts/Runtime/Zuma/ZumaSaveData.cs`、`ZumaSaveCodec.cs`、`ZumaSaveStore.cs`、`Tests/ZumaPersistence/`、`scripts/test-zuma-persistence.sh`。

- [x] 先用实际临时文件编写完整快照往返、坏主档/好备份、未来版本写保护、I/O 错误和其他游戏隔离测试。
- [x] 显式验证版本、token、字段、容量、有限数值及跨字段约束；版本不支持时不回退覆盖。
- [x] 实现同目录临时文件、flush、替换和合法备份；区别 Missing/Loaded/Recovered/Corrupt/Unsupported/IoError。
- [x] 成绩与终局单次提交，结果确认单独保存；失败保留 dirty 内存，重试不重复奖励。
- [x] 运行 `scripts/test-zuma-persistence.sh`，覆盖原九个存档文件保持不变。

## 4. 原 UI、输入与宿主

文件：`Assets/Scripts/Runtime/Zuma/ZumaPage.cs`、`ZumaView.cs`、`ZumaTouchInput.cs`、`ZumaAimGesture.cs`、`ZumaViewport.cs`、`ZumaAudio.cs`，以及 `AppController.cs`、`ProjectBuild.cs`。

- [x] 编写单指、取消、区域外松手、零向量、冷却拒绝、UI 不穿透的输入测试并观察失败。
- [x] 原素材构建选图、游戏 HUD、瞄准、暂停、胜败界面；新提示复用现有中文字体。
- [x] 独立 1280×720 横屏画布，绘制与输入共享逆变换；方向 token、两帧稳定、3 秒超时和退出恢复。
- [x] 加入大厅入口与 Android 返回路由；共用声音/触感设置和独立可见错误通道。
- [x] 暂停/失焦/后台/退出立即保存最近完整快照；恢复必须明确继续，dirty 内存不被旧磁盘覆盖。
- [x] 按关卡释放纹理/声音引用，唯一 EventSystem/AudioListener，不使用全局 timeScale。

## 5. 集成、审查与交付

- [x] 规则、存储、输入分别完成规格审查，再完成代码质量审查，修复所有有证据的问题。
- [x] 运行所有新测试和现有八个测试脚本，检查 Unity 导入和 C# 编译。
- [x] 使用 `scripts/build-android.sh` 生成独立工作树的开发 APK；只在签名配置可用时构建正式对照包。
- [x] 检查 Android Manifest、纹理平台配置、包体与 BuildReport，记录实际字节数和构建条件。
- [x] 检查可用真机；设备不可用时明确保留未验证项，不把构建成功写成体验验证通过。
- [x] 更新 README、架构、Zuma接入与验证文档，保留该工作树供审阅，不自动发布、合并或覆盖原分支。

## 实际交付证据

规则 29、输入 18、存储 32、资源 8 项通过，原八个测试脚本共 172 项通过。规格与质量审查完成，发现的问题均补回归并修复。Unity 编译/IL2CPP/两种 APK 构建通过；独立 preview 安装验证横屏、发射、暂停、后台及强杀续局，结算另使用有来源记录的夹具。

预览包 63,633,408 字节；nondevelopment 调试签名测量包 56,109,832 字节，两者无增量 ZIP 空洞。未配置正式发布签名，未发布、推送或合并。原工作区保持原分支和未提交文件。全部 72 组合人工通关、跨机型性能及音频长时听感不在已完成证据内，详见 [验证记录](../../verification-zuma.md)。
