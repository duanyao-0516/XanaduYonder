using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.AgeOfWar;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private AgeOfWarPage ageOfWarPage;
        private bool IsAgeOfWarPage {get{return activePage==Page.AgeOfWarOne||activePage==Page.AgeOfWarTwo;}}
        private GameObject ageOfWarSelection;
        private static void AddAgeOfWarPreview(Transform parent)
        {
            // Lobby-owned artwork stays independent of the battle assets' unload lifecycle.
            var badge=UiFactory.Panel(parent,"Age of War badge",0,0,90,90,UiFactory.Paper);
            badge.raycastTarget=false;
            var swords=UiFactory.Rect(badge.transform,"Age of War crossed swords",17,7,56,56).gameObject.AddComponent<Image>();
            swords.sprite=Resources.Load<Sprite>("Lobby/Icons/age-of-war");
            swords.color=Color.white;
            swords.preserveAspect=true;
            swords.raycastTarget=false;
            var titleBand=UiFactory.Panel(badge.transform,"Age of War title band",4,66,82,20,UiFactory.Ink);
            titleBand.raycastTarget=false;
            var logo=UiFactory.Rect(titleBand.transform,"Age of War original logo",4,5,74,10).gameObject.AddComponent<Image>();
            logo.sprite=Resources.Load<Sprite>("Lobby/AgeOfWarLogo");
            logo.color=Color.white;
            logo.preserveAspect=true;
            logo.raycastTarget=false;
        }
        private void ShowAgeOfWarSelection()
        {
            if(activePage!=Page.Lobby||paused||!focused)return;
            StopAnimation();CloseModal();Save();
            if(ageOfWarSelection==null)
            {
                var root=UiFactory.Rect(frame,"Age of War selection",0,0,720,1320);UiFactory.Stretch(root);ageOfWarSelection=root.gameObject;
                var nav=UiFactory.Band(root,"Selection navigation",0,92,0);
                UiFactory.IconButton(nav,UiIconKind.Home,56,34,58,58,ShowLobby);
                UiFactory.Label(nav,"XANADU / YONDER",210,34,300,58,19,UiFactory.Muted,TextAnchor.MiddleCenter);
                UiFactory.IconButton(nav,UiIconKind.Book,536,34,58,58,ShowAgeOfWarHelp);
                UiFactory.IconButton(nav,UiIconKind.Settings,606,34,58,58,OpenSettings);
                var heading=UiFactory.Band(root,"Selection heading",112,130,.05f);
                UiFactory.Label(heading,"战争进化史",36,0,648,80,52,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
                UiFactory.Label(heading,"选择一段文明，开始你的战争。",36,82,648,46,24,UiFactory.Muted);
                for(int edition=1;edition<=2;edition++)
                {
                    int selected=edition;
                    var band=UiFactory.Band(root,"Edition "+edition,294+(edition-1)*302,266,.2f+(edition-1)*.28f);
                    var card=UiFactory.Panel(band,"Edition card "+edition,36,0,648,266,UiFactory.Hex("E7EDE4"));
                    UiFactory.Label(card.transform,edition==1?"战争进化史 · 一代":"战争进化史 · 二代",24,18,600,60,32,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
                    UiFactory.Label(card.transform,edition==1?"五个时代 · 经典攻防":"七个时代 · 兵种升级",24,88,600,42,24,UiFactory.Accent);
                    UiFactory.Label(card.transform,"招募士兵、建造炮塔、进化文明。",24,132,600,34,21,UiFactory.Muted);
                    UiFactory.Button(card.transform,"进入游戏  →",24,184,600,62,()=>EnterAgeOfWar(selected),true);
                }
            }
            frame.GetComponent<SafeDesignFrame>().WidthOnly=false;
            activePage=Page.AgeOfWarSelection;lobbyPage.SetActive(false);gamePage.SetActive(false);ageOfWarSelection.SetActive(true);
            if(status!=null)status.gameObject.SetActive(false);
        }
        private void ShowAgeOfWarHelp()
        {
            if(activePage!=Page.AgeOfWarSelection||paused||!focused)return;
            var panel=CreateModal("战争进化史玩法",1100,CloseModal);
            float height=panel.rect.height;
            var viewport=UiFactory.Rect(panel,"Age of War help viewport",32,114,532,height-236);
            var background=viewport.gameObject.AddComponent<Image>();background.color=Color.clear;background.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=UiFactory.Rect(viewport,"Age of War help content",0,0,532,0);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            float top=0;
            AddAgeOfWarHelpSection(content,ref top,"守住基地，推进战线",
                "士兵出场后会自动前进和攻击。摧毁敌方基地即可获胜；己方基地被摧毁则失败。合理分配金币，在出兵、炮塔防守与升级之间取得平衡。");
            AddAgeOfWarHelpSection(content,ref top,"一代 · 五个时代",
                "训练士兵需要金币和时间，可连续点选加入训练队列。积累经验后进化，解锁下一时代的士兵与炮塔。\n\n右上角技能蓄满后即可使用，效果随时代变化：第三时代恢复己方士兵生命，其他时代发动战场攻击。");
            AddAgeOfWarHelpSection(content,ref top,"二代 · 七个时代",
                "除了招募士兵、建造炮塔和时代进化，还可升级兵种能力。根据当前资源与敌军阵容调整组合。\n\n特殊技能开局充能 60 游戏秒，每次使用后也需冷却 60 游戏秒；暂停时不计时，倍速会加快充能。第一时代的落石还需 2000 经验。\n\n敌方技能按独立时序发动，不会重置你的冷却。后续时代技能的效果和经验消耗，请查看技能提示。");
            AddAgeOfWarHelpSection(content,ref top,"手机操作",
                "横屏游玩，点按按钮训练、建造或进化；建造炮塔时，先选型号，再点空炮位。\n\n在战场空白处左右拖动，可查看双方基地和战线。\n\n点击顶部中央暂停按钮；手机返回也可打开暂停页（沉浸式可能需先唤出导航）。一代点暂停页空白处恢复，点 Exit 返回大厅；二代点 Return 恢复，或先返回游戏菜单，再点 Exit 返回大厅。");
            content.sizeDelta=new Vector2(532,top);
            UiFactory.Button(panel,"知道了",32,height-100,532,74,CloseModal,true);
            dismissModal=CloseModal;
        }
        private static void AddAgeOfWarHelpSection(RectTransform content,ref float top,string title,string body)
        {
            UiFactory.Label(content,title,0,top,532,44,25,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold).supportRichText=false;
            top+=50;
            var label=UiFactory.Label(content,body,0,top,532,1,24,UiFactory.Muted,TextAnchor.UpperLeft);
            label.supportRichText=false;
            float bodyHeight=Mathf.Ceil(label.preferredHeight);
            label.rectTransform.sizeDelta=new Vector2(532,bodyHeight);
            top+=bodyHeight+28;
        }
        private void EnterAgeOfWar(int edition)
        {
            if(activePage!=Page.AgeOfWarSelection||paused||!focused)return;
            StopAnimation();CloseModal();Save();
            if(ageOfWarPage==null)
            {
                var host=new GameObject("Age of War session host");host.transform.SetParent(transform,false);
                ageOfWarPage=host.AddComponent<AgeOfWarPage>();
                ageOfWarPage.Initialize(FinishAgeOfWarExit,()=>data.SoundEnabled,SetAgeOfWarSound);
                ageOfWarPage.PresentationReady+=()=>{if(IsAgeOfWarPage){ageOfWarSelection.SetActive(false);canvasRoot.gameObject.SetActive(false);}};
            }
            activePage=edition==1?Page.AgeOfWarOne:Page.AgeOfWarTwo;
            SetLandscapeHostInput(false);ageOfWarPage.Enter(edition);
        }
        private void FinishAgeOfWarExit()
        {
            activePage=Page.Lobby;SetLandscapeHostInput(true);canvasRoot.gameObject.SetActive(true);ShowLobby();
            if(ageOfWarPage!=null&&!string.IsNullOrEmpty(ageOfWarPage.Error))SetStatus(ageOfWarPage.Error);
        }
        private void SetAgeOfWarSound(bool enabled)
        {data.SoundEnabled=enabled;if(!enabled)feedback.Stop();Save();}
    }
}
