using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.FlappyBird
{
    internal sealed partial class FlappyBirdPage
    {
        private enum DialogKind { None,Pause,Result,Help,Exit }
        private GameObject dialog;
        private RectTransform dialogPanel;
        private DialogKind dialogKind;
        private Button resultRestart;
        private GameObject resultRetry;

        private RectTransform BeginDialog(DialogKind kind,float height)
        {
            pointer?.Cancel(); CloseDialog();
            var overlay=FlappyScene.Shape(layout,"Flappy dialog overlay",0,0,388,layout.rect.height,0,new Color(.09f,.239f,.208f,.28f));
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget=true; dialog=overlay.gameObject; dialogKind=kind;
            var panel=FlappyScene.Shape(overlay.transform,"Flappy dialog "+kind,0,0,300,height,19,UiFactory.Paper);
            panel.raycastTarget=true; dialogPanel=panel.rectTransform; UiFactory.Center(dialogPanel); FitDialog();
            return dialogPanel;
        }
        private void FitDialog()
        {
            if(dialogPanel==null) return;
            float s=Mathf.Min(1,(layout.rect.height-40)/dialogPanel.rect.height,(layout.rect.width-40)/300);
            dialogPanel.localScale=Vector3.one*Mathf.Max(.05f,s);
        }
        private void CloseDialog()
        {
            if(dialog!=null) { dialog.SetActive(false); Destroy(dialog); }
            dialog=null; dialogPanel=null; dialogKind=DialogKind.None; resultRestart=null; resultRetry=null;
        }
        private Button DialogButton(RectTransform panel,string title,float y,Action action,bool primary=true,bool quiet=false)
        {
            var button=ActionButton(panel,title,24,y,252,44,action,10,quiet,primary);
            Label(button.transform,title,4,0,244,44,quiet?11:13,quiet?UiFactory.Muted:primary?UiFactory.Paper:UiFactory.Ink,TextAnchor.MiddleCenter);
            return button;
        }
        private void DialogHeading(RectTransform panel,string title,float y)
        { Label(panel,title,24,y,252,38,24,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold); }
        private void ShowPause()
        {
            var panel=BeginDialog(DialogKind.Pause,308);
            Label(panel,"TAKE A LITTLE BREAK",24,27,252,15,9,UiFactory.Muted,TextAnchor.MiddleCenter);
            DialogHeading(panel,"歇一小会儿",51);
            Label(panel,"天空在这里，等你继续。",24,98,252,28,12,UiFactory.Muted,TextAnchor.MiddleCenter);
            DialogButton(panel,session.Phase==XanaduYonder.Core.FlappyBird.FlappyPhase.Ready?"返回准备":"继续飞行",139,Continue);
            DialogButton(panel,"重新开始",193,Restart,false);
            DialogButton(panel,"返回大厅",242,RequestHome,false,true);
        }
        private void ShowResult()
        {
            bool unsaved=!string.IsNullOrEmpty(SaveError);
            var panel=BeginDialog(DialogKind.Result,unsaved?392:366);
            FlappyScene.SpriteImage(panel,"Result bird",scene.BirdSprite,127,28,46,42);
            DialogHeading(panel,"飞得不错",82);
            Label(panel,"下一次，也许再远一点。",24,125,252,28,12,UiFactory.Muted,TextAnchor.MiddleCenter);
            Label(panel,"本局分数",24,170,126,20,10,UiFactory.Muted,TextAnchor.MiddleCenter);
            Label(panel,"最高纪录",150,170,126,20,10,UiFactory.Muted,TextAnchor.MiddleCenter);
            ResultNumber(panel,session.Game.Score.ToString(),24,UiFactory.Ink);
            ResultNumber(panel,data.BestScore.ToString(),150,UiFactory.Hex("8C9A8B"));
            resultRestart=DialogButton(panel,"再飞一次",unsaved?278:252,Restart);
            resultRestart.interactable=session.CanRestart;
            DialogButton(panel,"返回大厅",unsaved?327:301,RequestHome,false,true);
            if(unsaved)
            {
                var retry=ActionButton(panel,"Result retry save",60,249,180,24,RetrySave,5,true);
                resultRetry=retry.gameObject;
                Label(retry.transform,"最高纪录未保存 · 重试",0,0,180,24,10,UiFactory.Hex("906B27"),TextAnchor.MiddleCenter);
            }
        }
        private void ResultNumber(RectTransform parent,string value,float x,Color color)
        {
            var number=Label(parent,value,x,196,126,45,34,color,TextAnchor.MiddleCenter,FontStyle.Bold);
            number.horizontalOverflow=HorizontalWrapMode.Overflow;
            if(number.preferredWidth>126) number.fontSize=Mathf.Max(1,Mathf.FloorToInt(34*126/number.preferredWidth));
        }
        private void ShowHelpDialog()
        {
            var panel=BeginDialog(DialogKind.Help,386);
            DialogHeading(panel,"怎么飞？",26);
            var close=ActionButton(panel,"Close help",256,8,36,36,()=>{ CloseDialog(); Refresh(); },0,true);
            var closeIcon=UiFactory.Rect(close.transform,"Close symbol",9,9,18,18).gameObject.AddComponent<UiIconGraphic>(); closeIcon.Kind=UiIconKind.Close; closeIcon.color=UiFactory.Muted; closeIcon.raycastTarget=false;
            Rule(panel,"1","轻点场地，小鸟就向上拍翼一次。松手后自然下落。",82,48);
            Rule(panel,"2","穿过管道的空隙，每通过一组得 1 分。碰到管道或地面，本局结束。",141,48);
            Rule(panel,"3","随时暂停。回到前台后由你决定何时继续。",201,48);
            UiFactory.Panel(panel,"Credit divider",24,260,252,1,UiFactory.Hex("DDE3D7"),false);
            Label(panel,"素材：By Peleila · Flappy bird\n使用作者列出的 CC0 许可选项。",24,272,252,42,10,UiFactory.Muted,TextAnchor.UpperLeft);
            DialogButton(panel,"知道了",320,()=>{ CloseDialog(); Refresh(); });
        }
        private void Rule(RectTransform parent,string number,string copy,float y,float height)
        {
            var badge=FlappyScene.Shape(parent,"Rule "+number,24,y,22,22,7,UiFactory.Hex("E7EDE4"));
            Label(badge.transform,number,0,0,22,22,10,UiFactory.Accent,TextAnchor.MiddleCenter);
            var text=Label(parent,copy,56,y-1,220,height,12,UiFactory.Hex("64786B"),TextAnchor.UpperLeft); text.lineSpacing=1.2f;
        }
        private void ShowExit()
        {
            bool unsaved=HasUnsavedRecord;
            var panel=BeginDialog(DialogKind.Exit,unsaved?290:256);
            DialogHeading(panel,"结束这一局？",26);
            string copy=unsaved?"新纪录 "+data.BestScore+" 尚未保存。\n退出应用前若未保存成功，这项纪录会丢失。\n仍要结束本局并返回大厅吗？":"本局将结束，已保存的纪录会保留。";
            Label(panel,copy,24,79,252,unsaved?88:54,12,UiFactory.Muted,TextAnchor.MiddleCenter);
            float y=unsaved?178:144;
            DialogButton(panel,"结束并返回大厅",y,()=>{ Save(); returnLobby?.Invoke(); });
            DialogButton(panel,"留在这里",y+54,()=>{ CloseDialog(); Refresh(); },false);
        }
    }
}
