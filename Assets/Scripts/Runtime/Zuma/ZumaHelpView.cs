using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Zuma
{
    // Read-only help belongs to the Zuma view. It never owns a game, save store or navigation state.
    internal sealed class ZumaHelpView
    {
        private const float ContentWidth=1108;
        private readonly RectTransform panel;
        private readonly Button[] tabs=new Button[3];
        private readonly string[] titles={"基本玩法","英文对照","难度说明"};
        private RectTransform body;

        public ZumaHelpView(RectTransform root,Action close)
        {
            var blocker=root.gameObject.AddComponent<Image>();
            blocker.color=new Color(0,0,0,.58f);blocker.raycastTarget=true;
            var paper=UiFactory.Panel(root,"Xanadu Zuma help",48,32,1184,656,UiFactory.Paper);
            paper.raycastTarget=true;panel=paper.rectTransform;
            UiFactory.Label(panel,"Zuma · 玩法说明",28,16,900,58,34,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            UiFactory.Button(panel,"关闭",1008,20,148,52,close);
            UiFactory.Label(panel,"熟悉操作，再慢慢挑战。英文界面的翻译与功能都在这里。",28,76,1096,28,19,UiFactory.Muted);
            for(var i=0;i<tabs.Length;i++)
            {
                var index=i;
                tabs[i]=UiFactory.Button(panel,titles[i],28+i*190,112,178,46,()=>ShowTab(index));
                tabs[i].GetComponentInChildren<Text>().fontSize=22;
            }
            UiFactory.Label(panel,"向上滑动查看完整内容 · 系统返回键也可关闭",28,618,1128,28,18,UiFactory.Muted);
            ShowTab(0);
        }

        private void ShowTab(int index)
        {
            for(var i=0;i<tabs.Length;i++)
            {
                tabs[i].GetComponent<Image>().color=i==index?UiFactory.Accent:UiFactory.Hex("E7EDE4");
                tabs[i].GetComponentInChildren<Text>().color=i==index?Color.white:UiFactory.Ink;
            }
            if(body!=null){body.gameObject.SetActive(false);UnityEngine.Object.Destroy(body.gameObject);}
            body=UiFactory.Rect(panel,"Help scroll area",28,174,1128,432);
            var viewport=UiFactory.Rect(body,"Clipped help viewport",0,0,ContentWidth,432);
            var surface=viewport.gameObject.AddComponent<Image>();surface.color=Color.clear;surface.raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=UiFactory.Rect(viewport,"Help document",0,0,ContentWidth,432);
            float height;
            if(index==1)height=BuildTerms(content);
            else if(index==2)height=BuildDifficulties(content);
            else height=BuildRules(content);
            content.sizeDelta=new Vector2(ContentWidth,Mathf.Max(432,height));
            var scroll=body.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var track=UiFactory.Panel(body,"Help scrollbar",1116,0,10,432,UiFactory.Hex("E7EDE4"));
            track.raycastTarget=true;
            var sliding=UiFactory.Rect(track.transform,"Scrollbar sliding area",0,0,10,432);
            var handle=UiFactory.Panel(sliding,"Scrollbar handle",0,0,10,80,UiFactory.Muted);
            UiFactory.Stretch(handle.rectTransform);
            var bar=track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.verticalNormalizedPosition=1;
        }

        private static Text Paragraph(Transform parent,string text,float x,float y,float width,int size,Color color,bool bold=false)
        {
            var label=UiFactory.Label(parent,text,x,y,width,32,size,color,TextAnchor.UpperLeft,bold?FontStyle.Bold:FontStyle.Normal);
            label.supportRichText=false;
            label.rectTransform.sizeDelta=new Vector2(width,Mathf.Ceil(Mathf.Max(32,label.preferredHeight)));
            return label;
        }

        private static float BuildRules(RectTransform content)
        {
            var y=0f;
            foreach(var section in ZumaHelpContent.Sections)
            {
                var title=Paragraph(content,section.Title,16,y+12,1076,24,UiFactory.Ink,true);
                var text=Paragraph(content,section.Body,16,y+20+title.rectTransform.rect.height,1076,22,UiFactory.Ink);
                y+=title.rectTransform.rect.height+text.rectTransform.rect.height+42;
            }
            return y;
        }

        private static float BuildTerms(RectTransform content)
        {
            var header=UiFactory.Panel(content,"Glossary header",0,0,ContentWidth,44,UiFactory.Hex("DCE7DD"));
            UiFactory.Label(header.transform,"英文",16,0,224,44,21,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            UiFactory.Label(header.transform,"中文",254,0,164,44,21,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            UiFactory.Label(header.transform,"功能 / 含义",432,0,656,44,21,UiFactory.Ink,TextAnchor.MiddleLeft,FontStyle.Bold);
            var y=50f;var index=0;
            foreach(var term in ZumaHelpContent.Terms)
            {
                var row=UiFactory.Panel(content,"Term "+term.English,0,y,ContentWidth,64,index++%2==0?UiFactory.Hex("EDF0E7"):UiFactory.Hex("FAF9F4"));
                var english=Paragraph(row.transform,term.English,16,12,224,21,UiFactory.Accent,true);
                var chinese=Paragraph(row.transform,term.Chinese,254,12,164,21,UiFactory.Ink,true);
                var meaning=Paragraph(row.transform,term.Function,432,12,656,21,UiFactory.Ink);
                var height=Mathf.Max(english.rectTransform.rect.height,Mathf.Max(chinese.rectTransform.rect.height,meaning.rectTransform.rect.height))+24;
                row.rectTransform.sizeDelta=new Vector2(ContentWidth,height);y+=height+4;
            }
            return y;
        }

        private static float BuildDifficulties(RectTransform content)
        {
            var intro=Paragraph(content,"18 张地图、4 档难度都可以直接选择，共 72 种组合。颜色数量、生成速度和终点附近的缓速共同影响难度，不能只比较速度。",16,12,1076,22,UiFactory.Ink);
            var y=intro.rectTransform.rect.height+36;
            foreach(var item in ZumaHelpContent.Difficulties)
            {
                var card=UiFactory.Panel(content,"Difficulty "+item.Id,0,y,ContentWidth,180,UiFactory.Hex("E7EDE4"));
                var title=Paragraph(card.transform,item.English+" · "+item.Chinese,16,12,1076,24,UiFactory.Ink,true);
                var parameters=Paragraph(card.transform,item.Parameters,16,22+title.rectTransform.rect.height,1076,21,UiFactory.Accent,true);
                var description=Paragraph(card.transform,item.Description,16,32+title.rectTransform.rect.height+parameters.rectTransform.rect.height,1076,22,UiFactory.Ink);
                var height=44+title.rectTransform.rect.height+parameters.rectTransform.rect.height+description.rectTransform.rect.height;
                card.rectTransform.sizeDelta=new Vector2(ContentWidth,height);y+=height+14;
            }
            return y;
        }
    }
}
