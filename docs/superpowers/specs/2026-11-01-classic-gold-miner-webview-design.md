# 经典黄金矿工离线 WebView 接入

用户于 2026-11-01 批准开发：新增独立“经典黄金矿工”大厅入口，保留 Unity 黄金矿工代码、入口和旧存档；同时接入单人和双人原版 H5，加入独立存档和退出后续局。

## 职责

- Unity 只负责大厅 item、进入/返回和公共声音初始设置；不重写游戏规则。
- Android 独立横屏 Activity 使用系统 `android.webkit.WebView`，不包含 Chrome/Chromium 内核。大厅保留在下层，退出恢复方向和输入。
- APK 内的 H5 资源负责原玩法、原数值、手机输入及状态快照。只允许本地 HTTPS origin，拒绝其他 URL、导航、新窗口及联网。
- 删除单人包的广告/赞助启动链、GA 初始化/调用/定时器、外链、站点 document.domain；保留实际本地素材预加载。双人包不引入平台壳。

## 交互

- 大厅新增 item，内部选择单人/双人；有各自存档时提供继续和重新开始，重新开始需确认。
- 两种模式均横屏，游戏画面等比显示，控制区避让 cutout 和系统手势边缘。
- 游戏放钩第一次触摸即执行，炸药有独立触控按钮；双人分别控制左右玩家，支持同时触摸、取消和重复动作隔离。
- 商店第一次点击商品只选中并显示说明/价格，第二次点击同一商品才购买。切换商品重置确认，购买成功后不能重复购买。
- Android 第一次完成系统 Back 显示提示，2 秒内第二次完成 Back 保存并退出大厅；取消手势及唤出系统栏不计数，超时/后台清空计数。不使用网页 history 处理大厅返回。

## 持久化与生命周期

- 新存档仅使用 `classic-gold-miner-solo-v1.json` 和 `classic-gold-miner-duo-v1.json`；不读取/写入旧 `gold-miner-v1.json`。
- 保存版本、模式、游戏场景、关卡、钱包/贡献、道具、商店商品价格和购买状态、时间、地图实体和钩子运动状态。恢复保留同局，不重新随机地图或商店，不退回关卡起点冒充续局。
- 保存接口同步返回成功/失败；退出前暂停并保存，失败显示错误且不假称保存成功。Android 使用原子文件；浏览器验证使用独立 localStorage key。
- 后台暂停游戏、音频及输入；回前台显示继续遮罩，不追赶后台经过的计时。退出销毁 WebView。定期保存及关键操作保存覆盖进程意外结束。

## 共享接口（实现者必须遵守）

```javascript
// shared/host.js，先于游戏脚本加载。
ClassicHost.read(mode); // 返回模式的原始状态对象/null；损坏数据抛错且保留原件。
ClassicHost.save(mode, state); // 同步 boolean，state 需有 schema:1 和 mode。
ClassicHost.register({mode, capture, pause, resume});
ClassicHost.checkpoint(); // capture + save，返回 boolean。
ClassicHost.pauseAndSave(reason); // Android evaluateJavascript 调用，返回 boolean。
ClassicHost.foreground(); // 显示继续遮罩；点击后 adapter.resume。
ClassicHost.message(text); // 可见消息。
// adapter.capture():纯 JSON；pause()/resume() 仅管理游戏时钟/声音/输入。
// 各模式负责严格校验自己的 schema 和恢复逻辑，read() 不替代状态校验。
```

```java
// JavaScriptInterface 名称 ClassicGoldMinerNative，仅可信本地页可见。
String readSave(String mode); // 空串=无存档；原始JSON由shared host解析。
boolean writeSave(String mode, String json); // 白名单solo/duo，长度限制，AtomicFile。
String getLastSaveError();
boolean isSoundEnabled();
// Android -> JS: ClassicHost.pauseAndSave('background'|'exit'); ClassicHost.foreground();
```

各模式可以在状态里保留自己的版本/校验字段；read/save 的 mode 参数仅为 `solo` 或 `duo`。页面路径分别为 `solo/index.html`、`duo/index.html`，`?resume=1` 恢复，`?new=1` 明确新局。模式选择页不注册游戏 adapter。

## 验收

验证单人与双人开始、放钩、商店两次点击、双指同时操作、各模式独立保存、钩子运动中退出/重启恢复、商店状态恢复、后台暂停、2 秒返回、切换大厅与旧版存档不变；检查 APK 权限/无外网请求/无内核文件。构建与设备操作串行，由主代理执行。用独立测试包名安装，不覆盖正式版存档。原始下载包保留，适配副本单独追踪来源和修改。

## User-approved fullscreen revision (2026-11-01, emulator feedback)

The user rejected the reserved header/side/bottom control panels because they made the actual game too small. Gameplay must fill the landscape viewport. The mine expands horizontally for wide screens, with proportional characters/ore and unchanged item values, goals and timers. Solo uses immediate first-touch drop anywhere in the mine, with a floating bottom-right bomb. Duo assigns the left and right mine halves to the corresponding miner; bottom-left and bottom-right bombs control their own miner (explicitly confirmed). Both pointers must work simultaneously, and pointer cancellation/dragging must never transfer ownership or double-fire. HUD and bomb touches must not fall through to drop.

The canvas/background can draw edge-to-edge; interactive overlays and essential HUD text must honor cutouts and system gesture insets. Bomb targets are at least 48 CSS pixels, use original game artwork, show remaining inventory, and never consume an item when unavailable. No external icon/font resource is added. Mode selection and shop remain touch-friendly; merchandise and price labels share enlarged non-overlapping targets, with the existing first-select/second-buy rule.

Existing preview saves must continue to load. Any expanded-world geometry must carry a bounded saved width and restore/reflow all dependent position, hook, rope and movement state consistently; saves without it use the original width. Old Unity data remains untouched. System WebView/offline restrictions and two-Back exit remain unchanged. Validate the actual final APK at 800×360 and 640×320 CSS-sized emulator displays, including both simultaneous mine touches, both bomb controls, shops, background pause and cross-layout resume.
