# Dino 黑夜与节日 UI 原图 — 2026-10-28 高速修订版

以下是本工作树实际生产 Unity UGUI 渲染截图，720×1320 竖屏及 1515×720 横屏。黑夜通过真实 Game.Step 跑到第 31 秒进入；节日日期只注入测试 fixture，手机日期未修改。分数和最高纪录为测试数据，不能当作 OPPO 真实游玩记录。

## 黑夜与生日横幅

### 黑夜横屏与生日帽

![黑夜横屏与生日帽](../artifacts/validation/dino-ui-highspeed/birthday-landscape-night-1515x720.png)

### 黑夜竖屏与圣诞帽

![黑夜竖屏与圣诞帽](../artifacts/validation/dino-ui-highspeed/hat-christmas-night-1225-720x1320.png)

### 农历四月十五生日横幅

![农历四月十五生日横幅](../artifacts/validation/dino-ui-highspeed/birthday-selection-0531-720x1320.png)

### 农历五月十六生日横幅

![农历五月十六生日横幅](../artifacts/validation/dino-ui-highspeed/birthday-selection-0630-720x1320.png)

## 全部帽子：白天与黑夜

| 样式 | 受控日期 | 白天 | 黑夜 |
| --- | --- | --- | --- |
| 中国节日礼帽 | 2026-01-01（其余中国节日同款） | [原图](../artifacts/validation/dino-ui-highspeed/hat-festival-0101-720x1320.png) | [原图](../artifacts/validation/dino-ui-highspeed/hat-festival-night-0101-720x1320.png) |
| 万圣节帽 | 2026-10-31 | [原图](../artifacts/validation/dino-ui-highspeed/hat-halloween-1031-720x1320.png) | [原图](../artifacts/validation/dino-ui-highspeed/hat-halloween-night-1031-720x1320.png) |
| 圣诞帽 | 2026-12-25 | [原图](../artifacts/validation/dino-ui-highspeed/hat-christmas-1225-720x1320.png) | [原图](../artifacts/validation/dino-ui-highspeed/hat-christmas-night-1225-720x1320.png) |
| 生日派对帽 | 2026-05-31（农历四月十五） | [原图](../artifacts/validation/dino-ui-highspeed/hat-birthday-0531-720x1320.png) | [原图](../artifacts/validation/dino-ui-highspeed/hat-birthday-night-0531-720x1320.png) |
| 生日派对帽 | 2026-06-30（农历五月十六） | [原图](../artifacts/validation/dino-ui-highspeed/hat-birthday-0630-720x1320.png) | [原图](../artifacts/validation/dino-ui-highspeed/hat-birthday-night-0630-720x1320.png) |

## 本轮布局检查

- [竖屏暂停加粗、移除重复玩法入口](../artifacts/validation/dino-ui-highspeed/ready-720x1320.png)
- [横屏暂停居中并加粗](../artifacts/validation/dino-ui-highspeed/ready-1515x720.png)
- [竖屏玩法弹窗](../artifacts/validation/dino-ui-highspeed/help-720x1320.png)
- [横屏玩法弹窗](../artifacts/validation/dino-ui-highspeed/help-1515x720.png)

截图与报告均归档在本树 `artifacts/validation/dino-ui-highspeed/`，截图没有覆盖绘制或替换其中的 UI。
