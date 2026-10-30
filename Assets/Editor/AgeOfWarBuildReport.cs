using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    internal static class AgeOfWarBuildReport
    {
        internal static void Write(BuildReport report)
        {
            var rows=new SortedDictionary<string,ulong>(StringComparer.Ordinal);
            foreach(var packed in report.packedAssets)
                foreach(var item in packed.contents)
                {
                    string path=item.sourceAssetPath??"";
                    if(!path.StartsWith("Assets/Resources/AgeOfWar/",StringComparison.Ordinal))continue;
                    ulong size;rows.TryGetValue(path,out size);rows[path]=size+item.packedSize;
                }
            var csv=new StringBuilder("sourceAssetPath,packedBytes\n");ulong total=0;
            foreach(var item in rows){csv.Append('"').Append(item.Key.Replace("\"","\"\"")).Append("\",").Append(item.Value).Append('\n');total+=item.Value;}
            var folder=Path.GetFullPath("artifacts/validation/age-of-war-build");Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"resources.csv"),csv.ToString());
            File.WriteAllText(Path.Combine(folder,"summary.txt"),
                "APK="+report.summary.outputPath+"\nAPKBytes="+new FileInfo(report.summary.outputPath).Length+
                "\nAgeOfWarPackedResourceBytes="+total+"\nSourceAssetCount="+rows.Count+
                "\nThese are BuildReport packed-asset bytes before APK compression. They exclude shared runtime/code and are not an exact APK delta against a differently configured release.\n");
        }
    }
}
