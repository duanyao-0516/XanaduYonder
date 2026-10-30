using System.Collections.Generic;
namespace XanaduYonder.Core.AgeOfWar.One
{
    public sealed partial class AgeOfWarOneBattle
    {
        /// <summary>Original menu dynamic text. SellTurret id means its one-based slot.</summary>
        public Dictionary<string,string> CaptureTooltip(AgeOfWarAction action,int id)
        {
            string description="";
            switch(action)
            {
                case AgeOfWarAction.Train:
                    if(id==0)description="Train units menu";
                    else if(id>=1&&id<=16)description=OneData.Units[id].Cost+"$ - "+OneData.Units[id].Name;
                    break;
                case AgeOfWarAction.BuyTurret:
                    if(id==0)description="Build turrets menu";
                    else if(id>=1&&id<=15)description=OneData.Turrets[id].Cost+"$ - "+OneData.Turrets[id].Name;
                    break;
                case AgeOfWarAction.SellTurret:
                    if(id==0)description="Sell a turret";
                    else {var turret=FindTurret(1,id);if(turret!=null){var d=OneData.Turrets[turret.Id];description="Sell "+d.Name+" for "+Round(d.Cost/2.0)+"$";}}
                    break;
                case AgeOfWarAction.BuyTurretSlot:description=_addons<3?OneData.SlotCosts[_addons]+"$ - Add a turret spot":"Can't build any more";break;
                case AgeOfWarAction.Evolve:description=_age<5?OneData.Evolution[_age-1]+" Xp - Evolve to next age":"You cannot evolve anymore";break;
                case AgeOfWarAction.Cancel:description="Return to previous menu";break;
            }
            return new Dictionary<string,string>{{"desc",description},{"bdesc",_queue.Count>0?"Training "+OneData.Units[_queue[0]].Name+"...":""}};
        }
    }
}
