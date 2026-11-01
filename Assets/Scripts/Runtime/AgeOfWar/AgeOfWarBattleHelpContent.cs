using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // A read-only snapshot for the app's existing help dialog. It never dispatches battle commands.
    internal sealed class AgeOfWarBattleHelpContent
    {
        internal sealed class Entry
        {
            internal string Group, Title, Body;
            internal Sprite Icon;
        }

        private static readonly string[] OneUnits = { "", "棍棒兵", "投石兵", "恐龙骑兵", "剑士", "弓箭手", "骑士", "决斗士", "火枪手", "炮兵", "近战步兵", "步枪兵", "坦克", "神之刃", "爆能兵", "战争机器", "超级战士" };
        private static readonly string[] TwoUnits = { "", "投石兵", "棍棒兵", "迅猛龙", "突击恐龙", "长矛勇士", "剑盾勇士", "突击勇士", "重甲勇士", "祭司", "弯刀战士", "死神战士", "战车勇士", "弓箭手", "步兵", "法师", "狮鹫骑士", "火枪手", "剑士", "火炮", "骑士", "机枪兵", "步枪兵", "掷弹兵", "坦克", "改造人", "蜘蛛刀手", "疯狂科学家", "装甲战衣", "神之怒" };
        private static readonly string[] OneTurrets = { "", "岩石弹弓", "自动蛋枪", "原始投石", "投石车", "火焰投石", "热油塔", "小型火炮", "大型火炮", "爆破火炮", "单管炮塔", "火箭炮塔", "双管炮塔", "钛金炮塔", "激光炮", "离子射线" };
        private static readonly string[] TwoTurrets = { "", "猛犸投石", "蛋弹枪", "岩石弹弓", "守卫塔", "金鹰塔", "巨鸟塔", "弩炮", "岩石投石", "金属投石", "小型火炮", "大型火炮", "机枪塔", "火箭发射", "双联火箭", "离子炮", "等离子炮", "重型等离子炮" };
        private static readonly string[] UpgradeNames = { "", "恐龙棚", "军械库", "马厩", "图书馆", "黑火药", "工厂", "能量线圈", "击杀收益", "支援伤害", "支援射程", "步兵伤害", "步兵护甲", "反甲伤害", "反甲护甲", "装甲伤害", "装甲护甲", "炮塔伤害", "炮塔射程" };
        private static readonly string[] UpgradeCopy = { "", "建造恐龙棚，解锁突击恐龙。", "建造军械库，解锁重甲斯巴达勇士。", "建造马厩，解锁战车勇士。", "建造图书馆，解锁法师。", "研究黑火药，解锁火炮。", "建造工厂，解锁坦克。", "建造能量线圈，解锁神之怒。", "提升击杀敌军获得的金币。", "提高支援兵种伤害。", "提高支援兵种射程。", "提高步兵伤害。", "提高步兵护甲。", "提高反装甲兵种伤害。", "提高反装甲兵种护甲。", "提高装甲兵种伤害。", "提高装甲兵种护甲。", "提高炮塔伤害。", "提高炮塔射程。" };
        private static readonly string[] OneAges = { "", "石器时代", "中世纪", "文艺复兴", "现代", "未来" };
        private static readonly string[] TwoAges = { "", "石器时代", "斯巴达", "古埃及", "中世纪", "文艺复兴", "现代", "未来" };
        private static readonly string[] OneSpecials = { "", "落石技能", "箭雨技能", "治疗技能", "空袭技能", "离子轰击" };
        private static readonly string[] OneSpecialNames = { "", "Rock fall", "Arrow rain", "Healing", "Air strike", "Ion strike" };
        private static readonly string[] TwoSpecials = { "", "陨石雨", "宙斯之怒", "沙漠之怒", "箭矢风暴", "暴怒国王", "特殊空投", "死亡射线" };
        private static readonly string[] OneSpecialCopy = { "", "向战场投下落石。", "向战场降下箭雨。", "持续治疗我方单位。", "飞机沿战场投下炸弹。", "向战场发射离子攻击。" };
        private static readonly string[] OneSpecialEnglish = { "", "Drop rocks onto the battlefield.", "Rain arrows onto the battlefield.", "Heal friendly units over time.", "Bomb the battlefield from the air.", "Strike the battlefield with ion attacks." };
        private static readonly string[] TwoSpecialCopy = { "", "向敌军降下陨石雨。", "宙斯以雷暴打击敌军。", "用强力飞弹打击敌军。", "向战场降下箭矢风暴。", "向战场投射爆炸岩石。", "用炸弹轰炸战场。", "用强力激光消灭敌方单位。" };

        internal string Title { get; private set; }
        internal string Summary { get; private set; }
        internal readonly List<Entry> Entries = new List<Entry>();
        private readonly AgeOfWarAssets assets;
        private readonly IAgeOfWarBattle battle;
        private readonly AgeOfWarFrame frame;
        private readonly AgeOfWarOneBattle one;
        private readonly AgeOfWarTwoBattle two;

        private AgeOfWarBattleHelpContent(AgeOfWarAssets assets, IAgeOfWarBattle battle, AgeOfWarFrame frame)
        {
            this.assets = assets;
            this.battle = battle;
            this.frame = frame;
            one = battle as AgeOfWarOneBattle;
            two = battle as AgeOfWarTwoBattle;
        }

        internal static AgeOfWarBattleHelpContent Capture(AgeOfWarAssets assets, IAgeOfWarBattle battle, AgeOfWarFrame frame)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (assets.Edition != battle.Edition || assets.Edition != frame.Edition)
                throw new ArgumentException("Battle help requires the current edition's assets and snapshot.");
            var result = new AgeOfWarBattleHelpContent(assets, battle, frame);
            result.Build();
            return result;
        }

        private void Build()
        {
            Title = Name(one != null ? OneAges : TwoAges, frame.Age) + " · 图标说明";
            Summary = "当前时代的原版图标与功能 / Current age icon guide\n金币 / Gold " + Number(frame.Gold)
                + "   经验 / XP " + Number(frame.Experience) + "   训练队列 / Queue " + frame.TrainingQueue.Length;
            if (two != null)
            {
                var hud = two.CaptureHudText();
                Summary += "\n人口 / Population " + Value(hud, "popTXT");
            }
            if (frame.PendingTurretId != 0)
                Summary += "\n待放置 / Awaiting placement: " + TurretName(frame.PendingTurretId);
            BuildUnits();
            BuildTurrets();
            BuildUtilities();
            BuildEvolutionAndSpecial();
            BuildUpgrades();
        }

        private void BuildUnits()
        {
            for (int index = 0; index < frame.Units.Length; index++)
            {
                var offer = frame.Units[index];
                var icon = one != null
                    ? SourceIcon("one-units/" + frame.Age, "bf" + (index + 1), offer.Id == 16 ? 141 : 0)
                    : SourceIcon("buttons/1", "btnU" + offer.Id);
                // Match the source depth rule: future-age ACS covers the retained Tank button.
                if (two != null && ObscuredUnit(offer.Id, icon)) continue;
                var fields = Tooltip(AgeOfWarAction.Train, offer.Id);
                string body = "训练后加入队列 / Train and add to the queue.\n" + GoldCost(offer.Cost);
                if (one != null) body += "\n" + OneStats(offer.Description);
                else
                {
                    body += "\n基础数值 / Base stats: " + UnitType(Value(fields, "typeTXT"))
                        + " · 人口 / Pop " + Value(fields, "popTXT") + " · 生命 / HP " + Value(fields, "hpTXT")
                        + " · 伤害 / Damage " + Value(fields, "damageTXT") + " · 训练 / Train " + Value(fields, "buildTXT")
                        + " · 射程 / Range " + Value(fields, "rangeTXT") + " · 移速 / Speed " + Value(fields, "walkTXT")
                        + " · 攻速 / Attack " + Value(fields, "hitSpeedTXT");
                    body += "\n" + Value(fields, "descTXT");
                }
                body += "\n状态 / Status: " + UnitStatus(offer, fields);
                Add("兵种 / Units", UnitName(offer.Id), offer.Name, icon, body);
            }
        }

        private void BuildTurrets()
        {
            int index = 0;
            foreach (var offer in frame.Turrets)
            {
                var fields = Tooltip(AgeOfWarAction.BuyTurret, offer.Id);
                string body = one != null
                    ? "选择炮塔后点击空炮位，放置时扣除金币。\nSelect a turret and an empty slot; Gold is spent on placement."
                    : "购买后选择空炮位安装；取消会退还金币。\nBuy, then select an empty slot; cancel to refund the purchase.";
                body += "\n" + GoldCost(offer.Cost);
                body += one != null ? "\n" + OneStats(offer.Description)
                    : "\n基础数值 / Base stats: 伤害 / Damage " + Value(fields, "damageTXT") + " · 射程 / Range " + Value(fields, "rangeTXT") + "\n" + Value(fields, "descTXT");
                body += "\n状态 / Status: " + (frame.Gold < offer.Cost ? "金币不足 / Not enough Gold" : !HasEmptySlot() ? "没有空炮位 / No free turret slot" : "可购买 / Available");
                Add("炮塔 / Turrets", TurretName(offer.Id), offer.Name,
                    one != null ? SourceIcon("one-turrets/" + frame.Age, "bt" + (++index)) : SourceIcon("buttons/1", "btnT" + offer.Id), body);
            }
        }

        private void BuildUtilities()
        {
            const string group = "炮位与操作 / Slots and controls";
            string slotBody = "增加一个空炮位，用于安装炮塔。最多四个炮位。\nAdd an empty turret slot; maximum four slots.\n";
            slotBody += frame.NextTurretSlotCost > 0 ? GoldCost(frame.NextTurretSlotCost) + "\n状态 / Status: "
                + (frame.Gold < frame.NextTurretSlotCost ? "金币不足 / Not enough Gold" : "可购买 / Available") : "炮位已满 / Maximum turret slots";
            Add(group, "增加炮位", "Add turret spot", UtilityIcon("btnAddTower", 186), slotBody);

            var resale = new StringBuilder("进入出售模式，再选择我方已建炮塔。\nEnter sell mode, then select a built friendly turret.");
            bool occupied = false;
            foreach (var slot in frame.TurretSlots)
            {
                if (slot.Enemy || slot.TurretId == 0) continue;
                occupied = true;
                var fields = Tooltip(AgeOfWarAction.SellTurret, one != null ? slot.Slot : slot.TurretId);
                string refund = Value(fields, "onT.costTXT");
                string english = "";
                if (one != null)
                {
                    string original = Value(fields, "desc");
                    int split = original.LastIndexOf(" for ", StringComparison.Ordinal);
                    if (split >= 5) { refund = original.Substring(split + 5); english = original.Substring(5, split - 5); }
                }
                else english = Value(Tooltip(AgeOfWarAction.BuyTurret, slot.TurretId), "nameTXT");
                resale.Append("\n炮位 / Slot ").Append(slot.Slot).Append(": ").Append(TurretName(slot.TurretId))
                    .Append(" · ").Append(english).Append(" — 回收 / Refund ").Append(refund);
            }
            if (!occupied) resale.Append("\n没有可出售的炮塔 / No turret to sell");
            Add(group, "出售炮塔", "Sell turret", UtilityIcon("btnSellTurret", 184), resale.ToString());
            if (one != null)
            {
                Add(group, "训练菜单", "Train units menu", SourceIcon("one-main/1", "m1", 179), "打开当前时代的兵种菜单。\nOpen the current age's unit menu.");
                Add(group, "炮塔菜单", "Build turrets menu", SourceIcon("one-main/1", "m2", 181), "打开当前时代的炮塔菜单。\nOpen the current age's turret menu.");
                Add(group, "返回", "Return", SourceIcon("one-units/" + frame.Age, "bfr"), "返回上一级菜单；选定炮塔后可取消放置。\nReturn to the previous menu; cancel turret placement when selecting a slot.");
            }
            else
            {
                Add(group, "取消", "Cancel", SourceIcon("buttons/1", "btnCancel"), "退出出售或建造模式；取消待建炮塔时退还购买金币。\nLeave sell or build mode; canceling a pending turret refunds its purchase cost.");
                Add(group, "选项", "Options", SourceIcon("battle-ui", "btnPause"), "暂停战斗并打开原版选项菜单，继续后返回战斗。\nPause the battle and open its options; resume to return to combat.");
                Add(group, "游戏速度", "Game speed", SourceIcon("battle-ui", "btnGameSpeed"),
                    "切换 1× / 2× 战斗速度。当前 / Current: " + frame.Speed + "×\nToggle between normal (1×) and double (2×) battle speed.", frame.Speed - 1);
            }
        }

        private void BuildEvolutionAndSpecial()
        {
            string evolution;
            if (frame.NextEvolutionExperience <= 0) evolution = "已达到最终时代 / Final age reached.";
            else
            {
                string cost = Number(frame.NextEvolutionExperience);
                evolution = one != null ? "达到 " + cost + " XP 后进入下一时代，不消耗经验。\nReach " + cost + " XP to evolve; XP is not spent."
                    : "消耗 " + cost + " XP，进入下一时代。\nSpend " + cost + " XP to evolve to the next age.";
                evolution += "\n状态 / Status: " + (frame.Experience < frame.NextEvolutionExperience ? "经验不足 / Not enough XP" : "可进化 / Available");
            }
            Add("进化与技能 / Evolution and special", "时代进化", "Evolve", UtilityIcon("btnEvolve", 188), evolution);

            var fields = Tooltip(AgeOfWarAction.Special, frame.Age);
            string special = Name(one != null ? OneSpecialCopy : TwoSpecialCopy, frame.Age);
            special += one != null ? " 不消耗金币或经验。\n" + Name(OneSpecialEnglish, frame.Age) + " No Gold or XP cost."
                : " 消耗 " + Number(frame.SpecialExperienceCost) + " XP。\n" + Value(fields, "descTXT") + "\n费用 / Cost: " + Number(frame.SpecialExperienceCost) + " XP";
            double fullCooldown = one != null ? 2000.0 / battle.TickRate : 60;
            int remaining = (int)Math.Ceiling(Math.Max(0, 1 - frame.SpecialProgress) * fullCooldown);
            special += "\n充能周期 / Recharge: " + fullCooldown.ToString("0.##", CultureInfo.InvariantCulture) + "s";
            special += "\n状态 / Status: " + (!frame.SpecialReady ? "冷却中 / Cooldown " + remaining + "s"
                : frame.Experience < frame.SpecialExperienceCost ? "经验不足 / Not enough XP" : "已就绪 / Ready");
            var icon = one != null ? SourceIcon("one-special/" + frame.Age, "") : SourceIcon("buttons/1", "btnS" + frame.Age);
            Add("进化与技能 / Evolution and special", Name(one != null ? OneSpecials : TwoSpecials, frame.Age), one != null ? Name(OneSpecialNames, frame.Age) : Value(fields, "nameTXT"), icon, special);
        }

        private void BuildUpgrades()
        {
            foreach (var offer in frame.Upgrades)
            {
                var fields = Tooltip(AgeOfWarAction.Upgrade, offer.Id);
                bool maximum = offer.Level >= offer.MaximumLevel;
                string body = Name(UpgradeCopy, offer.Id) + "\n等级 / Level: " + offer.Level + " / " + offer.MaximumLevel;
                body += "\n" + (maximum ? "已满级 / Maximum level" : GoldCost(offer.Cost));
                body += "\n" + Value(fields, "descTXT");
                if (!maximum) body += "\n状态 / Status: " + (frame.Gold < offer.Cost ? "金币不足 / Not enough Gold" : "可升级 / Available");
                Add("升级 / Upgrades", Name(UpgradeNames, offer.Id), offer.Name, SourceIcon("buttons/1", "btnUP" + offer.Id), body);
            }
        }

        private string UnitStatus(AgeOfWarOffer offer, Dictionary<string, string> fields)
        {
            var reasons = new List<string>();
            if (offer.LockedAppearance) reasons.Add("未解锁 / Locked: " + RequirementName(offer.Requirement) + " · " + offer.Requirement);
            if (frame.Gold < offer.Cost) reasons.Add("金币不足 / Not enough Gold");
            if (one != null && frame.TrainingQueue.Length >= 5) reasons.Add("训练队列已满 / Training queue full");
            if (two != null)
            {
                int population;
                if (int.TryParse(Value(fields, "popTXT"), out population) && frame.Population + population > frame.MaximumPopulation)
                    reasons.Add("人口不足 / Not enough population room");
                int waiting = 0;
                foreach (int id in two.WaitingUnitIds) if (id == offer.Id) waiting++;
                if (waiting >= 5) reasons.Add("该兵种等待队列已满 / This unit's waiting queue is full");
            }
            return reasons.Count == 0 ? "可训练 / Available" : string.Join(" · ", reasons.ToArray());
        }

        private bool HasEmptySlot()
        {
            foreach (var slot in frame.TurretSlots) if (!slot.Enemy && slot.Purchased && slot.TurretId == 0) return true;
            return false;
        }

        private bool ObscuredUnit(int id, AgeOfWarSceneLayer icon)
        {
            foreach (var offer in frame.Units)
            {
                if (offer.Id == id) continue;
                var other = SourceIcon("buttons/1", "btnU" + offer.Id);
                if (other.depth > icon.depth && other.x == icon.x && other.y == icon.y) return true;
            }
            return false;
        }

        private void Add(string group, string chinese, string english, AgeOfWarSceneLayer source, string body, int iconFrame = -1)
        {
            AgeOfWarSpriteFrame icon;
            if (!assets.TryGetFrame(source.key, source.action, iconFrame >= 0 ? iconFrame : source.frame, out icon) || icon.Empty)
                throw new InvalidOperationException("Missing original battle help icon: " + source.key);
            Entries.Add(new Entry { Group = group, Title = chinese + " · " + english, Body = body, Icon = icon.Sprite });
        }

        private AgeOfWarSceneLayer UtilityIcon(string twoName, int oneId)
        { return one != null ? SourceIcon("one-main/1", "", oneId) : SourceIcon("buttons/1", twoName); }

        private AgeOfWarSceneLayer SourceIcon(string sceneKey, string name, int originalId = 0)
        {
            var scene = assets.GetScene(sceneKey);
            if (scene != null) foreach (var layer in scene.layers)
                if (originalId != 0 ? layer.key.EndsWith("/" + originalId, StringComparison.Ordinal) : layer.name == name) return layer;
            throw new InvalidOperationException("Missing original battle help placement: " + sceneKey + " / " + name + " / " + originalId);
        }

        private Dictionary<string, string> Tooltip(AgeOfWarAction action, int id)
        { return one != null ? one.CaptureTooltip(action, id) : two.CaptureTooltip(action, id); }
        private string UnitName(int id) { return Name(one != null ? OneUnits : TwoUnits, id); }
        private string TurretName(int id) { return Name(one != null ? OneTurrets : TwoTurrets, id); }
        private static string Name(string[] names, int id) { return id > 0 && id < names.Length ? names[id] : ""; }
        private static string Number(double value) { return Math.Floor(value + .5).ToString("N0", CultureInfo.InvariantCulture); }
        private static string GoldCost(int cost) { return "费用 / Cost: " + Number(cost) + " 金币 / Gold"; }
        private static string Value(Dictionary<string, string> fields, string key)
        { string value; return fields.TryGetValue(key, out value) ? value : ""; }
        private static string OneStats(string value)
        { return "基础数值 / Base stats: " + value.Replace("HP", "生命").Replace("melee", "近战伤害").Replace("ranged", "远程伤害").Replace("frames", "训练帧").Replace("damage", "伤害").Replace("range", "射程") + "\n" + value; }
        private static string UnitType(string value)
        {
            switch (value)
            {
                case "Support": return "支援 / Support";
                case "Infantry": return "步兵 / Infantry";
                case "Anti-Armor": return "反装甲 / Anti-Armor";
                case "Armored": return "装甲 / Armored";
                case "Special": return "特殊 / Special";
                default: return value;
            }
        }
        private static string RequirementName(string value)
        {
            switch (value)
            {
                case "Dino Hut": return "恐龙棚";
                case "Armory": return "军械库";
                case "Stables": return "马厩";
                case "Library": return "图书馆";
                case "Black Powder": return "黑火药";
                case "Factories": return "工厂";
                case "Power Coil": return "能量线圈";
                default: return value;
            }
        }
    }
}
