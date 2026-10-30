using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace XanaduYonder.EditorTools
{
    public static class AgeOfWarAudioProbe
    {
        public static void Run()
        {
            var report=new StringBuilder();
            foreach(var path in Directory.GetFiles("Assets/Resources/AgeOfWar","*",SearchOption.AllDirectories))
            {
                if (Path.GetExtension(path)!=".mp3"&&Path.GetExtension(path)!=".wav") continue;
                var asset=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var key=path.Substring("Assets/Resources/".Length);key=key.Substring(0,key.Length-4);
                var resource=Resources.Load<AudioClip>(key);
                string load="not attempted";
                if(resource!=null)
                {
                    try { load="accepted="+resource.LoadAudioData()+" state="+resource.loadState+" rate="+resource.frequency+" channels="+resource.channels; }
                    catch(Exception error) { load=error.GetType().Name+": "+error.Message; }
                }
                report.AppendLine(path+" importer="+AssetImporter.GetAtPath(path)?.GetType().Name+" direct="+(asset==null?"NULL":asset.name+" samples="+asset.samples+" state="+asset.loadState)+" resource="+(resource==null?"NULL":resource.name)+" load="+load);
            }
            var result=report.ToString();File.WriteAllText("artifacts/logs/age-of-war-audio-probe.txt",result);Debug.Log(result);
        }
    }
}
