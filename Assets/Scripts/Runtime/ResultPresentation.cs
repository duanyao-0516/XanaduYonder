using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Opted into only by terminal result flows. It never intercepts ordinary dialogs or owns game state.
    internal sealed class ResultPresentation : MonoBehaviour
    {
        private readonly ResultPresentationState state=new ResultPresentationState();
        private RectTransform root;
        private Func<bool> canShow;
        private Action showResult;
        private Button reopen;
        private bool applicationPaused,applicationFocused=true;
        public static ResultPresentation For(MonoBehaviour owner,RectTransform parent)
        {
            var value=owner.GetComponent<ResultPresentation>();
            if(value==null)value=owner.gameObject.AddComponent<ResultPresentation>();
            value.root=parent;return value;
        }
        public static void Clear(MonoBehaviour owner)
        { var value=owner.GetComponent<ResultPresentation>();if(value!=null)value.Reset(); }
        public static bool IsInspecting(MonoBehaviour owner)
        { var value=owner.GetComponent<ResultPresentation>();return value!=null&&value.state.IsInspecting; }
        public bool Gate(object identity,Func<bool> available,Action show)
        {
            canShow=available;showResult=show;
            bool result=state.Request(identity);
            if(result&&reopen!=null)reopen.gameObject.SetActive(false);
            return result;
        }
        public void Inspect(Action closeResult)
        {
            state.Inspect();closeResult?.Invoke();
            EnsureReopen();reopen.gameObject.SetActive(true);reopen.transform.SetAsLastSibling();
        }
        public void Reset()
        {
            state.Reset();canShow=null;showResult=null;
            if(reopen!=null){reopen.gameObject.SetActive(false);Destroy(reopen.gameObject);reopen=null;}
        }
        private void EnsureReopen()
        {
            if(reopen!=null||root==null)return;
            float scale=Mathf.Min(1,root.rect.width/720f);
            reopen=UiFactory.Button(root,"查看结算",0,0,232*scale,64*scale,Reopen,true);
            reopen.name="Reopen result";
            var rect=reopen.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=new Vector2(0,24*scale);
            reopen.GetComponentInChildren<Text>().fontSize=Mathf.Max(12,Mathf.RoundToInt(26*scale));
        }
        internal void Reopen()
        {
            if(applicationPaused||!applicationFocused||canShow==null||!canShow())return;
            state.Reopen();if(reopen!=null)reopen.gameObject.SetActive(false);showResult?.Invoke();
        }
        private void Update()
        { Advance(Time.unscaledDeltaTime); }
        // Editor checks drive the exact foreground presentation path without sleeping.
        internal void Advance(float deltaSeconds)
        {
            bool available=!applicationPaused&&applicationFocused&&canShow!=null&&canShow();
            if(state.IsInspecting&&available)
            {
                EnsureReopen();
                if(reopen!=null){if(reopen.transform.parent!=root)reopen.transform.SetParent(root,false);reopen.transform.SetAsLastSibling();}
            }
            if(reopen!=null)reopen.gameObject.SetActive(state.IsInspecting&&available);
            if(state.Advance(deltaSeconds,available))showResult?.Invoke();
        }
        private void OnDisable(){Reset();}
        private void OnEnable(){state.Interrupt();}
        private void OnApplicationPause(bool value){applicationPaused=value;state.Interrupt();}
        private void OnApplicationFocus(bool value){applicationFocused=value;state.Interrupt();}
        private void OnDestroy(){Reset();}
    }
}
