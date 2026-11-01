# 赵云与阿斗离线接入实施计划

> **For agentic workers:** Use subagent-driven-development with explicit file ownership, followed by independent spec and quality review. User approved the WebView design and full-screen double-system-back interaction.

**Goal:** 第21个大厅入口打开完全离线的全屏竖屏原版游戏，连续两次系统返回回大厅，去除广告/联网/导流并验证小屏交互和持久化。

**Architecture:** Unity shell负责大厅与外部Activity互斥；专属Android Activity提供严格本地HTTPS资源与系统生命周期；固定SHA原版Laya包通过一处窄导出接入可读离线适配层。内容转换和纯Java AAR由可复现脚本生成，所有Android构建校验当前输入与产物一致。

**Tech Stack:** Unity 6000.3.27f1/C#，Android Java API26–36，系统WebView，LayaAir3.3.13/Spine3.7，Python构建工具，Playwright浏览器验证。

## 1. 内容与规则适配（独立任务）

- [ ] 将原始成功下载文件的URL/bytes/SHA256固化于 `ThirdParty/ZhaoyunAdouContent/upstream-lock.json`；读取 `artifacts/4399-262277/source`，不提交原站资产或依赖/tmp。
- [ ] `scripts/zhaoyun-adou/build-content.py` 校验308输入、仅在SHA锁定bundle的游戏IIFE末尾注入类引用，复制已审计引擎和场景，生成 `artifacts/zhaoyun-adou/content` 与 `content-build.json` 文件哈希清单。
- [ ] `ThirdParty/ZhaoyunAdouContent/offline-adapter.js` 在原启动脚本之前加载，明确平台策略/移除广告分享统计云端/体力门槛/原价商店/每局援助与本地奖励；正常成长保持原逻辑。
- [ ] 核对并移除武器背包等按游玩天数开放的功能门槛，首日开放；保留必要初始化、正常获取/装备/升级及每日奖励/赛季的时间语义，不修改全局日期。
- [ ] 转换scene/prefab/manifest修复可达缺图，移除无关平台场景；验证所有引用而非补空文件。
- [ ] 提供统一 `window.XanaduZhaoyun` API：`configure({soundEnabled})`、`setActive(boolean)`、`exit()`、`status()`；退出结果包含 `ok`，保存错误可见且不清档，状态包含ready/scene/battle/paused/error。
- [ ] 先用真实原包建立失败见证，再以browser全阻断外网检验新档、首战、商店、结算、成长、存档、生命周期；浏览器检查只写artifacts，不加入空壳SDK。

## 2. Android宿主（独立任务）

- [ ] `ThirdParty/ZhaoyunAdouRuntime/src/com/xanaduyonder/zhaoyunadou/` 新增专属Activity/bridge/本地资源handler/返回计数器，Manifest exported=false竖屏硬件加速，无新权限。
- [ ] 固定origin `https://zhaoyun-adou.xanadu.invalid/`，APK资产路径 `assets/zhaoyun-adou/`；仅本地GET/HEAD白名单，拒绝路径穿越/未知方法/外部导航，正确MIME与必要音频Range，关闭网络/文件/内容访问/弹窗下载/权限请求。
- [ ] WebView 直接铺满全屏，保持原布局，不添加安全区留白或预先搬移控件，无常驻宿主工具条；API33+与旧返回路径统一处理：第一次提示、2秒内第二次保存退出，取消手势不计数。
- [ ] 生命周期采用JS先暂停/保存/静音，再冻结WebView计时器；恢复仅释放宿主拥有的暂停原因。状态代号阻止过期回调，销毁与启动失败收口busy。
- [ ] `scripts/zhaoyun-adou/build-runtime.py` 消费内容清单编译Java并打包单一 `ZhaoyunAdouRuntime.aar`，报告文件hash，不引入额外native引擎。
- [ ] 纯Java边界测试覆盖URI规范化、返回间隔和重复/过期回调；编译后验证manifest不含非必要权限。

## 3. Unity大厅与构建门禁（主任务）

- [x] `AppController.cs` 添加第21条LobbyEntry；新增 `AppController.ZhaoyunAdou.cs` 和 `Runtime/ZhaoyunAdou/ZhaoyunAdouLauncher.cs` 采用现有bridge轮询契约。
- [x] `AppController.ExternalGames.cs` 收敛外部游戏busy+稳定大厅CanvasGroup锁；调整KR/Aircraft入口与HandleBack/OnDestroy，仅改共同宿主归属范围。
- [x] 从本包现有素材生成/复用大厅大图和小图，保存正确Unity meta；不得使用fallback图标冒充完成。
- [x] 扩展 `LobbyDesignChecks.cs` 为21入口，验证三视图真实路由与不提前加载游戏。
- [x] 新增Editor构建gate、`verify-build-input.py`、非Development隔离预览构建方法及 `scripts/build-zhaoyun-adou.sh`；保持正式版版本/签名/包名不变，预览包名后缀 `.zhaoyunadoupreview`。
- [x] 复用已验证KR依赖并核对内容与运行库hash，不能绕过现有KR构建门禁；生成物加入ignore。

## 4. 集成与交叉审查

- [ ] 按实际内容API联调Android生命周期、保存失败、退出、声音总开关与内部偏好，不用模拟成功或临时接线。
- [ ] 内容实现者审宿主约定、宿主实现者审内容/Unity接线；先审需求覆盖，再审正确性。主任务独立复核高风险结论并处理问题。
- [ ] `python3 scripts/zhaoyun-adou/build-content.py`、`python3 scripts/zhaoyun-adou/build-runtime.py`、`python3 scripts/zhaoyun-adou/verify-build-input.py --root .` 成功。
- [ ] 运行现有大厅检查和有意义的内容/宿主检查，结果写artifacts；`git diff --check`、JS语法、Java/C#编译检查均通过。

## 5. Android实测与记录

- [ ] 构建当前worktree的完整非Development预览APK；检查ZIP、payload哈希、无INTERNET/广告/通知等权限、Activity与正式包隔离。
- [ ] 安装到已连接测试设备，验证三视图入口/返回；第一次返回不退出、第二次退出、超时重置、左右边缘系统手势及预测返回取消。
- [ ] 小屏浏览器320×568/360×640与真机测试棋盘拖拽、征兵/合成、暂停、商店、标准结算、成长与长存档；检查边缘布局不截断关键操作。
- [ ] 实测Home/锁屏/失焦暂停和音频，重进不重放已领取奖励；记录加载耗时/宿主+WebView renderer内存，避免只报告主进程内存。
- [ ] 更新README、架构与第三方记录、`docs/verification-zhaoyun-adou.md`；明确代码/构建/设备各验证层，列未覆盖的后期内容。保留可安装APK，不自行发布或push。
