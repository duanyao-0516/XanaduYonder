# 跳一跳

稳定游戏标识为 `jump`。在游戏区域按住蓄力，松手自动朝下一个平台中心起跳；编辑器支持鼠标或空格键。蓄力不超过 1.35 秒，距离为 `0.35 + 3.6 × 秒数`，达到上限后保持最大距离。

落到新平台得 1 分；距平台中心不超过 0.16 世界单位时，连续中心奖励依次为 2、4、6…分。普通落台或留在原平台会中断连击。短跳仍在原平台时可继续，不消耗下一平台随机数；落空结束。重开需确认，最高分保留。

## 模块与生命周期

- `Core/Jump` 只依赖 C#，拥有平台、落点、计分、步数、连击和确定性随机状态。坐标在成功落台后归零，快照导入/导出复制隔离。
- `JumpPage` 管理对局、独立存档、输入与弹窗请求。正常松手立即提交模型和存档；后续动画不会再次计分。暂停、失焦、弹窗和离页取消未提交蓄力，并显示已提交结果。
- `ChargeGesture` 保留最初手指的所有权，取消事件不能变成跳跃，第二个手指不能结束第一个手指的蓄力。
- `JumpSceneView` 和 `JumpSceneGeometry` 使用原创三维网格与材质，独立正交相机渲染到 UI 的 RenderTexture。平台尺寸来自规则模型；相机向左右上方推进。离页禁用相机/场景，退出应用销毁自建网格、材质和纹理。
- `AppController` 仅管理公共路由、设置与弹窗，2048 的规则及原存档编解码不变。

## 独立存档

文件为 `Application.persistentDataPath/jump-v1.json`，带 `.bak` 和 `.tmp`。根字段固定为 `schemaVersion=1`、`gameId="jump"`、`game`、`bestScore`。游戏字段包含平台半边长、目标 X/Z、玩家 X/Z、分数、步数、连击、结束标记和非零随机状态。分数与最高分使用非负 64 位十进制字符串，其余数值按实际 JSON token 严格检查。

读取拒绝缺失/null/额外字段、重复键、错误类型、未知版本、非有限或越界坐标、不可能的计分状态，以及超过 64 KiB 的文件。写入使用 flush 和原子替换，仅以合法主档更新备份；损坏主档恢复备份，不影响旧 2048 文件。存储失败会提示且可继续游玩，后续写入成功清除错误提示。

## 素材

项目所有者提供 `跳一跳素材.zip`，并明确确认已经获得免费分发授权。使用其中 6 个音效与 2 张大厅预览图片；三维场景使用自建网格。完整说明见 `THIRD_PARTY_NOTICES.md`。参考仓库 `dee1024/jump-jump-game` 是 Java/ADB 辅助工具，仅用于观看演示，未引入其代码或素材。

## 验证命令

```bash
scripts/test-jump.sh
scripts/test-jump-persistence.sh
scripts/test-core.sh
scripts/test-input.sh
scripts/test-persistence.sh
scripts/build-android.sh
scripts/install-android.sh DEVICE_SERIAL
```

安装开发包会保留同签名已有数据。正式签名与开发签名仍不同，不要通过卸载来强行切换签名并丢失对局。
