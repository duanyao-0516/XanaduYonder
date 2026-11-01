# 割绳子 H5 版下载与完整性核验

本文记录 2026-11-01 最初下载调查阶段，不代表后续集成的交付状态。后续已完成普通 425 关的离线适配、Android WebView 接入和设备验证，见 [实施与验证记录](../superpowers/plans/2026-11-01-cut-the-rope-offline.md) 与 [构建说明](../../ThirdParty/CutTheRopeContent/README.md)。

最初对用户指定的 [4399 游戏页](https://www.4399.com/flash/142014_2.htm)进行了下载和本地验证。**已取得 218 个原始文件、17 箱共 425 关的数据，但没有取得无缺失的完整包。** 源站缺少片尾视频和大量设备备用格式；当时普通桌面 DDS 路径可进入首关，不代表全设备或全流程可运行。

## 工作树

- 从执行 `git fetch origin master` 后的 `origin/master` 创建，基线为 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`。
- 工作树：`/Users/bytedance/.codex/worktrees/cut-the-rope-research/XanaduYonder`。
- 分支：`p/duanyao.0516/feature/cut-the-rope-research`。
- 最初调查阶段保留原工作区的分支及未提交修改，当时没有修改 Unity 产品代码、提交或推送。后续开发与交付另见上述记录。

## 实际下载来源与内容

游戏页中的 `_strGamePath` 与 [serversxiao.js](https://www.4399.com/js/serversxiao.js) 的 `webServer` 拼接得到[原始游戏入口](https://sxiao.4399.com/4399swf/upload_swf/ftp55/zmj/20260824/22/index.htm)。入口加载 `viverse-game-interface.js`，随后加载 `ctr.min.js`。另外需要同目录的 `index.umd.js`、`storage-sdk.umd.js` 和 `famobi.json`。

[famobi.json](https://sxiao.4399.com/4399swf/upload_swf/ftp55/zmj/20260824/22/famobi.json) 声明游戏版本 `1.1.4`，导出时间为 `2026-06-16T08:18:20.306Z`，最大关卡数为 425。下载物是 HTML、发布用 JavaScript 和运行资源，不是 APK 或原作者开发工程。

| 核验项 | 结果 |
| --- | --- |
| 主脚本 `m.bytes` 声明资源 | 688 项，70,508,904 字节 |
| 实际取得的清单资源 | 211 项 |
| 实际取得的入口、配置、脚本和图标 | 7 项 |
| 原始下载总量 | 218 个文件，22,492,036 字节，约 21.45 MiB |
| 未取得的清单资源 | 477 项，均为源站 HTTP 404 |
| 额外缺失 | `ctr.js.map`、10 个 SDK 图片均为 HTTP 404 |
| 地图数据 | `box01` 至 `box17`，每箱 25 关，逐份 JSON 解析共 425 关 |
| 完整性 | 所有已取得的清单资源字节数吻合；218 个文件的本地 SHA-256 复核通过 |

原始文件逐字保存于 `artifacts/cut-the-rope-4399-142014/game/`。下载 URL、HTTP 错误、预期/实际字节数及 SHA-256 见同级 `evidence/download-manifest.json`；额外图片见 `sdk-assets-manifest.json`；本地复核见 `integrity-and-levels.json`。`artifacts/` 已被仓库原有规则忽略，下载文件没有加入 Git。

## 为什么还不是完整包

1. **普通通关片尾缺失。** `res/video/outro_portrait.mp4` 和 `outro_landscape.mp4` 均返回 404。原程序在完成全部关卡并通过最后一关时进入片尾场景，按方向加载其中一个视频，静态调用链未发现替代资源。
2. **设备格式不齐。** 所有 KTX、WebP、OGG 文件以及许多 1x 图像/图集缺失。代码先选择 S3TC 对应的 DDS，若检测到 ASTC 又覆盖为 KTX；资源 404 会停止加载，没有自动换格式。本机默认 Mac Chrome 因此停在约 25%。
3. **可选内容也有缺失。** 部分圣诞背景/音乐、糖果皮肤 4 和奖励功能资源缺失。当前配置关闭奖励功能，普通模式不必加载这些文件，不能据此认定清单完整。

主程序、桌面 2x DDS 图集、普通模式 AAC 音频及 425 关地图已取得；备用格式缺失不等于每一关都缺内容，但片尾视频缺失会影响完整流程。

## 本地运行验证

通过绑定 `127.0.0.1` 的 Python HTTP 服务提供原始 `game/` 目录，在隔离的 Chrome 中阻断所有外部 HTTP/HTTPS 请求。原始入口设置 `document.domain='4399.com'` 会在 localhost 抛异常，但不阻止其他独立脚本执行。

- 默认 GPU 能力：请求缺失 KTX，停在加载页，见 `evidence/local-initial.png` 及对应网络报告。
- 仅在诊断浏览器里令 ASTC 扩展返回不支持，保留 S3TC，让原程序选择 DDS；**没有修改下载的入口、游戏代码或资源**。主菜单与首关均可进入，见 `local-dds-capability.png` 和 `local-play-delayed.png`。
- 首关执行真实鼠标拖动割绳，角色吃到糖果后出现 `Excellent!` 反馈；这只证明首关交互已运行。
- 这些探测没有外部网络请求，取得的游戏资源均由本地 HTTP 服务提供。没有验收全部 425 关、最终通关片尾、Android、iOS 或 Unity/App 集成。

启动原始副本：

```sh
cd artifacts/cut-the-rope-4399-142014
python3 -m http.server 8765 --bind 127.0.0.1 --directory game
```

访问 `http://127.0.0.1:8765/index.htm`。上述命令不会修复 KTX 缺失；默认 Mac 浏览器仍可能停在加载页。不要以双击 HTML 文件代替 HTTP 验证。

仓库基线另外运行了 `bash scripts/test-core.sh`，43 项通过、0 项失败；该结果只覆盖已有核心测试，不是本次第三方游戏的全量验收。

## 补齐来源核对

[Famobi 官方目录](https://games.famobi.com/cut-the-rope?technology=web)链接到[官方播放页](https://play.famobi.com/cut-the-rope)，后者公开引用 `games.cdn.famobi.com/html5games/c/cut-the-rope/v100/`。本环境连接该 CDN 超时，未取得主脚本，因此无法核实是否同版本或比较哈希。本次没有把其他版本的资源混进 4399 快照。证据保存于 `evidence/ctr-audit-official-source-check.json`。

本地归档为 `artifacts/cut-the-rope-4399-142014-downloaded.zip`，其中包含原始下载文件、README 和核验记录；名称中的 downloaded 表示成功下载的快照，不表示完整包。
