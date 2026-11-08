# Q版泡泡堂初代离线接入

2026-11-08 基于已同步的远端 `master`（`0d25dad`）接入。Android、iOS 共用原版游戏和触控适配，大厅新增第 27 号入口；现有 22 个入口保留，共 23 个。初次开发验证使用 0.9.6 / build 15 预览包；正式 Android 内容随 [v0.9.7 / build 16](releases/v0.9.7.md) 发布，iOS 本轮不发布新包。

## 游戏与手机操作

使用 [4399 初代页面](https://www.4399.com/flash/3881.htm) 对应的完整 `game.swf`，由固定 Ruffle 0.7.1 执行。保留原版人机、同屏双人、难度、角色、关卡和规则；没有接入联机服务。

- 横屏中央以原版 550×400 比例完整显示画面并贴齐屏幕底边；工具放到左右操作栏，不再为中央预留顶部栏和底部空白。画面尺寸取完整屏高与两侧最小操作宽度能容纳的最大值，不裁切、不拉伸；侧栏和弹层遵守刘海及手势安全区。
- 单人：左侧移动，右侧放弹；双人：左右玩家各自拥有移动和放弹操作，可同时操作。默认虚拟摇杆，齿轮设置中可切换上下左右方向键，偏好使用独立的 `xanaduyonder.bomb-it.controls.v1` 保存，不改原版排行数据。
- 摇杆使用死区与四向主轴映射，松开立即释放；不把连续角度解释成原版不存在的自由移动。滑出、取消、失焦、后台和退出均清理旧触点。
- 左侧单人／双人头像切换的是触控布局。游戏模式、难度仍在原版菜单选择；选角时移动换角色、放弹确认，双人须分别确认。右侧暂停图标会切换为继续，书本沿用 Unity 原生玩法的 Book 图形；设置和玩法弹层关闭后保留原手动暂停状态。
- 后台暂停并静音，恢复遵从应用声音总开关与页面暂停状态。返回大厅会结束当前对局；原版自身写入的 SharedObject 数据使用独立 origin 保存，不承诺整局续玩。

大厅大 Logo 为原 SWF 的 bitmap 425（151×152）原样导出；小图标为本项目统一绿色描边的原创炸弹 SVG。二者分别用于大卡和紧凑布局。

## 离线净化与边界

原 SWF 为 2,167,060 字节，SHA-256 为 `a2b4cc65115e4b6eb813962d80e91f1e65746a91f27e91d5e86fa1963ed5b54d`。构建先核对输入散列，再按已审查标签删除 `FWAd_AS2` 的两个符号、两个导出和两个初始化动作，并清除 `FileAttributes.UseNetwork`。不重编译游戏动作，不包含站点 `mainload.swf`、`as3.swf`、广告／News 资源或网站页面。

净化 SWF 为 2,163,057 字节，SHA-256 为 `2127f525d471450fe3d39a477b489892b6a500e5e3162debc0cc6a52e2ec632a`。除明确删除的 SDK 标签与网络标志外，保留的标签逐字节一致。FFDec 26.4 独立审计检查 9,964 条动作、3,880 个递归标签：缺失角色引用为 0，对外 URL 操作为 0；剩余 61 份游戏动作字节及反编译文本一致。两处仅控制显示的 FSCommand 保留。

HTML 使用 CSP 和 Ruffle 网络限制；Android 固定离线 HTTPS origin、iOS 固定 loopback 端口 24684，仅提供 SHA-256 清单中的本地资源。宿主拒绝外部导航、弹窗及权限请求。Android 不新增权限，继续从最终 manifest 移除 INTERNET。系统 WebView 引擎由设备提供，包内新增的是 Ruffle WASM／JS、净化 SWF 和触控壳。

## 构建与检查

日常入口仍是 `./scripts/build.sh android` 或 `./scripts/build.sh ios`。前者在 Unity 前构建 `bomb-it-content.aar`，后者通过相同纯构建函数 staging 到过滤后的 iOS 副本。固定输入位于 `ThirdParty/BombItContent/` 和 `ThirdParty/Ruffle/`；生成结果位于 `artifacts/bomb-it/content/`，不会把未净化原 SWF 打入包。

`scripts/bomb-it/build-content.py --verify` 检查输入、净化报告、完整资源和 AAR；`verify-package.py <APK/IPA/APP/Xcode目录>` 检查实际包中的文件集合、大小和散列。Ruffle 两个 WASM 兼容变体及两份原许可保留，开发 source map 不打包。来源和许可见 [THIRD_PARTY_NOTICES](../THIRD_PARTY_NOTICES.md)。

已完成源内容审计、Android Java 编译、iOS ARM64 原生语法编译和 iOS staging 接口检查。真实 Chrome 对净化生产 SWF 的检查通过：原版单／双人进入、选角确认、双摇杆与两放弹键四指操作、死区、滑出捕获、取消／失焦／换布局释放、后台画面冻结与恢复、音量控制，以及销毁与退出确认。扩大布局后补查设置切换与重新加载、方向键多指引用计数／滑动换向、两种操作样式下的安全区与几何；所有图标和操作键在屏内、互不覆盖且不遮挡原画面。844×390 无安全区时画面约 536×390，较旧版约 454×330 增大约 40% 面积；左右各 59px 安全区时仍按可用宽度等比适配。运行期间没有外网请求或 JS 错误。脚本为 `scripts/bomb-it/check-browser.cjs`，通过 `PLAYWRIGHT_MODULE` 指定本机 playwright-core；结果与单／双人、两种控制样式截图在 `artifacts/bomb-it/browser/`。浏览器的音量状态不等于手机扬声器听感。

Android 完整构建通过，APK 为 `artifacts/builds/XanaduYonder-0.9.6-debug.apk`，233,399,543 字节；对比本机接入前 APK 增加 12,404,374 字节（约 12.4 MB）。最终包内容与已审查资源逐文件一致，ARM64、non-debuggable、无 INTERNET 权限、无 Unity Development Console。拉取 master 后曾遇到既有三个网页游戏的旧内容缓存，按各自现有脚本重建后通过，不修改它们的玩法源码。

本轮仅更新 Web 适配器时，Unity 自动复用了原生资源：构建日志明确提示 `player data was not rebuilt`，因此 `packedAssets` 为空，严格资源清单检查停止。按 [Unity 6000.3 缓存说明](https://docs.unity3d.com/6000.3/Documentation/Manual/build-cache-location-reference.html)，将 Android 的 `Library/PlayerDataCache/Android/DataBuildDirtyInfo.json` 可逆备份到 `artifacts/bomb-it/build-cache-backup/`，随后使用原统一入口触发资源重建。没有跳过清单检查、修改玩法或清空全局构建缓存。

重建后重新生成 1,124 条真实资源清单，统一构建脚本以 0 退出，全部包内检查通过；扩大布局的新包再次向 OPPO 覆盖安装，ADB 返回 `Success`。两次安装都没有执行真机游戏自动操作。

按用户要求，已向连接的 OPPO 覆盖安装现有 `com.duanyao.xanaduyonder.debug`，ADB 返回 `Success`；没有卸载、清数据或操作游戏验证。

iOS 上一版接入的 Unity 导出和 Xcode Release 真机构建已成功，沿用 `com.duanyao.xanaduyonder.iosnative` 及既有开发签名，签名完整性和实际 APP 的泡泡堂资源散列通过。本轮扩大画面／图标／控制方式设置后，按用户要求暂停 iOS 编译与安装；旧 `artifacts/ios/DerivedData-web-games/Build/Products/Release-iphoneos/XanaduYonder.app` 不包含本轮布局改动，后续安装前须重新构建。尚未进行 Android／iPhone 真机玩法、声音、性能或双人手感验收，按用户要求由用户操作设备验证。
