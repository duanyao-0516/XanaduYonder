# 只有一道门 2.1：网页发布包下载与完整性验证

日期：2026-11-01。

已取得此 4399 版本在支持 MP3 的 Chrome 中运行所需的网页发布文件，并打包保存。取得的是 Stencyl 导出的 Haxe/OpenFL/Lime 产物，不是原始可编辑工程或 Android APK。未接入 XanaduYonder，也未修改 Unity 业务代码。

## 工作树与来源

- 已执行 `git fetch origin master`，新工作树基线为 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`（`fix(release): publish v0.9.4 with production app identity`）。
- 工作树：`/Users/bytedance/.codex/worktrees/only-one-door-package/XanaduYonder`。
- 分支：`p/duanyao.0516/codex/only-one-door-package`。
- 原工作目录 `/Users/bytedance/Desktop/code/XanaduYonder` 的分支和未提交修改未改动。
- [4399 介绍页](https://www.4399.com/flash/243372.htm)、[播放页](https://www.4399.com/flash/243372_1.htm)。页面名为「只有一道门2.1」，游戏本身显示「Level Devil」。
- 由播放页 `serverszhong.js` 的 `webServer` 与游戏路径拼接取得 [H5 入口](https://szhong.4399.com/4399swf/upload_swf/ftp50/liuxinyu/20250415/11/index.html)。下载使用播放页 Referer；没有使用登录态。

## 下载结果

本地目录：`artifacts/research/only-one-door-243372/`，按仓库现有规则被 Git 忽略。下载原件和本地试玩版分开保留；SHA256 清单、来源快照、逐资源校验和浏览器证据同时保存。

| 交付物 | 文件数 | 字节数 |
| --- | ---: | ---: |
| `original/` 原始网页发布文件 | 24 | 5,229,774 |
| [原始包 ZIP](../../artifacts/research/only-one-door-243372/only-one-door-2.1-4399-original.zip) | 24 | 2,085,341 |
| [本地试玩包 ZIP](../../artifacts/research/only-one-door-243372/only-one-door-2.1-local-playable.zip) | 26 | 2,086,482 |

原件包括 `index.html`、`Level Devil.js`、`poki-sdk.js`、动态注入的 `poki-sdk-core.js`、`lib/default.pak` 和 19 个 `assets/sfx/*.mp3`。

本地试玩包仅从入口删除 `document.domain = '4399.com'` 所在的独立 script 行，并增加 README 和启动脚本。其余 23 个原始文件逐字节不变。原始入口在 localhost 会出现一次 SecurityError，但后续脚本仍能显示菜单；删除该行后抽测无 JavaScript 异常。差异保存在 `evidence/local-entry.patch`。

解压本地试玩 ZIP 后，在解压目录运行：

```sh
python3 -m http.server 8765 --bind 127.0.0.1
```

随后打开 [本地游戏](http://127.0.0.1:8765/)。macOS 也可在终端运行包内 `start.command`；停止服务器按 Ctrl+C。请通过 HTTP 启动，不要以 `file://` 直接打开页面。

## 资源完整性证据

主 JavaScript 内嵌清单含 2176 项：1709 IMAGE、57 TEXT、372 BINARY、38 SOUND。

- PAK 内 2138 项逐一验证 offset/length、连续性、gzip CRC/ISIZE 和解压内容；无越界、无缺口、无重叠，末项准确覆盖 PAK 文件边界，解压后合计 3,064,536 字节。
- 1709 个 PNG 文件头通过；场景包含 184 对 `.mbs` / `.scn`，ID 一一对应。场景含菜单等内容，不能把这个数量等同于 184 个可玩关卡。
- SOUND 是 19 组 MP3/OGG 备选路径；只有 MP3 条目标记预加载。19 个 MP3 均下载成功，117,956 字节，逐项大小吻合清单。
- 源站 19 个 OGG 路径返回 HTTP 404，未伪造或转码补齐。引擎将 `pathGroup=[mp3,ogg]` 交给 Howler，由浏览器选择支持的首个格式。Chrome 实测加载了全部 19 个 MP3，没有请求 OGG。这是缺失的备用编码，不代表已取得清单中的每种编码文件。
- `config/game-config.json` 是主 JS 内嵌 Base64 内容，不需要额外网络请求。

完整报告：[audit-summary.json](../../artifacts/research/only-one-door-243372/evidence/audit/audit-summary.json)。可复验工具：[audit-package.py](../../artifacts/research/only-one-door-243372/tools/audit-package.py)。下载来源、大小及 SHA256：[download-manifest.json](../../artifacts/research/only-one-door-243372/evidence/download-manifest.json)。

## 实际运行验证与边界

使用 Google Chrome headless 的全新浏览器上下文，本机 HTTP 提供资源，Service Worker 禁用，Playwright 阻断所有非本机 HTTP(S) 请求。未使用线上缓存、SDK 替身或玩法补丁。

- 本地试玩版进入主菜单、单人首个场景、双人首个场景；单人右方向输入后角色位置发生变化。
- 抽测期间 56 个请求全部得到 HTTP 200，失败请求 0，外网请求尝试 0，页面 JavaScript 异常 0；全部 19 个 MP3 和 PAK 成功加载。
- SDK 文件仍保留原站代码，其中包含外部广告/统计 URL；上述路径未触发，不据此保证所有后续玩法都无外部调用。
- 未逐关通关，未验证长时间稳定性、所有浏览器、存档全路径、音效听感或 Android 真机；没有进行 Unity 编译或回归，因为本轮仅下载和调研。
- 两份 ZIP 的 CRC 检查通过，解压内容逐项与对应本地目录一致；原件 24 项 SHA256 重新校验通过。

运行记录：[playable-browser.json](../../artifacts/research/only-one-door-243372/evidence/playable-browser.json)。截图：[单人开局](../../artifacts/research/only-one-door-243372/evidence/playable-one-player-select.png)、[移动后](../../artifacts/research/only-one-door-243372/evidence/playable-one-player-moved.png)、[双人开局](../../artifacts/research/only-one-door-243372/evidence/playable-two-player.png)。

## ZIP 哈希

```text
d508056d6329c5b7c74b45371091880dab7e7c4e690d03473b49ede5b204e64a  only-one-door-2.1-4399-original.zip
d57d03e5bbb491356290ebef622edf1ea7c97f4a725730982597923697b0d56f  only-one-door-2.1-local-playable.zip
```
