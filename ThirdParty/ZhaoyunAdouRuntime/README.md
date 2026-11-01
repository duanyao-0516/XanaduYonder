# 赵云与阿斗 Android 宿主

此目录提供纯 Java WebView Activity 和 Unity 调用桥，不包含游戏规则。网页适配层位于 `ThirdParty/ZhaoyunAdouContent`；经过内容构建校验的文件打包进 `Assets/Plugins/Android/ZhaoyunAdouRuntime.aar`。

## 入口与退出

Unity 调用 `com.xanaduyonder.zhaoyunadou.ZhaoyunAdouBridge.open(Activity, soundEnabled, hapticsEnabled)`。返回值表示启动是否已加入 Android 主线程队列；`isRunning()` 在排队、Activity 存活和 WebView 释放期间均为 `true`。`getLastError()` 用于异步启动失败。网页没有原生震动能力，`hapticsEnabled` 仅保留统一入口签名。

Activity 固定竖屏并隐藏系统栏，没有常驻工具条。第一次已提交的系统返回提示 Toast，2 秒内第二次返回调用网页 `exit()`；取消的预测返回手势不计数，失焦和进入后台会重置计数。保存确认成功后销毁 WebView 并返回大厅。保存失败会展示重试和明确的“返回大厅（未确认本次保存）”，不会自动放弃、恢复已结算战斗或声称保存成功。

## 全屏布局

WebView 以 `MATCH_PARENT` 填满窗口，根视图不设置 padding；游戏沿用原布局，不预先缩进或移动控件。Activity 使用 `setDecorFitsSystemWindows(false)` 和 cutout `SHORT_EDGES`，隐藏系统栏。

不传递通用安全区，也不设置手势排除矩形或拦截边缘触摸；系统返回手势区域不形成画面留白。原生错误页保留自身 24dp 内容间距。仅在真机发现具体控件被遮挡后，再针对该控件修正。

## 网页契约与生命周期

宿主只有 Java → JavaScript 调用，不注册 JavaScript → Java 接口。页面必须提供同步 JSON 结果：

| 接口 | 用途 |
| --- | --- |
| `configure({soundEnabled})` | 初始化声音偏好，可在游戏准备完成前调用 |
| `setActive(false)` | 保存、获取宿主暂停所有权并静音 |
| `setActive(true)` | 释放宿主暂停；战斗等待用户明确继续，保留游戏自身暂停 |
| `exit()` | 幂等结算并保存退出 |
| `status()` | 返回 `ready`、`scene`、`battle`、`paused`、`error` 等状态 |

操作返回 `{ok:true}` 或 `{ok:false,error}`。`setActive(false)` 与 `exit()` 保存失败时仍先完成暂停和静音，并用 `paused:true` 明确告知宿主可冻结 WebView；普通 `status().paused` 也可能来自游戏自身暂停，不能替代这次握手。`onPageFinished` 不代表游戏准备完成，宿主继续轮询 `status()`，启动超时仅累计前台且拥有焦点的时间。

错误恢复依据页面本次报告的 `ready`：未完成初始化时重新载入，初始化完成后才重试保存。初始化可能在 `configure` 成功之后失败，因此 `setActive` 返回错误时也会查询实际状态再选择恢复。首日礼包事务未保存成功不能通过普通保存或恢复操作绕过初始化确认。

只有宿主暂停握手确认后才调用 WebView `onPause()` / `pauseTimers()`。回调使用代次与实例校验，快速暂停恢复、页面重载或销毁后的过期回调不会改变新页面。长期进度保存在独立 origin 的 localStorage；退出和重载不清理 storage。原游戏不提供战斗快照，销毁页面后不能恢复进行中的战斗。

## 离线边界与构建

固定 origin 为 `https://zhaoyun-adou.xanadu.invalid/`，入口 `index.html`。本地服务器只返回内容清单中的资源，支持规范化 GET/HEAD 和音频单段 Range，未知路径明确返回 404。设置 `setBlockNetworkLoads(true)`，拒绝外链、弹窗、下载、权限、file/content 访问；运行库 Manifest 不声明任何权限。

在仓库根目录运行：

```sh
python3 scripts/zhaoyun-adou/test-host.py
python3 scripts/zhaoyun-adou/test-runtime-package.py
python3 scripts/zhaoyun-adou/test-apk-verifier.py
python3 scripts/zhaoyun-adou/build-runtime.py
python3 scripts/zhaoyun-adou/verify-build-input.py
```

`build-runtime.py` 读取 `artifacts/zhaoyun-adou/content-build.json`，核对整个内容目录的文件集合、大小和 SHA-256 后，使用 Unity AndroidPlayer 的 OpenJDK 与 Android 35 编译。输出 AAR 及 `artifacts/zhaoyun-adou/runtime-build.json`，记录内容报告、所有 Java/Manifest/构建脚本输入和 AAR 的指纹。Unity 构建前 gate 拒绝源文件、内容报告或 AAR 已漂移的组合。

宿主状态恢复回归从 Activity 提取实际轮询及生命周期方法，在最小调用/界面替身上执行；它验证分支与重试选择，不是 Android 原生错误面板实测。打包校验也不代表设备上的布局、声音或返回手势已经验证；完整 Unity APK 仍需验证大厅入口、全屏画面、双返回、后台恢复和长期进度保留。
