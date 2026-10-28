using System;
using System.Collections.Generic;
using System.Globalization;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    public sealed class ZumaHelpSection
    {
        public readonly string Title, Body;
        public ZumaHelpSection(string title, string body) { Title = title; Body = body; }
    }

    public sealed class ZumaHelpTerm
    {
        public readonly string English, Chinese, Function;
        public ZumaHelpTerm(string english, string chinese, string function)
        { English = english; Chinese = chinese; Function = function; }
    }

    public sealed class ZumaHelpDifficulty
    {
        public readonly string Id, English, Chinese, Description;
        public readonly ZumaDifficulty Rules;
        public ZumaHelpDifficulty(string id, string english, string chinese, string description)
        { Id = id; English = english; Chinese = chinese; Description = description; Rules = ZumaDifficulty.Get(id); }
        public string Parameters
        {
            get
            {
                // Keep displayed parameters tied to the playable configuration, not a second table.
                return string.Format(CultureInfo.InvariantCulture,
                    "{0} 种颜色 · 初始 {1} 球 · 超过 {2} 分停止生成 · 默认 ACE {3} 秒",
                    Rules.ColorCount, Rules.StartCount, Rules.GaugeScore, Rules.AceTimeSeconds);
            }
        }
    }

    // Copy only: this catalog does not hold a session, load resources, or write settings/save data.
    public static class ZumaHelpContent
    {
        public static IReadOnlyList<ZumaHelpSection> Sections { get; } = Array.AsReadOnly(new[]
        {
            new ZumaHelpSection("目标与选关", "阻止彩球进入骷髅终点。用同色球消除球链，分数严格超过当前难度的门槛后停止生成新球，再清空剩余球链即可获胜。已有球入洞则失败，达到门槛不等于立即获胜。18 张地图和 4 档难度均可直接选择，共 72 种组合，不需要逐关解锁。"),
            new ZumaHelpSection("发射弹珠", "在游戏区域按下即可瞄准，不需要等待长按；拖动调整方向，正常松手发射一颗球。青蛙嘴中的球是当前发射球，背上的小球是下一颗。射入球链后，相邻同色球达到 3 颗或以上就会消除。发射有短暂间隔，冷却期间松手不会排队补发。"),
            new ZumaHelpSection("取消瞄准与交换弹珠", "一次瞄准只跟随最先按下的手指；在游戏区域外松手可取消。点击 SWAP 可交换当前球与下一颗球，不会自动发射，也没有次数消耗。换球、打开菜单或切到后台都会取消正在进行的瞄准，回来后需要重新按下。"),
            new ZumaHelpSection("自动回退与连锁", "消除中间一段后，球链会出现空隙；空隙两端同色时，前段会自动回退、向后吸合。合拢后再次形成至少 3 颗同色球，可触发连锁消除并获得额外奖励。回退由球链规则触发，没有手动回退按钮，也不是额外道具。可以提前安排断口两侧的颜色。"),
            new ZumaHelpSection("金币", "金币会周期性出现在地图上。用弹珠射中可获得 500 分，同时消耗该发弹珠，并中断连续射击消除的 CHAIN 计数。金币出现约 10 秒后开始闪烁，再约 3 秒消失；看准球链空隙再射击，弹珠被球链挡住就无法击中后方金币。金币是本局得分奖励，不是商店货币。"),
            new ZumaHelpSection("计分与隧道", "普通消除每颗球 10 分。连续有效射击消除从第 5 次起有额外奖励；射空或插入后未消除会中断这段 CHAIN。自动回退形成的 COMBO 另有连锁奖励，两种统计不同。进入隧道的隐藏球暂时不能被命中，等它们重新露出再瞄准。"),
            new ZumaHelpSection("难度与时间", "难度同时改变颜色数、初始球数、推进速度、近终点缓速和分数门槛，并非只增加速度。Rabbit 适合先熟悉操作。YOUR TIME 记录有效游戏时间；ACE TIME 是本图本难度的参考成绩，有最佳时间时会使用该成绩，它不是失败倒计时。胜利后的收尾动画还会加分，最终成绩以结算页为准。"),
            new ZumaHelpSection("暂停、续局与返回大厅", "MENU 或 Android 返回键可暂停；BACK TO GAME 继续。离开选关或返回大厅会保留当前对局，重新进入可选择 CONTINUE。后台回来先停在暂停页；新开局需确认替换未结束的对局。进度自动保存，意外终止可能回到最近一次成功保存处；出现“进度未保存”时请先解决存储问题。声音和触感与 Xanadu 的设置共用。")
        });

        public static IReadOnlyList<ZumaHelpDifficulty> Difficulties { get; } = Array.AsReadOnly(new[]
        {
            new ZumaHelpDifficulty("rabbit", "Rabbit", "兔子", "颜色较少，适合熟悉瞄准、交换和连锁；接近终点时有明显缓速保护。"),
            new ZumaHelpDifficulty("eagle", "Eagle", "雄鹰", "增加一种颜色和初始球数，常规推进比 Rabbit 更快；接近终点仍有缓速保护。"),
            new ZumaHelpDifficulty("jaguar", "Jaguar", "美洲豹", "使用 6 种颜色，接近终点不再额外缓速。常规推进略慢于 Eagle，但配色与终点压力更难处理。"),
            new ZumaHelpDifficulty("sun_god", "Sun God", "太阳神", "使用 6 种颜色，四档中常规推进最快；接近终点没有额外缓速，适合熟悉连锁后挑战。")
        });

        public static IReadOnlyList<ZumaHelpTerm> Terms { get; } = Array.AsReadOnly(new[]
        {
            new ZumaHelpTerm("MAIN MENU", "返回大厅", "退出 Zuma 选关页，回到 Xanadu 竖屏大厅；未结束对局保留。"),
            new ZumaHelpTerm("BACK", "上一张 / 返回", "在选关页切换到上一张地图；在存档错误弹窗中则返回大厅。"),
            new ZumaHelpTerm("NEXT", "下一张", "在选关页切换到下一张地图。"),
            new ZumaHelpTerm("PLAY", "开始游戏", "按当前地图和难度开局；已有未结束对局时会先请求确认替换。"),
            new ZumaHelpTerm("Rabbit", "兔子", "第一档难度，4 种颜色；可先用这一档熟悉游戏。"),
            new ZumaHelpTerm("Eagle", "雄鹰", "第二档难度，5 种颜色，更多初始球。"),
            new ZumaHelpTerm("Jaguar", "美洲豹", "第三档难度，6 种颜色，近终点不额外缓速。"),
            new ZumaHelpTerm("Sun God", "太阳神", "第四档难度，6 种颜色，常规推进最快。"),
            new ZumaHelpTerm("BEST SCORE", "最高分", "当前地图和难度下，已获胜对局的最高分。"),
            new ZumaHelpTerm("BEST TIME", "最佳时间", "当前地图和难度下，已获胜对局的最短有效用时。"),
            new ZumaHelpTerm("CONTINUE", "继续", "恢复当前尚未结束的对局，使用上次保留的进度。"),
            new ZumaHelpTerm("CONTINUE?", "是否继续", "提示已有对局；可继续，也可先返回选关查看地图。"),
            new ZumaHelpTerm("SELECT LEVEL", "选择关卡", "关闭继续提示并回到选关，保留当前未结束对局。"),
            new ZumaHelpTerm("NEW GAME?", "是否新开一局", "确认后用新对局替换当前未结束对局，已有最佳成绩保留。"),
            new ZumaHelpTerm("MENU", "菜单", "暂停当前游戏，查看声音、触感和返回选关等选项。"),
            new ZumaHelpTerm("SWAP", "交换弹珠", "交换当前发射球与下一颗球，不发射；也会取消当前瞄准。"),
            new ZumaHelpTerm("SOUND", "声音", "同时控制音乐和音效，与 Xanadu 全局声音开关共用。"),
            new ZumaHelpTerm("HAPTICS", "触感", "控制有效操作的震动反馈，与 Xanadu 全局触感开关共用。"),
            new ZumaHelpTerm("RETURN TO LEVEL SELECT", "返回选关", "保存并暂停当前对局，回到地图和难度选择页。"),
            new ZumaHelpTerm("BACK TO GAME", "返回游戏", "关闭暂停菜单，继续当前对局。"),
            new ZumaHelpTerm("LVL / LEVEL", "关卡", "显示当前地图对应的关卡编号或名称。"),
            new ZumaHelpTerm("ZUMA!", "清链成功", "进入胜利收尾阶段；等待奖励结算完成。"),
            new ZumaHelpTerm("STATS", "成绩统计", "展示本局最终分数、连锁、金币和时间。"),
            new ZumaHelpTerm("GAME OVER", "游戏结束", "球链已进入终点，本局失败；查看统计后可重新选择。"),
            new ZumaHelpTerm("POINTS", "得分", "本局总分，包含消除、金币和胜利收尾奖励。"),
            new ZumaHelpTerm("COMBOS", "连锁次数", "统计由断链回退、合拢后触发的有效连锁消除。"),
            new ZumaHelpTerm("COMBO xN", "连锁等级 N", "消除浮字中的连锁等级；等级越高，连锁额外奖励越高。"),
            new ZumaHelpTerm("COINS", "金币数", "本局成功击中的金币数量，每枚奖励 500 分。"),
            new ZumaHelpTerm("MAX CHAIN", "最长连续消除", "连续射击都形成消除时的最高 CHAIN 计数；射空或未消除会中断。"),
            new ZumaHelpTerm("MAX COMBO", "最高连锁等级", "本局通过自动回退连锁达到的最高 COMBO 等级。"),
            new ZumaHelpTerm("YOUR TIME", "本局用时", "有效游戏时间；暂停和后台时间不计入，胜利收尾阶段不再增加。"),
            new ZumaHelpTerm("ACE TIME", "参考时间", "默认参考成绩或该图该难度的已有最佳时间，不是超时失败倒计时。"),
            new ZumaHelpTerm("OK", "确定", "确认本局结果并回到选关，不会重复获得奖励。"),
            new ZumaHelpTerm("SAVE DATA", "存档提示", "说明存档加载遇到的问题；已有文件会被保留。"),
            new ZumaHelpTerm("RESET?", "是否重置", "请求确认重置损坏的 Zuma 数据；其他小游戏与公共设置不受影响。"),
            new ZumaHelpTerm("RESET", "重置", "先保留损坏存档的恢复副本，再创建新进度；保留副本失败则不重置。"),
            new ZumaHelpTerm("RETRY", "重试", "重新尝试读取存档，不会主动清空已有数据。"),
            new ZumaHelpTerm("CANCEL", "取消", "关闭当前确认提示，保留原来的对局或存档状态。")
        });
    }
}
