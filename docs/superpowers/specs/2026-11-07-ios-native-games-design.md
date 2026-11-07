# iPhone / iPad 原生小游戏版本

用户已授权在独立分支构建并尝试安装到连接的 iPhone 17 Pro，保留大厅及所有 Unity 原生玩法，暂时排除切水果、只有一道门、经典黄金矿工、赵云与阿斗、割绳子、王国保卫战。

## 设计

- 分支：`p/duanyao.0516/feature/ios-native-games`，基于当前 master，不推送或合并。
- `UNITY_IOS` 下大厅保留 19 个入口；稳定 ID、分类、排序及纯 Unity 黄金矿工保持现状。战争进化史的一代和二代仍共用一个入口。
- 独立 iOS 构建入口使用现有 Bootstrap 场景、IL2CPP、Metal、iPhone+iPad、旧 Input Manager，应用标识 `com.duanyao.xanaduyonder.iosnative`。
- 构建在 `artifacts/ios/project` 的副本进行，原工作树的 Assets 和 ProjectSettings 不由构建修改。副本排除 Android 插件、六款游戏的专属 Resources、切水果 StreamingAssets。导出路径为 `artifacts/ios/xcode`。
- Android 专属 Editor API 使用条件编译；不把 Android Prepare 方法用于 iOS。
- 免费账号签名通过 Xcode Personal Team 或用户自行选择的侧载工具完成。账号密码与配对秘密不进入仓库、脚本或日志。自动续签方案本轮核实说明，不擅自开通远程签名服务。

## 验证与限制

先验证 iOS 大厅实际入口集合，再导出并核查资源清单。Xcode 可用后编译真机 app，签名成功后安装、启动。明确分别记录导出、原生编译、签名、安装和真机交互结果。未连接 iPad，不宣称 iPad 真机验证。当前触感只有 Android 实现，本轮不新增触感功能。
