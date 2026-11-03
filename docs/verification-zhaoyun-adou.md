# 赵云与阿斗离线接入记录

本分支在 `1f99287016f353fd92ee0ca94a761c2e6bd433e9`（当时最新 master）上完成最初 21 入口集成；交付时合并最新 master 的割绳子与只有一道门，本游戏编号调整为 23。原游戏公开客户端为 4399 `262277`，Laya JavaScript 游戏；对手由本地 AI 驱动。设计见 [离线设计](superpowers/specs/2026-11-01-zhaoyun-adou-offline-design.md)，原始资源调查见 [下载记录](research/2026-11-01-zhaoyun-adou-package.md)。

## 行为与归属

- 大厅三种视图均有“赵云与阿斗”，归入逻辑策略。仅点击时创建原生启动器，游戏由独立竖屏 Activity 内的全屏 WebView 承载。
- 第一次完成系统返回提示“再返回一次回到大厅”，两秒内第二次保存长期进度并退出。超时、失焦或切后台重置计数；取消的预测返回手势不计数。WebView 与游戏画布直接铺满屏幕，不添加整屏安全区留白、不预先搬移原控件，不设置手势排除矩形。仅对真机证实遮挡或无法点击的具体按钮做必要修正。
- 游戏内容和存档属于离线网页；原生层管理可见性、全屏、系统返回和资源白名单；Unity 管理大厅及外部游戏互斥。原有王国保卫战与新游戏共用稳定大厅根节点上的输入锁。
- 后台先由 JS 确认保存、模拟暂停与静音，再冻结 WebView；回到未销毁的战斗需点击继续。主动退出或进程回收不恢复战斗快照，重新进入主菜单并读取长期进度。
- 保存失败不能冒充退出成功；已经开始退出结算的会话不恢复为可玩的旧战斗，只允许重试保存。损坏存档保留原值，不静默覆盖。

## 内容与权限

原始成功下载的 308 个文件由 `ThirdParty/ZhaoyunAdouContent/upstream-lock.json` 固定路径、长度和 SHA-256。原包和生成的大资源留在 `artifacts/`，不加入 Git；大厅大 Logo 最终使用用户指定原图，小图标为原创统一风格长枪护盾；见[美术来源](zhaoyun-adou-lobby-art.md)。

离线适配移除广告双倍、抽奖、分享、授权、线上榜单及体力入口限制。正常胜负奖励、金币原价商店、兵种与武器成长保留；战斗援助按设计作为每局一次的明确免费功能。可达缺图复用包内素材，不加载无用的缺失 shader 或调试场景。

游戏只访问 `https://zhaoyun-adou.xanadu.invalid/` 的 APK 内白名单文件。非本地请求直接拒绝，不回退到网络；WebView 禁用网络、文件/content 访问、下载、弹窗、位置和权限申请。没有 JS 到 Java 的能力桥，原生只调用窄的 JS 生命周期 API。原始第三方版权声明保留；研究客户端不是作者原始 Laya 工程。

## 可重复构建

前置条件：原始成功下载资源位于 `artifacts/4399-262277/source`，既有王国保卫战依赖通过其现有内容门禁；Unity 6000.3.27f1 与其 Android 工具链可用。

```sh
python3 scripts/zhaoyun-adou/build-content.py
python3 scripts/zhaoyun-adou/build-runtime.py
python3 scripts/zhaoyun-adou/verify-build-input.py --root .
python3 scripts/zhaoyun-adou/test-host.py
python3 scripts/zhaoyun-adou/test-build-gates.py
NODE_PATH=/tmp/4399-package-tools/node_modules node scripts/zhaoyun-adou/test-content.cjs
bash scripts/test-lobby-design.sh
bash scripts/build-zhaoyun-adou.sh
```

浏览器检查使用本机安装的 Playwright Core；`NODE_PATH` 可替换为自己的模块目录。内容构建和 APK 运行不依赖 `/tmp`。

内容报告、运行库报告和 AAR 必须与当前输入源码及资源指纹一致；Unity 的 Android 构建前置检查拒绝遗漏或过期产物。APK 校验再检查最终权限、Activity、资源清单及全部内容 SHA。预览包为 `com.duanyao.xanaduyonder.zhaoyunadoupreview`，采用非 Development 本地签名，不覆盖正式包或改动正式版号与签名。

