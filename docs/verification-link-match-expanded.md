# 连连看第四、第五关验证记录

日期：2026-10-28。基线为首版提交 `e59247c`，继续使用 `p/duanyao.0516/codex/link-match-research` 独立工作树。用户已完成首版 OPPO 测试，并确认第四关 96 张、第五关 192 张；素材可使用 Fluent Emoji 的其他类型。原 checkout 未修改，没有推送或发布。

## 当前交付

| 关卡 | 棋盘 | 图案数 | 每种张数 | 挑战时间 |
| --- | --- | --- | --- | --- |
| 01–03 | 原有 4×6 / 6×6 / 6×8 | 6 / 9 / 12 | 4 | 原有 90 / 120 / 150 秒 |
| 04 | 8×12，96 张 | 24 | 4 | 300 秒（5 分钟） |
| 05 | 12×16，192 张 | 48 | 4 | 600 秒（10 分钟） |

轻松和挑战均可在选关页进入全部五关，保持 3 次挑战提示、2 次主动洗牌及免费无路重排。第五关相对于第三关将行列都加倍；相同安全内容区下格边长为原来的 1/2、面积为 1/4。保留全部导航、工具尺寸和单一释放输入，密集棋盘不使用覆盖相邻格的扩大热区。

360×640 下，第三 / 四 / 五关格边分别约 **51.33 / 35.50 / 25.67** 设计像素；保存错误条出现时第五关为 **24.125**。密集棋盘的内边距、圆角、阴影及连线按相对原第三关的 cell 缩放；选中边框保留可见下限。剩余数量区域为三位数保留宽度，五关入口沿用滚动列表。第 3、4 关完成后可以继续下一关已有局；只有第 5 关是末关。

新增 36 张食物、动物、自然和物件图案，连同原 12 张水果组成 48 种 Fluent **3D** 图案。每种仍为 4 张，避免通过重复同图数量增加过多可选对子。原 12 张 PNG、ID 和 GUID 完全不变；固定源版本 `1ffb34c752ecf5d402f04cfb4b392c77f57c54bc`、48 个原图哈希及完整 MIT 许可保留在 [生产 manifest](../Assets/Resources/LinkMatch/manifest.json)。48 张原图合计 **1,699,094 bytes**，没有重编码或改色。

## UI 示例

- [48 / 96 / 192 同尺寸对照](prototypes/link-match-density/index.html)。
- [96 张实际 Unity 渲染](screenshots/link-match-expanded-96-360x640.png)、[192 张实际 Unity 渲染](screenshots/link-match-expanded-192-360x640.png)。
- [第五关选中态](screenshots/link-match-expanded-selected-192-360x640.png)、[实际外圈连线](screenshots/link-match-expanded-outer-route-192-360x640.png)、[保存错误占位](screenshots/link-match-expanded-warning-192-360x640.png)。
- [五关入口](screenshots/link-match-expanded-entry-new-388x784.png)、[第 05 关完成](screenshots/link-match-expanded-last-level-completed-388x784.png)、[升级存档写失败](screenshots/link-match-expanded-migration-write-warning-360x640.png)。

以上 PNG 来自生产 UGUI 的 Camera.Render，没有修饰原始渲染。浏览器原型仍仅演示，不读写玩家存档；真实 Unity 图与浏览器图的计时 / 模式样例差异不用于判断尺寸。短屏最大盘、普通屏、选中态和外圈路径均检查过，图案数、命中位置与实际模型一致。

## 六槽升级为十槽

当前格式为 schema/content 2，文件名 **`link-match-v2.json`**。旧 `link-match-v1.json` 及其备份保留，不卸载应用、不清空数据。迁移先按原版严格字段、类型、尺寸和索引规则解码，再按 `(mode, levelId)` 搬运；旧挑战索引 3 / 4 / 5 正确进入新 5 / 6 / 7，不能直接复制数组。

