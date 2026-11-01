# 切水果大师（4399 / 235576）客户端包调查

日期：2026-11-01。已从远端最新 `origin/master` 创建独立工作树，基线为 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`，分支为 `p/duanyao.0516/codex/fruit-slice-package`。

## 结论

已取得游戏主体的公开发布文件，共 **418 个文件、9,840,649 bytes（9.84 MB）**。游戏采用 Laya 引擎，下载内容是浏览器发布版，不是 Laya/Unity 源码工程，也不是 Android APK。

按已公开的脚本、场景、图集及模型材质引用递归核对，核心资源依赖已覆盖；仍有 4 个源站 404，且原站平台框架依赖在线 SDK。因此不能把本次结果称为“服务器目录全量”或“所有功能已验证的完整离线版”。

## 来源与文件

- [4399 游戏介绍页](https://www.4399.com/flash/235576.htm)，[在线玩页面](https://www.4399.com/flash/235576_1.htm)。网页标注 10.16 MB。
- [平台框架入口](https://szhong.4399.com/4399swf/upload_swf/ftp44/huangcijin/20230825/08/index.html)。
- [游戏客户端入口](https://szhong.4399.com/4399swf/upload_swf/ftp44/huangcijin/20230825/08/gameIndex.html)。
- 工作树：`/Users/bytedance/.codex/worktrees/fruit-slice-package/XanaduYonder`。
- 包目录：`artifacts/fruit-slice-235576/`；压缩包：`artifacts/fruit-slice-235576-client.zip`。

包内 `original/` 保留原始下载字节，包含 HTML、启动脚本、5 个引擎脚本、游戏 bundle、50 个关卡、2 份香蕉配置、2 份排行榜、6 个场景、8 个图集、126 张图片、18 个音频、71 个网格、121 个材质、1 个模型层级和 2 个 `.ls` 文件（其中包含二进制反射贴图）。

`evidence/sdk/` 另存 6 个公开 SDK 脚本，共 1,184,786 bytes，用于记录在线依赖，不代表打包了平台后端或完整平台前端。`download-manifest.json` 记录 428 次资源取证结果：424 成功、4 个 404，成功文件带源 URL、大小与 SHA-256。`tools/download_public_assets.py` 保留下载与递归发现流程。

下载物位于仓库已忽略的 `artifacts/`，未接入 Unity，也未提交或推送第三方文件。

## 源站缺失项

| 文件 | 源站结果 | 当前脚本中的用途 |
| --- | --- | --- |
| `moreGame.png` | HTTP 404 | 其他平台推广入口 |
| `share.jpg` | HTTP 404 | 其他平台分享入口 |
| `share.png` | HTTP 404 | 其他平台分享入口 |
| `res/Sounds/main.mp3` | HTTP 404 | `ChangeMusicVolume` 辅助方法引用，当前 bundle 未发现该方法的调用点 |

未生成占位文件，也未用其他素材替代。以上判断来自当前公开脚本，不保证未执行的路径永远不会用到这些文件。

## 验证证据与边界

- 424 个成功下载文件的大小和 SHA-256 全部匹配。
- 6 个场景的 85 条 `loadList` 引用、8 个图集纹理、模型/场景/材质的 513 条路径引用（含重复）均有对应文件。
- 动态资源核对覆盖头像 1–50、商店图标 1–27、边框 1–6、`cut1/2` 和 `FruitImpactSplatter01/02`。
- 在只允许本机资源的 CSP 下，从原始 `gameIndex.html` 加载到隐私政策同意页面，背景和资源已渲染；没有代用户点击同意，因此没有执行后续模式和关卡验收。
- 本地请求还出现 `/favicon.ico` 的 404 与原始脚本向当前页面发起 POST 的 501；它们没有阻止首启页面渲染，但不能据此宣称运行无错误。
- 原站在线玩页面显示登录要求。`index.html` 加载在线平台框架；`gameIndex.html` 加载的 SDK 最终代理到 `parent.h5api`。本地禁止外网时，游戏采用原始代码中的默认 web 分支；这一结果不验证 4399 的登录、广告、奖励回调和其他在线服务。
- 未修改原始游戏、登录、广告或 SDK 回调；未运行 Unity 构建或 Android 验收。

详细机器可读结果见包内 `verification.json`；首启截图为 `evidence/offline-first-launch.jpg`。

## 本地复查

解压后，在包目录运行：

```bash
python3 tools/serve_offline.py --port 8769
```

浏览器打开 `http://127.0.0.1:8769/gameIndex.html`。此工具仅监听 `127.0.0.1`，用 CSP 禁止外部网络资源，不改写游戏文件；按 Ctrl-C 停止。默认 `index.html` 是依赖在线 SDK 的平台入口，不能作为完整离线启动验证。
