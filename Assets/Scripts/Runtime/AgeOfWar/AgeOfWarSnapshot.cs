using UnityEngine;
using UnityEngine.UI;
namespace XanaduYonder.Runtime.AgeOfWar
{
    internal static class AgeOfWarSnapshot
    {
        // Render a detached copy at the SWF stage resolution. The live canvas and safe area never move.
        internal static Texture2D Capture(RectTransform stage,int width,int height)
        {
            var owner=new GameObject("Original transition snapshot");
            var target=new RenderTexture(width,height,24);
            var previous=RenderTexture.active;
            Texture2D result=null;
            try
            {
                var root=new GameObject("Snapshot canvas",typeof(RectTransform),typeof(Canvas)).GetComponent<RectTransform>();root.SetParent(owner.transform,false);root.sizeDelta=new Vector2(width,height);
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                var copy=Object.Instantiate(stage,root,false);copy.anchorMin=copy.anchorMax=copy.pivot=new Vector2(0,1);copy.anchoredPosition=Vector2.zero;copy.localScale=Vector3.one;copy.localRotation=Quaternion.identity;copy.sizeDelta=new Vector2(width,height);
                SetLayer(root,31);
                var camera=new GameObject("Snapshot camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(owner.transform,false);camera.transform.position=new Vector3(0,0,-10);
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=height*.5f;camera.aspect=(float)width/height;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                target.Create();camera.targetTexture=target;canvas.worldCamera=camera;
                Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=target;result=new Texture2D(width,height,TextureFormat.RGB24,false);result.ReadPixels(new Rect(0,0,width,height),0,0);result.Apply();return result;
            }
            catch{if(result!=null)Object.Destroy(result);throw;}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(owner);target.Release();Object.Destroy(target);}
        }
        private static void SetLayer(Transform parent,int layer){parent.gameObject.layer=layer;foreach(Transform child in parent)SetLayer(child,layer);}
    }
}
