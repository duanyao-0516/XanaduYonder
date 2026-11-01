# 系统返回防误退验证（2026-11-01）

基线为远端 master `1f99287`（0.9.4），修复分支为 `p/duanyao.0516/bugfix/android-double-back`。

## 行为

- 系统返回即将离开小游戏回大厅时，首次原生 Toast“再按一次返回大厅”，间隔小于 2 秒的第二次执行退出；2 秒及以后重新提示。
- Unity 各游戏原有的关弹窗、收键盘、暂停、回选关操作仍单次生效，不计入退出确认。会直接回大厅的终局、模式选择、坏存档弹窗也要确认。正常界面按钮和大厅“退出应用”弹窗保留原行为。
- 按当前游戏区分确认状态；非退出返回、游戏切换、失焦和切后台清除状态。
- 王国保卫战的系统返回统一为上述双击回大厅，包含其帮助／设置界面；界面里的关闭按钮不变。Android 13+ 使用预测返回回调，旧返回键只按有效松手计数。正常退出走 `nativeSendQuit → love.quit → SDL_main → finish → nativeQuit → Bridge.stopped`；初始化完成前的退出请求等待既有 Lua 属性读取确认事件队列就绪。

## 根因与范围

Unity 已将系统返回送入 Escape，但部分页面及弹窗直接调用大厅回调。修复位于宿主确认状态与各游戏实际离开分支，转屏前先确认。

王国保卫战只重写旧 `onBackPressed/dispatchKeyEvent`，新系统预测返回会绕过它们。新增回调遵循 [Android 预测返回接口](https://developer.android.com/guide/navigation/custom-back/predictive-back-gesture)。未更换 Unity Activity，未修改塔防 Lua、图片、音频或关卡。

## 已执行的检查

- 纯确认状态机：12 项，覆盖首击、第二次、消费后重置、游戏隔离、1999ms／2000ms、时钟回退与重置。
- Unity 页面返回方法编排：64 项，覆盖 14 个页面的直接退出、局部返回、异步退出及错误弹窗。该检查抽取生产方法体并用替身记录副作用，不等同于完整游戏运行。
- 实际 Unity 宿主分发和生命周期：22 项，覆盖首击保留页面、第二次确认、共享弹窗、后台／失焦重置。
- 实际 Unity 启动与大厅交互：223 项通过，含真实 UGUI 拖动和按钮点击。
- KR Java Activity 事件序列：28 项，含预测返回、旧按键取消／长按、超时、生命周期和早启动退出竞态。使用 Android 替身执行真实 Activity 源码。
- KR 真实 Java／native 编译通过；62 项 JNI 签名、241 项 C++ 所需符号通过。
- 独立源码复核发现的公共终局弹窗、Gomoku 坏存档弹窗绕过问题已修复并复核关闭。

临时验证代码与证据在 `artifacts/validation/system-back*`、`artifacts/validation/android-double-back`，不加入正式源代码。此前设备验证待用户验收；2026-11-01 修复包已覆盖安装至 OPPO PKM110，手机内 APK SHA256 核对一致。用户随后确认“测试没问题了”，授权合并推送 master。

## 资源一致性

本轮 runtime AAR SHA256：`f094b3900fe7f247211c771890abf69a9708f59becf6a2c9d60d0d370dd0e669`。

塔防 `game.love` 保持已验收版本：`c600660fcf5b487eea66bf6a3cacc56a98530de6b6512696c025cc6a3ac3a046`。进入 Unity 构建前，完整内容校验门禁已通过。

## 构建产物

Release 编译和签名校验通过，正式包名 `com.duanyao.xanaduyonder`，关闭 debuggable，未添加网络权限。原生 Toast 类已确认包含在最终 DEX 中。

本地验收包：`artifacts/builds/XanaduYonder-0.9.4-back-confirmation.apk`，318,515,767 字节。SHA256：`e8b90c95bc48fea6475a53e105867c427b2b8b3428f73e707ca39485f36a5bcf`。安装包内 game.love 哈希与上述稳定内容一致，ZIP CRC 与内容检查通过。

这是基于 0.9.4 的修复验收包，已在 OPPO 上由用户验收；未创建新版本号或远端 Release。

## 合并到最新 master

合并时 master 新增 `ae1113f`（割绳子接入）。保留其大厅入口、独立运行期间的输入阻断和销毁清理；割绳子自己的 Android 双返回策略保持不变。仅统一 AppController 重叠的 Update／HandleBackCore 分支，其他已验收修复代码和游戏资源未变。

合并后重新通过 Unity 编译与 LobbyDesign 的 3817 项断言。逐文件核验：24 个非冲突修复文件与用户验收提交字节一致，55 个非冲突上游文件与 master 字节一致；独立合并审查通过。未重新生成或安装包含割绳子的合并版 APK。