## 验证记录

- 原大厅失败见证：新增 21 入口断言后，旧实现仅 20 入口，`artifacts/zhaoyun-adou/validation/lobby-before.txt` 记录失败。
- 实际 Unity UGUI：720×1320、720×1582，三种布局与路由、原图加载、延迟初始化、外部游戏互斥、返回解锁及销毁退订，共 **3,807** 断言通过。报告与截图：`artifacts/lobby-design/`。
- 既有王国保卫战依赖从同机既有工作树复制进本工作树，并重新通过 `verify-build-input.py`；内容 SHA-256 为 `c600660fcf5b487eea66bf6a3cacc56a98530de6b6512696c025cc6a3ac3a046`，没有绕过它的构建门禁。
- 新工作树的 Unity 导入缓存来自同一 master 提交、同一 Unity 版本的既有工作树，仅复制内容寻址的导入缓存并由 Unity 重新校验源文件；没有复用历史 APK 或历史验证报告。

2026-11-01，在用户要求停止追加测试/编译并提交合并前，已完成以下验证：

- 最终离线内容为 299 文件、16,747,960 字节，digest `1265d3e089c91b6f8124aa1d6b400f8a9f0087ec8e8d369fcb72f99f422a19a5`。真实浏览器主流程 11 阶段、首日开放专项 7 阶段，以及 360×640/320×568 两种小屏的征兵、拖放与同字合成通过；外网请求、缺资源和 JS 异常均为空。胜负、高级商店、较强武器碎片和累计 21 局地图场景使用明确记录的测试夹具，不代表自然打通全部后期内容。
- 武器背包及相关碎片能力第一天开放，基础四类武器和领取标志一次存档提交；写入失败保持未就绪，重载恢复后不重复发放。两个登录头像可选，地图按累计局数轮换；真实日期、段位/碎片门槛、每日奖励和道具有效期保持。
- 原生边界 91 断言及状态恢复 10 场景通过；所有 7 个 Java 类编译通过。状态场景使用真实方法与调用替身，不等于 Android 原生错误面板实测。构建输入 8 项篡改回归通过。
- 合并及最终美术调整之前的非 Development 预览 APK 构建并通过包内校验：`artifacts/builds/XanaduYonder-0.9.4-zhaoyun-adou-preview.apk`，328,600,646 字节，SHA-256 `4d1e3ea137d528f56f8da950ca6c1923417fd4dbaa70c4524fe607c1947e0f0c`。无 INTERNET 或新增权限，仅保留合集既有 VIBRATE；已安装 OPPO PKM110，与正式包隔离。
- 该预览完整包已实际显示 21 项大厅、进入独立全屏 WebView，并显示首日可用的四类基础武器。此前同宿主探针已实测左右边缘返回、两次退出、超时重置、短滑取消、后台战斗暂停继续和退出重进长期进度。

用户随后要求快速提交合并、不要再运行测试与编译，因此不追加最终完整包的其余设备回归。最终包大厅返回闭环、锁屏/声音实测、全部后期内容、最新 master 合并及最终大厅图标更换后的测试/构建均不宣称完成。源码检查、浏览器和探针结果不替代这些未完成项。

## 2026-11-03 存档管理与当局援将

主菜单“存档”可编辑蜜獾币，授予或替换商店道具，累加武器完整件数，以及开放玩家的随机武将招募资格。新旧档默认跨天保留已装备的技能道具及等级。战斗“援将”则暂停当前对局，使用原征召栏预约与原单位工厂，向玩家底部空槽一次放入所选武将的整组姓名字。

本轮内容共 303 个文件。Chromium 156 小屏专项 11 个阶段通过，包含 320×568 下孙尚香三字发放与实际拖动合成、只有两空槽时整组拒绝、动画预约占位、第二字创建后异常回滚重试、前后台暂停衔接，以及 360×640 的旧档跨日与奖励重置。原有浏览器主流程 11 个阶段通过。数据与阶段记录在 `artifacts/zhaoyun-adou/browser-evidence/save-editor.json` 和 `content.json`。安装包一节记录的是前一版预览，此功能已生成当前工作树的 Android AAR，未重建或安装合集 APK。
