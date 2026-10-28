using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using XanaduYonder.Core.Zuma;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
        passed++;
    }
    private static object Field(object row, string field)
    {
        return row.GetType().GetField(field).GetValue(row);
    }
    private static void Main()
    {
        var content = Assembly.GetExecutingAssembly().GetType("XanaduYonder.Runtime.Zuma.ZumaHelpContent");
        Check(content != null, "Help content must exist independently of Unity UI.");
        var terms = (IEnumerable)content.GetProperty("Terms").GetValue(null);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms)
        {
            var english = (string)Field(term, "English");
            Check(!string.IsNullOrWhiteSpace(english) && keys.Add(english), "Each English label needs one unambiguous glossary entry: " + english);
            Check(!string.IsNullOrWhiteSpace((string)Field(term, "Chinese")) && !string.IsNullOrWhiteSpace((string)Field(term, "Function")), "Each glossary entry needs both translation and function.");
        }
        var expectedIds = new HashSet<string>(new[] { "rabbit", "eagle", "jaguar", "sun_god" });
        var difficultyCount = 0;
        var priorCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            foreach (var row in (IEnumerable)content.GetProperty("Difficulties").GetValue(null))
            {
                var id = (string)Field(row, "Id");
                Check(expectedIds.Remove(id), "Each playable difficulty must appear exactly once: " + id);
                var shown = (ZumaDifficulty)Field(row, "Rules");
                var actual = ZumaDifficulty.Get(id);
                Check(shown.Id == id && shown.ColorCount == actual.ColorCount && shown.StartCount == actual.StartCount && shown.GaugeScore == actual.GaugeScore && shown.AceTimeSeconds == actual.AceTimeSeconds && shown.Speed == actual.Speed && shown.SlowFactor == actual.SlowFactor, "Help parameters must match the actual Core configuration: " + id);
                var parameters = (string)row.GetType().GetProperty("Parameters").GetValue(row);
                Check(parameters.Contains(actual.ColorCount.ToString(CultureInfo.InvariantCulture)) && parameters.Contains(actual.StartCount.ToString(CultureInfo.InvariantCulture)) && parameters.Contains(actual.GaugeScore.ToString(CultureInfo.InvariantCulture)) && parameters.Contains(actual.AceTimeSeconds.ToString(CultureInfo.InvariantCulture)), "Displayed parameters must include current color/initial-ball/threshold/ACE values: " + id);
                Check(keys.Contains((string)Field(row, "English")), "Difficulty names in the UI must also have glossary translations: " + id);
                difficultyCount++;
            }
        }
        finally { CultureInfo.CurrentCulture = priorCulture; }
        Check(difficultyCount == 4 && expectedIds.Count == 0, "All four playable difficulties must be documented.");
        var sections = (IEnumerable)content.GetProperty("Sections").GetValue(null);
        var sectionTitles = new HashSet<string>();
        foreach (var section in sections)
        {
            Check(sectionTitles.Add((string)Field(section, "Title")) && !string.IsNullOrWhiteSpace((string)Field(section, "Body")), "Rules need uniquely titled, nonempty sections.");
        }
        Console.WriteLine("Zuma help checks: " + passed + " passed, 0 failed.");
    }
}
