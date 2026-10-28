# 数独关卡页与四档续局验证

日期：2026-10-27。开发包仍为 0.4.0 / versionCode 4。本次未发布 Release、推送远端或合并其他分支。

## 范围与隔离

- 独立工作区：`/Users/bytedance/.codex/worktrees/sudoku-game/XanaduYonder`。
- 分支：`p/duanyao.0516/feature/sudoku-game`；修订基线 `897bdd9`。
- 改动限于数独运行时、专用测试及相关文档；未修改公共 AppController、其他游戏规则、品牌素材和版本配置。
- 2,000 道题、编号、答案、分档和提示算法均未更改。题库文件 SHA-256 前后相同：`4dbe98f4eff574129e8e723a54d19bff416843f7e6400c4f0fc2e031fb8b2f71`。

## 自动化与审查

本次运行 268 项检查，全部通过：

| 检查 | 通过数 |
| --- | ---: |
| 2048 规则 / 输入 / 存档 | 43 / 10 / 9 |
| 跳一跳规则 / 存档 | 25 / 18 |
| 旸了个旸规则 / 存档 / 布局 | 31 / 26 / 10 |
| 数独规则 / v1 存档 / 会话 | 27 / 28 / 8 |
| 数独四档 profile / 关卡选择策略 | 26 / 7 |

新增测试先验证缺失实现失败，再实现通过。四档测试覆盖槽位归属、替换隔离、深拷贝、全局通关合并、终局释放、带提示和撤回的 v1 迁移、v2 优先级、严格字段校验、原子写盘及备份恢复。非默认关卡刚进入、零计时零操作的迁移回归测试先失败，修正后通过。

独立需求审查与数据/生命周期审查已完成。审查发现并修正了迁移误删零操作关卡、uGUI 下拉内容多出空白滚动区、底部说明与公共状态栏间距问题。当前 Unity uGUI 无公开展开属性，数独专用 Dropdown 通过创建/销毁回调记录展开状态。`git diff --check` 通过。

题库及解题算法未改动，本次验证文件摘要相同，未重复执行初版的全题库唯一解/逻辑证明批处理；原记录见 [初版验证](verification-sudoku.md)。

## Android 构建与模拟器

Unity 6000.3.27f1 本工作区构建成功，独立使用 ADB 5041 端口执行 Unity 构建，未重启共享默认 ADB 服务。

- APK：`artifacts/builds/XanaduYonder-0.4.0.apk`
- 大小：59,937,387 bytes。
- SHA-256：`1fe66d5e5a03298b1afe3988c18dca416e6989bb620c53f5661d33652497ddca`。
- 构建日志：`artifacts/logs/build-android.log`，结果 `Build Finished, Result: Success.`。

只操作本任务的 `XanaduSudokuValidationApi36` / `emulator-5562`，未操作其他 AI 的模拟器。实际触摸和文件断言确认：

- 原模拟器 v1 对局迁移后完整快照及通关记录相等。
- 临时隔离模拟器存档后验证全新用户：大厅、选关、下拉、玩法页面不生成对局；明确开始入门第一关才生成可继续的槽位。验证后恢复该模拟器原测试数据。
- 入门、简单、中等分别填写正确数字和候选；困难保留原局，其他槽不受影响。
- 四档逐一继续，盘面与历史恢复；强制退出并覆盖安装最终 APK 后，四槽文件与安装前完全一致。
- 同难度另选关卡出现替换确认；取消所有槽不变，确认只替换所选难度。
- 下拉仅过滤关卡；图形箭头翻页，四档分别记忆页码，浏览不改变任何对局。
- 实际三错失败后释放该槽；返回与再次保存不会复活终局。
- 从对局打开书本后阅读等待 3 秒，返回再退回选关，时间仅增加操作间隔 0.367 秒，阅读时间没有计入。
- 两页书本入口、完整规则滚动、下拉展开/返回键、最终选关排版均检查截图。

证据保留于忽略目录 `artifacts/sudoku-refinement/`，包括 `selection-final.png`、`dropdown-final.png`、`rules-selection.png`、`rules-scrolled.png`、四槽恢复 JSON 和交互脚本。

## 物理测试手机

设备 PKM110，序列号 `3B6F4NE8A66ZXTRL`，1080×2373。先让 APP 保存并停止，再备份全部应用 files 和旧 APK；没有卸载或清除数据。

- 备份：`artifacts/phone-sudoku-refinement-20261027-123540/`。
- 安装前后签名 SHA-256 一致：`c199e00ba667927769ace89ff76e257849f7ec2b7ed94792bb8b01da7672d178`。
- `scripts/install-android.sh 3B6F4NE8A66ZXTRL` 返回 `Success` 并启动 APP。
- 手机原 `se-v1-d0-l001` 对局及计时完整迁入入门槽，未创建其他难度的虚假对局。
- `xanaduyonder-v1.json`、`jump-v1.json`、`sheep-v1.json`、`sudoku-v1.json` 均与安装前逐字节一致。
- 手机上进入数独选关页，打开设置旁书本并返回成功；保留用户对局，没有进行填数、重开或替换操作。
- 当前应用进程的 Unity/AndroidRuntime 错误日志为空。

真机图片：`artifacts/sudoku-refinement/phone-selection.png` 与 `phone-rules.png`。手机留在数独关卡页。多档填数/替换/失败等会修改进度的回归在独立模拟器执行，避免改变用户真实存档。
