# Dino 黑夜与节日 UI 原图 — 2026-10-28 高速修订版

当时的截图保存在本地 `artifacts/validation/dino-ui-highspeed/`，下表和各节记录该目录下的文件名。运行 [Dino 编辑器画面检查](../scripts/test-dino-visual.sh) 可为当前源码生成一组截图和报告，输出到 `artifacts/validation/dino-ui/`。

下列文件是当时在调研工作树以生产 Unity UGUI 渲染的截图，720×1320 竖屏及 1515×720 横屏。黑夜通过真实 Game.Step 跑到第 31 秒进入；节日日期只注入测试 fixture，手机日期未修改。分数和最高纪录为测试数据，不能当作 OPPO 真实游玩记录。

## 黑夜与生日横幅

### 黑夜横屏与生日帽

`birthday-landscape-night-1515x720.png`

### 黑夜竖屏与圣诞帽

`hat-christmas-night-1225-720x1320.png`

### 农历四月十五生日横幅

`birthday-selection-0531-720x1320.png`

### 农历五月十六生日横幅

`birthday-selection-0630-720x1320.png`

## 全部帽子：白天与黑夜

| 样式 | 受控日期 | 白天 | 黑夜 |
| --- | --- | --- | --- |
| 中国节日礼帽 | 2026-01-01（其余中国节日同款） | `hat-festival-0101-720x1320.png` | `hat-festival-night-0101-720x1320.png` |
| 万圣节帽 | 2026-10-31 | `hat-halloween-1031-720x1320.png` | `hat-halloween-night-1031-720x1320.png` |
| 圣诞帽 | 2026-12-25 | `hat-christmas-1225-720x1320.png` | `hat-christmas-night-1225-720x1320.png` |
| 生日派对帽 | 2026-05-31（农历四月十五） | `hat-birthday-0531-720x1320.png` | `hat-birthday-night-0531-720x1320.png` |
| 生日派对帽 | 2026-06-30（农历五月十六） | `hat-birthday-0630-720x1320.png` | `hat-birthday-night-0630-720x1320.png` |

## 本轮布局检查

- 竖屏暂停加粗、移除重复玩法入口（`ready-720x1320.png`）
- 横屏暂停居中并加粗（`ready-1515x720.png`）
- 竖屏玩法弹窗（`help-720x1320.png`）
- 横屏玩法弹窗（`help-1515x720.png`）

历史截图直接来自生产 UGUI 渲染。
