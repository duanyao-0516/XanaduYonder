# 系统返回防误退实现与验证

目标：游戏内系统返回即将退回 XANADU 大厅时，首次显示“再按一次返回大厅”，2 秒内第二次才退出。游戏按钮、关闭弹窗、收起键盘、暂停和返回选关保持原行为，大厅退出应用的确认弹窗保持原行为。

基线：origin/master `1f99287`（0.9.4），工作分支 `p/duanyao.0516/bugfix/android-double-back`。保留当前工作树原有未跟踪的资源裁剪实验，不修改已验收 game.love。

1. Unity 继续用已有 Android 返回 → Escape → AppController 链路。AppController 持有按页面隔离、以实时时钟计算的确认状态，游戏页面只在真正离开前调用确认函数，异步转屏前也必须确认。无参数 Back 保持给页面按钮使用。非退出返回、页面切换、后台或失焦清除确认状态。
2. Android Toast 独立在 UI 线程显示、取消，Unity 与 Android 生命周期成对清理。显式保持 Input.backButtonLeavesApp=false。
3. 王国保卫战独立 Activity 注册 Android 13+ OnBackInvokedCallback，和旧系统返回统一计数。只处理有效松手、忽略长按和取消；首次 Toast，二次 nativeSendQuit，通过 love.quit/SDL_main/teardown 原路径回大厅，不直接 finish 正常运行时。后台、失焦、销毁重置。
4. 临时验证覆盖首击、2 秒边界、超时、不同页面、失焦、弹窗不计入退出；检查各游戏的真实 Back 分支。Java 编译/JNI 检查和 Unity Android 编译后，独立审查代码与资源未变证明。

验证资料放 artifacts/validation/system-back；不把临时自测代码加入正式交付。不自动发布新版本，不改已有发布标签。
