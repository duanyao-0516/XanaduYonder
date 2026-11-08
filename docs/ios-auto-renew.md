# iOS 官方工具链自动续签

本机使用已登录的 Xcode Personal Team，以及 Apple 的 `xcodebuild`、`security`、`codesign`、`devicectl`。不使用第三方签名服务，不导出签名私钥，不给游戏增加联网功能。定时调度已改由 macOS `launchd` 负责，关闭 Codex 后仍能运行。

## 日常运行条件

- Mac 需要开机并登录当前用户；执行时需要联网、钥匙串可用，Xcode 账号和签名身份需有效。Codex 和 Xcode 的界面均无需一直打开；退出 macOS 用户登录或关机后，用户级 LaunchAgent 不运行。
- 当前任务为 `local.xanaduyonder.ios-renewal`，每天本机时间 **00:15、06:15、12:15、18:15** 检查，登录加载时也检查一次。使用 `StartCalendarInterval`，睡眠期间漏过的检查会在唤醒后合并补一次。任务不主动唤醒电脑；执行期间用 `caffeinate -i` 临时避免空闲睡眠，不更改系统电源设置，也不能阻止合盖或主动睡眠。
- 原 Codex 任务 `iphone` 已暂停，不再承担调度。运行结果保存在本地日志；目前没有额外桌面通知程序或消息推送。
- iPhone 通过 USB 或已配对的无线连接可达。无线自动发现通常要求同一可互通局域网；两边仅能访问互联网并不足够，访客 Wi-Fi 隔离或网络过滤也可能阻止连接。
- 每 6 小时检查一次。手机上已安装签名剩余超过 72 小时时不重新申请；接近到期才尝试。手机不可达时延后，下一次检查重试。
- 为降低打断游戏的风险，检测到游戏进程且手机未锁定时延后。不会主动终止或打开游戏。
- 手机实际版本与安装记录不一致时停止更新，避免把后来手工安装的新版本覆盖回旧快照。同版本号、同 build 号的手工替换无法仅凭设备版本信息识别；发布新包时应增加 build 号并同步快照与记录。
- 手机离开网络不会立即使应用失效，但如果直到已安装签名到期仍未成功覆盖更新，应用会暂时不能打开。重连后重新覆盖安装，保留原应用数据；不要先卸载。iOS 的开发者信任联网验证是独立步骤，自动续签不能保证绕过它。

## 固定输入与记录

运行目录为 `~/Library/Application Support/XanaduYonder/ios-renewal`，不依赖 Desktop 下的代码仓库。里面的 `config.json`、`ledger.json` 和固定 `accepted-source.zip` 不进入 Git。ZIP 保存已验收的 Unity Xcode 导出，并以 SHA-256 校验；后续主项目编辑和诊断导出不会自动进入续签包。重新发布游戏版本时，需要先完成验收，再更新这一快照及对应的已安装版本记录。

定时任务使用运行目录内的 `run-ios-renewal.sh` 和 `renew-ios.py` 固定副本，wrapper 先校验执行器自身 SHA-256，再调用系统 Python；正式源码位于 `scripts/` 下同名文件。启动参数显式使用 `/bin/zsh -f`，并设置 Xcode 工具目录与最小 PATH，不依赖用户 shell 启动文件。因此切换或删除代码工作树不会自动换掉已验收的执行器。修改执行器后，应重新验证、更新固定副本和 wrapper 中的哈希。

LaunchAgent 配置位于 `~/Library/LaunchAgents/local.xanaduyonder.ios-renewal.plist`。调度日志位于 `~/Library/Logs/XanaduYonder/ios-renewal/schedule.stdout.log` 和 `schedule.stderr.log`；实际续签的详细命令日志位于同目录的 `commands/`。这些文件使用当前用户权限，不需要 root 或新的全盘访问授权。旧 `artifacts/ios/auto-renew/config.json` 已指向同一份运行目录和台账，避免两份安装记录分叉；迁移前配置和台账保留在 `launchd-migration-backup/`。

`ledger.json` 分别记录 `installed` 和 `pending`。新包构建或签名成功只代表待安装；只有设备安装命令返回成功，才更新已安装到期时间。

```bash
renewal_root="$HOME/Library/Application Support/XanaduYonder/ios-renewal"

# 只读状态，不构建、不连接设备、不更新记录
"$renewal_root/run-ios-renewal.sh" --status

# 让 macOS 立即执行一次正常检查；不用 -k，避免终止正在运行的续签
launchctl kickstart "gui/$(id -u)/local.xanaduyonder.ios-renewal"

# 查看系统任务状态、运行次数和最近退出码
launchctl print "gui/$(id -u)/local.xanaduyonder.ios-renewal"

# 手工演练：即使未到阈值也尝试续签，仍执行所有安全校验
"$renewal_root/run-ios-renewal.sh" --force
```

正常检查完成后进程会退出，因此 `launchctl print` 显示 `state = not running`、`last exit code = 0` 是正常待机状态；任务仍按已登记的日历继续触发。

## 更新过程与验收

