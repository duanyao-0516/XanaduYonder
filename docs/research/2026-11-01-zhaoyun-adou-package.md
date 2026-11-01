# 赵云与阿斗：4399 网页客户端下载记录

2026-11-01。从公开发行页面提取，已得到可离线启动、进入首场战斗并征兵的客户端资源；不能称为完全无缺的原始完整包，也不是作者的 LayaAir 工程源码。

工作树：`/Users/bytedance/.codex/worktrees/zhaoyun-adou-package/XanaduYonder`。分支：`p/duanyao.0516/feature/zhaoyun-adou-package`，基于 fetch 并核对远端后的 `origin/master`：`1f99287016f353fd92ee0ca94a761c2e6bd433e9`。

以下路径均相对于 `artifacts/4399-262277/`；压缩包是 `artifacts/zhaoyun-adou-4399-web-client.zip`。

## 来源与版本

- 介绍页：https://www.4399.com/flash/262277.htm
- 开玩页：https://www.4399.com/flash/262277_1.htm
- 发行目录：https://sda.4399.com/4399swf/upload_swf/ftp56/huangcijin/20261016/12/
- 游戏入口：`source/gameIndex.html`；`source/index.html` 是依赖外网的 4399 平台外壳。
- 引擎：LayaAir 3.3.13，Spine 3.7。业务代码为发行版混淆 JavaScript。
- 308 个原始成功下载文件，共 16,810,667 字节（16.81 MB / 16.03 MiB）。包括 11 场景、55 预制体/弹窗、29 atlas、91 PNG、83 音频、24 JSON、9 JS、4 TXT、2 HTML。
- 手机公开入口指向同一发行目录，未发现可补缺失资源的第二套目录。
- 每个原始文件的 URL、HTTP 状态、字节数、SHA-256 见 `evidence/download-manifest.json`。

## 本地运行

在仓库根目录先进入 `artifacts/4399-262277/`，再执行：

```sh
python3 -m http.server 8262 --bind 127.0.0.1 --directory source
```

浏览器打开 http://127.0.0.1:8262/offline.html 。不要直接用 file:// 打开。

`offline.html` 是唯一新增入口：它复制原始 `gameIndex.html`，只省略远端 4399 SDK 的 script 标签，让原版游戏进入已有的无 SDK 初始化分支；未修改 bundle、玩法数值或资源，也未模拟广告成功。原始两个 HTML 原样保留。首次启动会显示游戏原有隐私提示，原版流程与官网嵌入模式存在差异。

4399 SDK 的原始加载器与 API 另存于 `evidence/` 供追踪；平台广告、账户及平台服务不包含在离线能力中。当前 H5 分支自身关闭游戏服务器请求，核心玩法使用本地状态。

## 获取与完整性检查

合并五个 `fileconfig.json`、正常在线请求、bundle 字符串与动态命名规则，以及场景/预制体/图集/Spine 引用递归追踪，共尝试 316 条实体路径，成功 308 条。

- 1,019 个图集虚拟帧已映射到实体 atlas，不错误地当作散图下载。
- 29 个 atlas 的贴图均存在；16 组 Spine 的 186 个附件均匹配 atlas。
- 103 个 JSON 格式资源解析成功；递归审计额外发现并补齐 `prefab/shopPropsItem.lh`。
- 当前仍有 8 条原站 HTTP 404，见下表；此外有 1 个不能解析为下载 URL 的资源 UUID。

| 缺失路径 | 已确认的影响或边界 |
| --- | --- |
| `resources/img/effect/LongDanLiangYinQiangEff.png` | 龙胆亮银枪配置引用，特定武器特效存在缺口 |
| `resources/img/shop/light3.png` | 商店按钮闪光数组引用，特定商店光效存在缺口 |
| `shader/buttonShapeGlow.shader` | 商店发光描边实际加载；失败返回 null，光效缺失 |
| `resources/img/weaponBag/fragment0.png` | 创建函数有引用，未证实实际调用 |
| `scene/SoldierRangeScene.ls` | 调试场景路径，非已验证正常启动依赖 |
| `shader/basicShader.shader` | 仅发现 manifest 名称注册，未发现当前 bundle 实际使用 |
| `shader/sweepLight.shader` | 仅发现 manifest 名称注册，未发现当前 bundle 实际使用 |
| `splash.png` | 在线页面同样 404；本地启动已证实不受阻断 |

`scene/GameOverScene.ls` 的 `winBox/bag` 图像引用 `res://5bf43f39-5425-4fb0-bffd-b4d58ddcac67`，未发现映射；初始隐藏，未验证结算后效果。未伪造替代文件。

## 实际验证

使用新的 headless Chrome 浏览器上下文，允许本机 HTTP 并阻断所有外网。完成首次启动、原版隐私页、公告、主菜单、匹配、第一场战斗和征兵。

- 158 次请求，全部本地；0 次外网请求；0 个未捕获 JavaScript 异常。
- 唯一本地 404 为原站也缺失的 `splash.png`。
- 截图：`evidence/offline-menu.png`、`offline-battle.png`、`offline-recruit.png`。
- 结果：`evidence/offline-smoke.json`、`verification-summary.json`、`closure-audit.json`。
- 未验证全部关卡、所有武器/商店效果、存档恢复回归、Android/Unity 集成；没有把首战验证当成完整通关验证。

`tools/download.py` 可重跑公开资源下载；`tools/check-resource-closure.py` 可复查实体引用。浏览器验证脚本 `tools/offline-smoke.cjs` 需要 Node.js、playwright-core 和 macOS Google Chrome；安装 playwright-core 后通过 NODE_PATH 指向其 node_modules 运行。

原始游戏内容、平台脚本及引擎保持各自权利归属，本仓库 MIT 许可不覆盖这些第三方文件。本次资源仅保存在 Git 忽略的 artifacts 目录。
