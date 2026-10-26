# Android 固定存档回归

`scripts/device-fixture.py` 是首版开发 APK 的独立回归工具。只使用 Python 3 标准库和 ADB：普通 `adb shell` 身份读写三个固定外部存档文件，`run-as` 仅验证开发包的应用 UID。App 内没有调试入口，不需要 root，也不会修改权限或 SELinux 配置。实际使用前，先启动一次开发 APK，并从应用日志核对 `Application.persistentDataPath` 为下述目录；路径不同则停止使用本工具。读取时必须能访问该目录，不会把目录权限错误当成“原存档不存在”。

这是基于本次 OPPO Android 16 真机结果的修正：`run-as ... id -u` 成功返回应用 UID，但该身份无法遍历自己的外部存储目录；普通 `adb shell ls/cat` 可以读取同一个存档。工具因此把文件访问身份与开发包验证分开，固定文件范围、原始备份、散列校验和写后回读保护不变。

工具固定包名 `com.duanyao.xanaduyonder`，只允许操作：

```text
/storage/emulated/0/Android/data/com.duanyao.xanaduyonder/files/xanaduyonder-v1.json
/storage/emulated/0/Android/data/com.duanyao.xanaduyonder/files/xanaduyonder-v1.json.bak
/storage/emulated/0/Android/data/com.duanyao.xanaduyonder/files/xanaduyonder-v1.json.tmp
```

设备操作都必须显式传入 `--serial`，不会自动选择设备。工具拒绝 `emulator-*`、设备报告的 QEMU 模拟器、未授权/未连接设备，以及无法 `run-as` 的非开发 APK。`--adb /absolute/path/to/adb` 或 `ADB` 环境变量可以指定 adb 路径。

## 先备份，再注入

在仓库根目录执行，设备序列号按实际连接情况填写：

```bash
python3 scripts/device-fixture.py --serial DEVICE_SERIAL backup
python3 scripts/device-fixture.py --serial DEVICE_SERIAL apply merge
```

`backup` 先停止目标包，得到一致的三个文件快照；不会修改设备文件，完成后保持 App 停止。原始文件字节、存在/缺失状态和 SHA-256 存在被 Git 忽略的 `artifacts/device-fixtures/<serial>/`。已有备份会被保留，命令失败退出；只有显式 `backup --overwrite` 才会替换。**不要在测试局面中覆盖原始备份**，否则备份记录的就是测试数据。

`apply` 必须找到完整且散列校验通过的原始备份，随后只停止目标包，以 `.tmp` 暂存并替换主文件和 `.bak`，回读确认后显式启动 `com.unity3d.player.UnityPlayerActivity`。原始本地备份保持不变。可以依次执行多个 `apply`，最后统一恢复。

App 启动后停留在大厅。每个局面均需点击 **2048** 进入棋盘，然后在棋盘上**向左滑动**。

| 局面 | 注入棋盘 | 预期 |
| --- | --- | --- |
| `merge` | 首行 `2,2,4,0`，其余为 0 | 首次左滑得到两个 4；本步得分 4，再随机生成一个方块；同一步不继续合成 8。 |
| `noop` | 前两行 `2,4,0,0` / `4,2,0,0`，其余为 0 | 左滑无效；棋盘、分数不变，也不生成新方块。 |
| `win` | 首行 `1024,1024,0,0`，其余为 0 | 左滑合成 2048，出现成功提示；继续后可正常游玩。 |
| `loss` | `2,2,8,16` / `32,64,128,256` / `512,1024,2,4` / `8,16,32,64` | 左滑一次合并后只剩一个空格；无论生成 2 还是 4，都应触发结束提示。 |

所有局面均使用正式存档协议：`schemaVersion=1`、`saveKind="xanaduyonder"`、`gameId="2048"`，分数和最高分是字符串 `"0"`，`winState=1`、声音和触感标记均为 1。

## 恢复原始数据

```bash
python3 scripts/device-fixture.py --serial DEVICE_SERIAL restore
```

恢复前再次校验本地备份，停止目标包，再写回原始文件字节并回读核验。原来缺失的文件，仅在 `apply` 日志表明本工具已接管该固定路径时才删除；不会删除其他包、目录或文件。原始 `.tmp` 若存在也会恢复。应用启动前恢复存在状态；应用启动后可能立即创建或更新正常存档。

操作中断时保留备份目录，重新连接同一设备后执行 `restore`。工具会在每次文件写入前记录接管状态，支持从部分注入或部分恢复中重试；未成功启动 App 也不会丢弃原始备份。恢复完成后本地备份继续保留。

## 离线检查

```bash
python3 scripts/device-fixture.py self-test
```

该命令不会启动 adb，也不会连接或修改手机。检查固定 JSON、失败局面两种随机生成结果、命令转义和指定序列号、覆盖保护、校验和损坏、原文件存在状态恢复、部分失败恢复及删除范围。它不能替代真实设备上的权限、路径、页面、动画和生命周期验证。
