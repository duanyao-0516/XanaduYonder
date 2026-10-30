using System;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarHelpSection
    {
        public readonly string Title;
        public readonly string Body;
        public AgeOfWarHelpSection(string title, string body) { Title = title; Body = body; }
    }

    // Presentation copy only. Rules follow the fixed original ports and the current mobile host.
    internal static class AgeOfWarHelpContent
    {
        public static AgeOfWarHelpSection[] ForEdition(int edition)
        {
            if (edition == 1) return One();
            if (edition == 2) return Two();
            throw new ArgumentOutOfRangeException(nameof(edition));
        }

        private static AgeOfWarHelpSection[] One()
        {
            return new[] {
                new AgeOfWarHelpSection("目标与五个时代",
                    "守住己方基地，训练士兵推进战线，摧毁敌方基地即可获胜。士兵出场后自动前进和攻击，不需要逐个指挥。\n\n本代共有五个时代。经验达到进化门槛后，可以解锁下一时代的士兵和炮塔；进化不会扣除已有经验。"),
                new AgeOfWarHelpSection("金币、士兵与炮塔",
                    "训练士兵消耗金币和训练时间，最多可排队五名。击败敌军可获得金币和经验；己方士兵阵亡也会带来经验。\n\n先选择炮塔型号，再点击己方空炮位完成建造。新增炮位需要金币；出售已有炮塔可回收部分金币。注意给出兵和防守都留出资源。"),
                new AgeOfWarHelpSection("时代技能",
                    "右侧技能充能蓄满后即可使用，使用后重新充能。技能效果随时代变化：第三时代恢复己方士兵生命，其他时代发动战场攻击。技能本身不扣金币或经验。"),
                new AgeOfWarHelpSection("手机操作与暂停",
                    "点按顶部按钮训练、建塔或进化；在战场空白处左右拖动可查看双方基地和战线。\n\n点击顶部中央 PAUSE 打开暂停页。点暂停页空白处或恢复文字继续；return to Main Menu 返回本代主菜单，Exit 返回大厅。"),
                new AgeOfWarHelpSection("自动保存与继续",
                    "本代单独保留一局未完成战场。对战中会自动保存，暂停、退出或切到后台时也会保存。Continue 继续最近一次成功保存的进度，恢复后先停在暂停页，离线期间不会推进。\n\n胜利或失败后清除该局进度，重新开始会替换本代旧进度。出现存档错误提示时，请先处理提示再继续。"),
                new AgeOfWarHelpSection("菜单英文对照",
                    "Play — 开始新游戏\nContinue — 继续已保存的战场\nInstructions — 原版操作说明\nPAUSE / Paused — 暂停 / 已暂停\nPress Screen to resume — 点屏幕恢复\nreturn to Main Menu — 返回本代主菜单\nExit — 返回大厅\nDifficulty — 难度\nNormal — 普通\nHarder — 更难\nImpossible — 极难"),
                new AgeOfWarHelpSection("战斗英文对照",
                    "Gold — 金币，用于训练和建造\nXp / XP / Experience — 经验，用于达到进化门槛\nUnits — 士兵\nTurrets — 炮塔\nEvolve — 进化到下一时代\nSpecial — 时代技能\nHP / Health — 生命值\nDamage — 伤害\nRange — 射程\nCost — 费用\nTraining Time — 训练时间\nTrain — 训练\nBuild — 建造\nSell — 出售\nCancel — 取消\nMelee — 近战\nRanged — 远程")
            };
        }

        private static AgeOfWarHelpSection[] Two()
        {
            return new[] {
                new AgeOfWarHelpSection("目标与七个时代",
                    "守住己方基地，招募士兵并摧毁敌方基地。士兵会自动前进和攻击，可以搭配近战、远程与重型单位推进战线。\n\n本代共有七个时代。消耗经验进化后，会解锁新的单位和炮塔；还可用金币升级兵种能力。"),
                new AgeOfWarHelpSection("资源、人口与升级",
                    "Gold 是金币，Xp 是经验，Pop 是已占用人口。人口上限为 10，不同单位占用的人口不同；加入训练队列时就会预留人口。资源不足、人口已满或单位尚未解锁时，按钮不能完成购买。\n\n升级按钮可提升相应能力；部分单位有前置升级要求。费用与效果可查看按钮提示。"),
                new AgeOfWarHelpSection("炮塔与时代技能",
                    "先购买炮位，再选择炮塔型号，最后点击空炮位放置。取消尚未放置的炮塔会退还这次购买费用；已有炮塔可出售。\n\n技能开局充能 60 游戏秒，每次使用后也冷却 60 游戏秒。暂停时不计时，倍速会加快充能。第一时代落石还需 2000 经验；后续技能的效果和经验消耗请查看技能提示。\n\n敌方按独立时序发动技能，不会重置你的冷却。"),
                new AgeOfWarHelpSection("手机操作与暂停",
                    "点按顶部按钮训练、升级、建塔或进化；在战场空白处左右拖动可查看双方基地和战线。\n\n点击顶部中央 PAUSE 打开暂停页，Return 恢复战斗。Return to Main Menu 返回本代主菜单，Exit 返回大厅。战斗界面的倍速按钮可切换游戏速度；Show Damage 控制伤害数字显示。"),
                new AgeOfWarHelpSection("自动保存与继续",
                    "本代单独保留一局未完成战场。对战中会自动保存，暂停、退出或切到后台时也会保存。Continue 继续最近一次成功保存的进度，恢复后先停在暂停页，离线期间不会推进。\n\n胜利或失败后清除该局进度，重新开始会替换本代旧进度。出现存档错误提示时，请先处理提示再继续。"),
                new AgeOfWarHelpSection("菜单英文对照",
                    "GAME / Play — 开始新游戏\nContinue — 继续已保存的战场\nInstructions — 操作说明\nPAUSE / Paused — 暂停 / 已暂停\nReturn — 返回；在暂停页表示恢复战斗\nReturn to Main Menu — 返回本代主菜单\nExit — 返回大厅\nDifficulty — 难度\nEasy — 简单\nMedium — 普通\nHard — 困难"),
                new AgeOfWarHelpSection("战斗英文对照",
                    "Gold — 金币，用于招募、建造和升级\nXp / XP — 经验，用于进化和技能\nPop / Population — 人口占用\nUnits — 士兵\nTurrets — 炮塔\nUpgrades — 能力升级\nEvolve — 时代进化\nSpecial — 时代技能\nHP / Health — 生命值\nDamage — 伤害\nRange — 射程\nCost — 费用\nTraining Time — 训练时间\nCooldown — 冷却时间\nBuild — 建造\nSell — 出售\nCancel — 取消\nLocked — 尚未解锁\nQueue — 训练队列\nMelee — 近战\nRanged — 远程"),
                new AgeOfWarHelpSection("暂停页英文对照",
                    "Music — 音乐音量\nSound — 音效音量\nQuality — 画质\nLow / Medium / High — 低 / 中 / 高\nFrame Rate / FPS — 帧率\nShow Damage — 显示伤害数字\nApply — 应用设置\nGame Speed — 游戏速度")
            };
        }
    }
}