1. 使用进程锁避免两个任务重叠；检查目标设备，在构建前和覆盖安装前分别核对手机实际应用版本与安装记录。
2. 只备份和移开解码后 Team 与完整 App ID 都精确匹配的本地描述文件；同时检查新旧缓存目录，不清空其他应用或共享通配描述文件，不撤销证书。
3. 从固定快照构建，使用全新的 DerivedData，防止 Xcode 增量构建只换 profile 而跳过 CodeSign。
4. 检查签名完整性、ARM64、应用标识、版本、团队、目标设备、签名证书和有效期。新授权的到期时间必须严格晚于已安装版本，且新构建剩余至少 120 小时；复用原 profile 不算成功。
5. 覆盖安装同一应用。失败时保留可重试的新包，已安装记录保持原值；构建或验证失败时恢复本地旧 profile 备份。新的日志和备份保留在上述 Library 目录；迁移前的证据仍在本地 artifacts。

## 实证记录

- 精确移开本应用缓存后，Xcode 已签发新 profile：原到期时间为 `2026-11-14T16:12:20Z`，新到期时间为 `2026-11-15T07:03:26Z`，签名证书相同。证据位于 `artifacts/ios/renewal-proof/`。
- 首次增量构建虽显示成功，但未再次 CodeSign，严格签名校验失败；随后 clean build 与严格签名校验通过。未把失败包安装到手机。
- USB 拔线后，当前网络能 ping 通手机，但 Xcode/CoreDevice 无线服务仍不可用。实际执行器返回 `deferred/device_not_connected`，没有推进安装记录。不能把网络可 ping 通当作无线续签验证通过。
- 首次自动执行器实测遇到 `No Accounts`；原工程直接运行相同 Xcode 命令也失败。普通账号配置列表为空，重启 Xcode 后界面也显示未登录，用户随后重新登录。保留现有有效签名证书，失败流程已恢复本应用的描述文件缓存；此类账号状态变化仍可能需要本人重新登录。
- 最终执行器通过 27 项临时检查，覆盖 profile 精确备份与失败恢复、安装失败不推进记录、待安装包复用、错误签名拒绝、进程锁、超时终止、归属目录清理，以及手机实际版本改变时拒绝覆盖。测试仅保存在 artifacts，未加入正式测试源码；独立审查发现的版本保护缺口已补齐。
- `2026-11-08T07:30:32Z`，正式执行器在 USB 连接、iPhone 锁屏状态下完成申请授权、从固定快照全量构建、严格验签和覆盖安装。新 profile 到期时间为 `2026-11-15T07:29:15Z`，即北京时间 **2026-11-15 15:29:15**；证书未改变，版本仍为 `0.9.5` / `14`。证据为 `artifacts/ios/auto-renew/real-run-result-4.json` 及其中的日志目录。
- 安装前后 Documents 中 29 份存档与备份文件逐字节一致，证据为 `save-verification.json`。安装后再次 `--check` 返回 `not_due`，没有无意义地再申请授权或安装。任务不会主动启动游戏，本轮验证的是续签、安装和数据保留，不冒充重新完成玩法听感或触控回归。
- 首版 Codex 自动任务曾创建并通过工具回读及持久化配置核对，随后按用户要求暂停并迁移到 macOS LaunchAgent；固定执行器 SHA-256 为 `e2e4f6b7d2d6b6675172023e33de26297df9b0244731843bb9a878926b20b732`，固定导出 ZIP 的 SHA-256 为 `0018734142a628b8512d7b746df2dd774063f79587f71f24ea9c8956f8105490`。
- macOS 调度迁移通过：plist 语法校验成功，正式任务真实执行 `--check` 并以 0 退出。wrapper 拒绝被更改的执行器；最终 `/bin/zsh -f` 启动参数、清空继承环境后的工具链解析均已验证。并发检查正确返回 `another_renewal_is_running`，没有重复构建。
- `2026-11-08T07:46:19Z`，临时、无重复计划的 LaunchAgent 完成完整后台续签、严格验签和 USB 覆盖安装，观察到执行器的父进程为 PID 1（launchd），不依赖 Codex 进程存活。签名证书不变，新期限为 `2026-11-15T07:39:32Z`，即北京时间 **2026-11-15 15:39:32**；29 份存档及备份逐字节一致。临时任务随后已卸载，永久任务保持 `--check`，再次实跑返回 `not_due`、退出码 0。汇总证据为 `artifacts/ios/auto-renew/launchd-completion-verification.json`。
- 四个日历触发项及登录加载已通过持久配置和 `launchctl print` 核对；本轮未主动让 Mac 睡眠、关机或退出用户登录，也未等待下一次六小时定时触发，不把这些场景写成实机回归通过。

## 官方依据

- [Apple：让 Xcode 请求新的自动签名描述文件](https://developer.apple.com/help/account/provisioning-profiles/edit-download-or-delete-profiles/)
- [Apple：无线设备连接](https://help.apple.com/xcode/mac/current/en.lproj/dev3e2f4ee6d.html)
- [Apple：描述文件的内容与有效期](https://developer.apple.com/documentation/technotes/tn3125-inside-code-signing-provisioning-profiles)
- [Apple：launchd 定时任务及睡眠行为](https://developer.apple.com/library/archive/documentation/MacOSX/Conceptual/BPSystemStartup/Chapters/ScheduledJobs.html)
