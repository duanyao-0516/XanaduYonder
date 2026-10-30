# 合成大西瓜：左右越界松手方案

最终设备记录：2026-10-30 已随不带临时探针的整体验证包覆盖安装到手机。用户随后告知已自行测试左右越界投放、上下越界取消和切后台不误投放；代理按要求停止设备操作，没有重复这些手势。编辑器、Core 检查与用户设备复测分开记录。

状态：**用户已批准并完成实现，源码和编辑器回归已验证，用户已完成设备关键场景复测。** 用户需求限定为左右边缘的松手容错；玩法说明新增独立粗体原话“左右越界仍然可以投放，上下越界不可投放”。

## 推荐行为

采用“区域内起手、锁定原指、水平越界仍可投放”。只放宽已开始手势的横向释放范围；保留原棋盘的起手范围与纵向释放范围。

1. 仅从棋盘可交互区域内按下，并且游戏可输入、冷却结束时，记录当前 `pointerId`。区域外起手再移入，不建立手势；冷却期间的按下不排队。
2. 同一指针拖到左右边界外时继续瞄准。屏幕坐标经棋盘 `RectTransform` 转成本地坐标，横向归一化后钳到 `0..1`，再沿既有 Page/Physics/Core 链路按当前水果半径钳位球心。
3. 原指正常抬起、游戏仍可输入、释放点 y 仍在棋盘高度内时，左右边界外也允许投放。左侧球心为 `radius`，右侧为 `Width - radius`；以本次有效释放坐标为准，不缓存最后一个区域内位置。
4. y 超过上边界或下边界时松手，取消本次投放；斜角越界同样按 y 判定。纵向短暂移出后再回到棋盘高度内仍可释放，延续现有行为，不新增“一旦纵向移出就永久取消”。
5. 系统 `Canceled`、`ICancel`、后台/失焦、弹窗、暂停、离页、重开、主题切换和组件禁用，继续结束当前手势。取消后迟到的 Up、缺少合法 Down 的 Up、重复 Up 均不能补投。
6. 多指保持现状：首指锁定后忽略其他指针，不允许第二指接管或投球；现状并非“第二指一出现就取消首指”。第二指若实际点击暂停、设置或返回，仍由原页面流程取消首指。

## 现有调用链与原因

- 修复前，[WatermelonPointer](../../../Assets/Scripts/Runtime/Watermelon/WatermelonPointer.cs) 的 `OnPointerDown` 锁定指针，`OnDrag` 按 ID 跟随，`Position` 已钳位横向坐标；但 `OnPointerUp` 要求完整矩形包含释放点，导致横向越界时预览停在边缘却不投放。现由 `TryPosition` 统一读取有效坐标，释放仅保留纵向范围限制。
- [WatermelonPage](../../../Assets/Scripts/Runtime/Watermelon/WatermelonPage.cs) 第 94–101 行建立棋盘射线区域并连接 Aim/Drop；`RenderPreview` 第 256–265 行按半径约束预览。`Drop` 仍经过 Physics 的投放检查。
- [WatermelonPhysicsWorld](../../../Assets/Scripts/Runtime/Watermelon/WatermelonPhysicsWorld.cs) 的 `CanDrop`/`TryDrop` 按同样的球心钳位位置检查生成空间；[WatermelonGame](../../../Assets/Scripts/Core/Watermelon/WatermelonGame.cs) 的 `TryDrop` 再约束实际球心，并检查冷却、终局、数量和 ID 上限。此方案不改变这些规则。
- [AppController](../../../Assets/Scripts/Runtime/AppController.cs) 的 `OnApplicationPause`/`OnApplicationFocus`、`CreateModal`、`ShowLobby` 都经 `StopAnimation` 同步调用 `WatermelonPage.Suspend`；后者清空指针。`Leave` 和 Pointer 的 `OnDisable` 也清理手势。当前链路没有发现需要在宿主补取消的缺口。

## 方案比较

