using System;
using System.Collections.Generic;
using XanaduYonder.Runtime.AgeOfWar;
namespace UnityEngine {
 public class Object { public bool destroyed; public static void Destroy(Object o){o.destroyed=true;} }
 public class Texture2D:Object { public int width=16,height=16; }
 public class AudioClip:Object {}
 public class Font:Object { public static string[] GetOSInstalledFontNames(){return new string[0];} public static Font CreateDynamicFontFromOSFont(string s,int i){return new Font();} }
 public class TextAsset:Object {public string text; public TextAsset(string t){text=t;} }
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;} }
 public struct Rect {public Rect(float x,float y,float w,float h){} }
 public enum SpriteMeshType {FullRect}
 public class Sprite:Object {public string name;public Texture2D texture;public static Sprite Create(Texture2D t,Rect r,Vector2 p,float scale,uint extrude,SpriteMeshType mesh){return new Sprite{texture=t};} }
 public static class Resources {public static Dictionary<string,Object> items=new Dictionary<string,Object>();public static T Load<T>(string p) where T:Object{return items.ContainsKey(p)?(T)items[p]:null;}public static void UnloadAsset(Object a){a.destroyed=true;} }
 public static class JsonUtility {public static Dictionary<string,AgeOfWarAssetManifest> items=new Dictionary<string,AgeOfWarAssetManifest>();public static T FromJson<T>(string key){return (T)(object)items[key];} }
}
class Test {
 static int count;static void Check(bool b,string s){if(!b)throw new Exception(s);count++;}
 static AgeOfWarAssetManifest M(string q,int ticks=2){return new AgeOfWarAssetManifest{schemaVersion=1,edition=2,quality=new AgeOfWarQuality{name=q},atlases=new[]{new AgeOfWarAtlas{resource=q,width=16,height=16}},clips=new[]{new AgeOfWarClip{key="unit/2",action="idle",frames=new[]{new AgeOfWarAssetFrame{w=4,h=4,scale=1,ticks=ticks}}}},scenes=new AgeOfWarScene[0],sounds=new AgeOfWarSound[0],fonts=new AgeOfWarFont[0]};}
 static void Install(string q,int ticks=2){UnityEngine.JsonUtility.items[q]=M(q,ticks);UnityEngine.Resources.items["AgeOfWar/Two/"+(q=="High"?"manifest":"Quality/"+q+"/manifest")]=new UnityEngine.TextAsset(q);UnityEngine.Resources.items[q]=new UnityEngine.Texture2D();}
 static void Main(){Install("High");Install("Low");Install("Medium");var a=AgeOfWarAssets.Load(2);AgeOfWarSpriteFrame old,newf;Check(a.TryGetFrame("unit/2","idle",0,out old),"initial frame");
 Check(a.SetQuality("low",()=>{Check(!old.Sprite.destroyed&&!old.Sprite.texture.destroyed,"old generation retired before callback");Check(a.Revision==1&&a.Quality=="Low","callback sees new revision");Check(a.TryGetFrame("unit/2","idle",0,out newf)&&newf.Sprite.texture!=old.Sprite.texture,"callback gets actual new atlas");}),"switch changed");
 Check(old.Sprite.destroyed&&old.Sprite.texture.destroyed,"old generation not retired after callback");Check(!a.SetQuality("LOW",()=>{throw new Exception("duplicate callback");}),"duplicate quality switched");
 Install("Medium",3);try{a.SetQuality("Medium",()=>{});throw new Exception("bad duration accepted");}catch(InvalidOperationException){}Check(a.Revision==1&&a.Quality=="Low","invalid catalog mutated state");Install("Medium");
 a.TryGetFrame("unit/2","idle",0,out old);try{a.SetQuality("Medium",()=>{throw new ApplicationException();});}catch(ApplicationException){}Check(!old.Sprite.destroyed&&!old.Sprite.texture.destroyed,"failed rebind destroyed previous Images");a.Dispose();Check(old.Sprite.destroyed&&old.Sprite.texture.destroyed,"failed generation leaked past Dispose");
 Console.WriteLine("PASS "+count+" quality loader lifecycle assertions (Unity API test doubles; real Unity pending)");}
}