原尝试 ID、局面、有效时间、RNG、提示 / 洗牌数、活动顺序、终局标记与成绩元组全部保留。新四槽为空。读取优先当前 v2 主 / 备档；只有两者都不存在时才迁移 v1，已存在但损坏的 v2 不会被过时 v1 静默覆盖。迁移写失败仍保留完整可玩的内存状态，`MigrationPending` 接入页面和大厅保存错误；实际重试成功后清除提示。

测试用 legacy fixture 由 `git e59247c` 的原始生产 Core + codec 生成；独立审查再次编译旧源码确认原版可接受这两份 fixture。设备备份只在 Git 忽略的 `artifacts/` 中，未加入测试或版本控制。

## 实际验证

| 层次 | 本轮结果 | 证据 |
| --- | --- | --- |
| Core | **24 组通过** | 500 个完整发牌证明、400 个稀疏重排、720 个独立寻路比较；`scripts/test-link-match.sh` |
| 存档 / 会话 | **45 组通过** | 原 32 组及 13 组十槽 / v1 迁移 / 失败边界；`scripts/test-link-match-persistence.sh` |
| 输入 / 几何 | **5,104 条断言通过** | 密集格中心及相邻边界唯一归属，半边长，带错误条布局；`scripts/test-link-match-input.sh` |
| 共享 Core 回归 | **43 个测试通过** | `scripts/test-core.sh`；其他游戏算法未变，本轮没有再次执行首版的全部 49 个非 Unity 脚本 |
| 生产 UGUI | **11,154 条断言通过** | 388×784 / 360×640 的 96 / 192 盘，拖出 / 多指 / resize 取消、真实配对及外圈、十槽往返、旧末关向后续接、新末关、迁移实际写失败 / 重试与宿主链路 |
| 大厅及既有 UI | **261 条断言通过** | 720×1320 / 1582；`scripts/test-minigame-ui.sh` |
| 资源与元数据 | **通过** | 48 个 PNG 哈希、顺序、尺寸与完整 importer；原 12 个字节及 GUID 不变；全 Assets 无重复 GUID |
| 独立源码审查 | **通过，无待处理确认缺陷** | Core 与存储交叉复审；独立 UI / 资源 / 宿主复审 |
| Android | **构建、签名、安装与冷启动通过** | 当前 APK；签名 v2 验证；OPPO PKM110 返回安装 Success、启动 Status ok / COLD，进程存活 |
| 设备实际迁移 | **通过** | 启动后读回 v1、v2：旧六槽与六记录逐字段一致，预选及活动序一致，新四槽为空；v1 仍保留 |

日志与报告：`artifacts/validation/link-match-expanded/`、`artifacts/validation/link-match-ui/report.txt`、`artifacts/validation/minigame-ui/report.txt`、`artifacts/logs/build-link-match-validation.log`。Unity 检查与构建串行，使用本工作树独立 Gradle / ADB 环境。

Core 扩大棋盘时发现路径候选产生大量临时数组，实际分配红测后改为值参数，只为改进后的合法路线创建显示数组。192 张生成平均分配由约 1.69 MB 降到 184 KB；本地 .NET 中位耗时约 0.277 ms。生成 PRNG 和路径候选顺序未变，前三关各 100 种子的棋盘、完整配对及路径指纹与原提交逐一一致。该桌面基准不代表 Android 帧率。

## 已安装验证包

- 包名：`com.duanyao.xanaduyonder.linkmatchtest`，名称「Xanadu 连连看测试」。
- 文件：`artifacts/builds/XanaduYonder-0.5.0-link-match-validation.apk`，ARM64，0.5.0 / versionCode 5。
- 大小：**98,474,387 bytes**。
- SHA-256：`dcd7fe70196bcf5c32ca42b7bf6b329534011dbddb377819198fcbae2fe31fd7`。
- OPPO 使用 `install --no-streaming -r` 覆盖独立验证包；正式应用没有覆盖，原存档没有删除。

上述代理验证未自动操作设备棋盘，也未测量实际设备帧率；安装和迁移成功不作为完整玩法验收。随后用户确认已完成测试，并明确要求不再验证，直接合并并推送远端 master。本次主线合并未重跑测试或构建。前一版的 [三关验证记录](verification-link-match.md) 保留为历史证据。