| 方案 | 收益 | 代价与限制 |
| --- | --- | --- |
| 水平容错，推荐 | 贴边拖动和边外释放一致，不需要额外距离参数；仍保留纵向取消 | 须明确仅接纳原手势的正常 Up，不得把系统取消当释放 |
| 左右有限容差带 | 可限制最多横向越界距离 | 容差带外仍有一道不可见失效边界，需要决定距离与屏幕缩放；只是推远当前问题 |
| 最后位置兜底 | 可在释放坐标不可用时保持旧位置 | 最后区域内采样可能离边缘较远；若缺少正常 Up 也自动投，会破坏取消语义。无必要为此需求增加位置缓存与补投入口 |

## 最窄修改范围与保护

实现修改 `WatermelonPointer` 的坐标读取和释放资格判断：完整矩形包含判断改为“原手势有效 + 正常 Up + 当前可输入 + 本地 y 合法”，横向沿用既有钳位。起手仍受原棋盘射线区域约束，Pointer 内也显式校验本地 x/y 范围。`WatermelonPage.ShowHelp` 仅调整说明文本布局并增加独立 `FontStyle.Bold` 标签；不扩大热区，不新增全屏输入轮询，不修改 Core/Physics、通用 UI、存档协议或其他游戏。

已有保护应保留：按指针 ID 匹配；释放先清空手势；真实触点缺失或 `Canceled` 时不投；Page/Physics/Core 重复核验游戏资格及生成位置。

两处输入边界已在同一 Pointer 内收紧：`TryPosition` 检查坐标转换成功、矩形尺寸和坐标有效，否则取消；触摸 Up 明确要求原触点 `Ended`。未扩展为宿主或输入架构重构。

## 验收范围

| 场景 | 预期 |
| --- | --- |
| 普通点击、区域内拖动松手 | 各投放一次 |
| 左/右边反复微抖后在边外松手；明显横向越界但 y 合法 | 原指正常松手后各一次贴边投放，预览与落点一致 |
| 上下边外或四个斜角外松手；纵向移出后返回再松手 | 前者不投；后者延续原手势正常投放 |
| 区域外起手后移入；冷却期间按下并等冷却结束再松手 | 不投，不补投 |
| 首指拖动，第二指按/移/抬；首指结束后第二指仍按着 | 第二指不影响首指且不接管，不多投 |
| 系统 Cancel、失焦/后台、弹窗、暂停、返回、重开后旧 Up | 不投；恢复后需要新按下 |
| 重复 Up、无 Down 的 Up、坐标转换失败 | 最多一次或零次，不使用旧位置兜底 |
| 各初始水果半径、不同屏幕比例；边缘上方空间不足 | 球体完整留在墙内；空间不足沿用拒绝与提示，不消耗下一颗 |

现有 `Tests/Watermelon` 覆盖 Core 半径钳位、冷却及非法坐标，但其项目只编译 Core，不能证明 UGUI 指针分发正确。新增 `WatermelonPointerChecks` 经真实 Canvas/GraphicRaycaster 和 ExecuteEvents 驱动生产 Pointer，并通过隔离内存页面调用实际 `ShowHelp` 检查粗体原话、文本高度、图鉴/按钮不重叠及输出帮助截图；不调用页面 Initialize、不读写玩家存档。

修复前 RED 已运行：20 项中 12 项通过，4 个水平边外释放、非法坐标和零宽取消、帮助粗体文案检查暴露预期差异。另一个“转换失败”用例最初用旋转 90° 构造近平行平面，未保证实际转换返回 false；首轮修复后 19 项通过，此用例仍失败，因而仅修正夹具为相机后方平面，并明确断言转换确实失败，未为浮点近似增加生产容差。

主线程已串行运行最终检查：`scripts/test-watermelon-pointer.sh` 为 **20 passed / 0 failed**，`scripts/test-watermelon.sh` 为 **36 passed / 0 failed**。指针/帮助报告位于 `artifacts/validation/watermelon-pointer/report.txt`，帮助截图为同目录的 `help-theme-0.png` 和 `help-theme-2.png`。`bash -n scripts/test-watermelon-pointer.sh` 与 `git diff --check` 通过。

首轮修复后的水果/校徽帮助截图已目视核对，粗体原话完整，与说明、图鉴和按钮不重叠。用户已自行测试设备边缘释放和切后台场景；代理没有重复或独立采集原生多指/系统 Canceled 的设备证据。合成鼠标事件与次指 ID 只覆盖 UGUI 处理器的分发和锁定，不能代替完整 Android 原生触摸回归。
