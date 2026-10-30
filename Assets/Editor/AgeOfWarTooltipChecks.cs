using System;
using System.Reflection;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.Two;
using XanaduYonder.Runtime.AgeOfWar;
using Object=UnityEngine.Object;
namespace XanaduYonder.EditorTools
{
    internal static class AgeOfWarTooltipChecks
    {
        internal static int Run()
        {
            int checks=0;
            using(var assets=AgeOfWarAssets.Load(2))
            {
                var owner=new GameObject("Original seven windows check",typeof(RectTransform),typeof(Canvas));AgeOfWarTwoHud hud=null;
                try
                {
                    hud=new AgeOfWarTwoHud(owner.transform,assets);var root=owner.transform.Find("Original seven tooltip windows");
                    Require(root!=null&&root.childCount==7,"Hud creates all seven original windows before any hover",ref checks);
                    var battle=new AgeOfWarTwoBattle(1,123);var unit=root.Find("unitInfo");var upgrade=root.Find("upgradeInfo");
                    hud.ShowTooltip(battle,AgeOfWarAction.Train,1,new Vector2(120,35));Advance(hud,.017,1);hud.Render(battle,battle.Capture());
                    Require(Math.Abs(unit.GetComponent<CanvasGroup>().alpha-17/256f)<.00001,"Tooltip opacity preserves the original alpha setter",ref checks);
                    hud.ShowTooltip(battle,AgeOfWarAction.Upgrade,1,new Vector2(250,35));Advance(hud,.01,1);hud.Render(battle,battle.Capture());
                    Require(unit.gameObject.activeSelf&&upgrade.gameObject.activeSelf,"Old unit window and new upgrade window cross-fade together",ref checks);
                    Require(((RectTransform)unit).anchoredPosition==((RectTransform)upgrade).anchoredPosition,"Fading-out windows continue to follow the current pointer",ref checks);
                    float alpha=upgrade.GetComponent<CanvasGroup>().alpha;hud.Render(battle,battle.Capture());hud.Render(battle,battle.Capture());
                    Require(upgrade.GetComponent<CanvasGroup>().alpha==alpha,"Repeated Render calls cannot advance opacity",ref checks);
                    battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Pause));Advance(hud,.01,1);hud.Render(battle,battle.Capture());
                    Require(upgrade.GetComponent<CanvasGroup>().alpha>alpha,"Source HUD window fades continue during battle pause",ref checks);
                    return checks;
                }
                finally {if(hud!=null)hud.Dispose();Object.DestroyImmediate(owner);}
            }
        }
        private static void Advance(AgeOfWarTwoHud hud,double seconds,int speed)
        {var method=typeof(AgeOfWarTwoHud).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic);if(method==null)throw new InvalidOperationException("Missing source-stage HUD advance");method.Invoke(hud,new object[]{seconds,speed});}
        private static void Require(bool value,string message,ref int count){count++;if(!value)throw new InvalidOperationException(message);}
    }
}
