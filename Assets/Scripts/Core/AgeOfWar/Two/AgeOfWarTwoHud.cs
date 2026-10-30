using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
namespace XanaduYonder.Core.AgeOfWar.Two
{
    public sealed partial class AgeOfWarTwoBattle
    {
        /// <summary>Original Windows/*Info.updateFields strings. IDs are one based; zero means the current contextual offer.</summary>
        public Dictionary<string,string> CaptureTooltip(AgeOfWarAction action,int id=0)
        {
            var fields=new Dictionary<string,string>();
            XElement d;
            switch(action)
            {
                case AgeOfWarAction.Train:
                    if(id<1||id>29) return fields;
                    d=Definition("units",id);
                    fields["nameTXT"]=S(d,"name"); fields["costTXT"]=S(d,"cost")+" Gold";
                    fields["popTXT"]=S(d,"pop"); fields["hpTXT"]=S(d,"health"); fields["walkTXT"]=S(d,"speed")+"m/s";
                    fields["typeTXT"]=S(d,"unitType"); fields["damageTXT"]=S(d,"damageMin")+" - "+S(d,"damageMax");
                    fields["hitSpeedTXT"]=S(d,"attackRate"); fields["buildTXT"]=S(d,"creationtime")+"s";
                    fields["rangeTXT"]=S(d,"range")=="0"?"none":S(d,"range");
                    fields["descTXT"]=RequirementLocked(id)?"You need "+S(d,"needUpgrade")+" to unlock this unit":S(d,"desc");
                    break;
                case AgeOfWarAction.BuyTurret:
                    if(id<1||id>17) return fields;
                    d=Definition("turrets",id); BasicTooltip(fields,d," Gold");
                    fields["damageTXT"]=S(d,"damageMin")+" - "+S(d,"damageMax"); fields["rangeTXT"]=S(d,"range")=="0"?"none":S(d,"range");
                    break;
                case AgeOfWarAction.Upgrade:
                    if(id<1||id>18) { fields["nameTXT"]="NO DATA"; fields["costTXT"]=fields["levelTXT"]=fields["descTXT"]=""; return fields; }
                    var levels=Definition("upgrades",id).Elements().ToArray(); int level=Upgrades[0,id-1];
                    BasicTooltip(fields,levels[level]," Gold"); if(level==levels.Length-1) fields["costTXT"]="";
                    fields["levelTXT"]=level.ToString(CultureInfo.InvariantCulture)+" / "+(levels.Length-1).ToString(CultureInfo.InvariantCulture);
                    break;
                case AgeOfWarAction.Special:
                    if(id==0) id=Age; if(id<1||id>7) return fields;
                    BasicTooltip(fields,Definition("specials",id)," Xp"); break;
                case AgeOfWarAction.Evolve:
                    if(id==0) id=Age; if(id<1||id>7) { fields["nameTXT"]="NO DATA"; fields["costTXT"]=fields["descTXT"]=""; return fields; }
                    BasicTooltip(fields,Definition("evolutions",id)," Xp"); break;
                case AgeOfWarAction.BuyTurretSlot:
                    if(id==0) id=Spots+1; if(id<1||id>4) return fields;
                    BasicTooltip(fields,Definition("towers",id)," Gold"); break;
                case AgeOfWarAction.SellTurret:
                    if(id>=1&&id<=17) fields["onT.costTXT"]=(I(Definition("turrets",id),"cost")/2).ToString(CultureInfo.InvariantCulture)+"$";
                    break;
            }
            return fields;
        }
        static void BasicTooltip(Dictionary<string,string> fields,XElement d,string suffix)
        {
            fields["nameTXT"]=S(d,"name"); fields["costTXT"]=S(d,"cost")+suffix; fields["descTXT"]=S(d,"desc");
        }
        /// <summary>Original HUD field names, including the source's spacing and Xp capitalization.</summary>
        public Dictionary<string,string> CaptureHudText()
        {
            return new Dictionary<string,string> {
                {"moneyTXT",Gold.ToString(CultureInfo.InvariantCulture)+" Gold"},
                {"xpTXT",Experience.ToString(CultureInfo.InvariantCulture)+" Xp"},
                {"popTXT",Population.ToString(CultureInfo.InvariantCulture)+" / 10 Pop"},
                {"base1Hp",PlayerHealth.ToString(CultureInfo.InvariantCulture)+"/"+PlayerMaximumHealth.ToString(CultureInfo.InvariantCulture)},
                {"base2Hp",EnemyHealth.ToString(CultureInfo.InvariantCulture)+"/"+EnemyMaximumHealth.ToString(CultureInfo.InvariantCulture)}
            };
        }
        public int BuildingUnitId { get { return Building; } }
        public int[] WaitingUnitIds { get { return Queue.ToArray(); } }
        public double TrainingRemainingFraction { get { return Building==0?0:BuildTimer/BuildMaximum; } }
        /// <summary>One based source MovieClip frame, clamped like Flash gotoAndStop.</summary>
        public int SpecialDelayFrame { get { return System.Math.Max(1,System.Math.Min(300,300-Round(SpecialTimer/60*300))); } }
    }
}
