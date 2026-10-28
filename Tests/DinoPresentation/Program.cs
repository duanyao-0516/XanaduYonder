using System;
using XanaduYonder.Runtime.Dino;

internal static class Program
{
    private static int assertions, cases, failures;

    private static int Main()
    {
        Run("fixed Gregorian festivals and their boundaries", GregorianFestivals);
        Run("Spring Festival includes lunar days one through seven", SpringFestival);
        Run("Dragon Boat and Mid-Autumn use lunar dates", LunarFestivals);
        Run("two birthdays use their real 2026 and 2027 dates", Birthdays);
        Run("leap months neither duplicate nor shift lunar celebrations", LeapMonths);
        Run("birthday classification is distinct from a general festival", BirthdayClassification);
        Run("unsupported lunar years still support fixed Gregorian celebrations", DateRange);
        Run("time of day and DateTime kind do not change the supplied local date", TimeOfDay);
        Console.WriteLine("Dino presentation checks: " + cases + " cases, " + assertions + " assertions, " + failures + " failures.");
        return failures == 0 ? 0 : 1;
    }

    private static void GregorianFestivals()
    {
        Expect(2025, 12, 31, DinoCelebration.None);
        Expect(2026, 1, 1, DinoCelebration.Festival);
        Expect(2026, 1, 2, DinoCelebration.None);
        Expect(2026, 4, 30, DinoCelebration.None);
        Expect(2026, 5, 1, DinoCelebration.Festival);
        Expect(2026, 5, 2, DinoCelebration.None);
        Expect(2026, 9, 30, DinoCelebration.None);
        for (int day = 1; day <= 7; day++) Expect(2026, 10, day, DinoCelebration.Festival);
        Expect(2026, 10, 8, DinoCelebration.None);
        Expect(2026, 10, 30, DinoCelebration.None);
        Expect(2026, 10, 31, DinoCelebration.Halloween);
        Expect(2026, 11, 1, DinoCelebration.None);
        Expect(2026, 12, 24, DinoCelebration.None);
        Expect(2026, 12, 25, DinoCelebration.Christmas);
        Expect(2026, 12, 26, DinoCelebration.None);
    }

    private static void SpringFestival()
    {
        Expect(2026, 2, 16, DinoCelebration.None);
        for (int day = 17; day <= 23; day++) Expect(2026, 2, day, DinoCelebration.Festival);
        Expect(2026, 2, 24, DinoCelebration.None);
        Expect(2027, 2, 5, DinoCelebration.None);
        for (int day = 6; day <= 12; day++) Expect(2027, 2, day, DinoCelebration.Festival);
        Expect(2027, 2, 13, DinoCelebration.None);
        Expect(2027, 1, 15, DinoCelebration.None); // Still in the previous lunar year.
    }

    private static void LunarFestivals()
    {
        Expect(2026, 6, 18, DinoCelebration.None);
        Expect(2026, 6, 19, DinoCelebration.Festival);
        Expect(2026, 6, 20, DinoCelebration.None);
        Expect(2026, 9, 24, DinoCelebration.None);
        Expect(2026, 9, 25, DinoCelebration.Festival);
        Expect(2026, 9, 26, DinoCelebration.None);
        Expect(2027, 6, 9, DinoCelebration.Festival);
        Expect(2027, 9, 15, DinoCelebration.Festival);
    }

    private static void Birthdays()
    {
        foreach (var date in new[] { new DateTime(2026, 5, 31), new DateTime(2026, 6, 30),
            new DateTime(2027, 5, 20), new DateTime(2027, 6, 20) })
        {
            Expect(date.AddDays(-1), DinoCelebration.None);
            Expect(date, DinoCelebration.Birthday);
            Expect(date.AddDays(1), DinoCelebration.None);
        }
    }

    private static void LeapMonths()
    {
        // 2023 has leap month two: the calendar API reports raw months 6 and 9 for these festivals.
        Expect(2023, 6, 22, DinoCelebration.Festival);
        Expect(2023, 9, 29, DinoCelebration.Festival);
        Expect(2023, 6, 2, DinoCelebration.Birthday);
        Expect(2023, 7, 3, DinoCelebration.Birthday);
        // Leap fourth/fifth months must not repeat birthdays or Dragon Boat Festival.
        Expect(2020, 5, 7, DinoCelebration.Birthday);
        Expect(2020, 6, 6, DinoCelebration.None);
        Expect(2009, 6, 8, DinoCelebration.Birthday);
        Expect(2009, 7, 8, DinoCelebration.None);
        Expect(2009, 6, 27, DinoCelebration.None);
    }

    private static void BirthdayClassification()
    {
        // The requested dates do not overlap the current festival windows. Birthday is the
        // more specific classification; the resolver checks it before any festival rule.
        Expect(2026, 5, 31, DinoCelebration.Birthday);
        Expect(2026, 6, 30, DinoCelebration.Birthday);
    }

    private static void DateRange()
    {
        Expect(DateTime.MinValue, DinoCelebration.Festival); // Gregorian New Year still works.
        Expect(DateTime.MaxValue, DinoCelebration.None);
        Expect(1800, 12, 25, DinoCelebration.Christmas);
        Expect(2200, 10, 31, DinoCelebration.Halloween);
        Expect(2200, 5, 1, DinoCelebration.Festival);
        Expect(2200, 10, 7, DinoCelebration.Festival);
        Expect(1901, 2, 18, DinoCelebration.None);
        Expect(1901, 2, 19, DinoCelebration.Festival);
        Expect(2101, 1, 28, DinoCelebration.None);
        Expect(2101, 1, 29, DinoCelebration.None);
    }

    private static void TimeOfDay()
    {
        foreach (var kind in new[] { DateTimeKind.Local, DateTimeKind.Utc, DateTimeKind.Unspecified })
        {
            Expect(new DateTime(2026, 5, 31, 0, 0, 0, kind), DinoCelebration.Birthday);
            Expect(new DateTime(2026, 5, 31, 23, 59, 59, 999, kind), DinoCelebration.Birthday);
            Expect(new DateTime(2026, 10, 31, 23, 59, 59, kind), DinoCelebration.Halloween);
        }
    }

    private static void Expect(int year, int month, int day, DinoCelebration expected)
    { Expect(new DateTime(year, month, day), expected); }

    private static void Expect(DateTime date, DinoCelebration expected)
    {
        assertions++;
        var actual = DinoCelebrationCalendar.Resolve(date);
        if (actual != expected) throw new Exception(date.ToString("yyyy-MM-dd HH:mm:ss") + ": expected " + expected + ", got " + actual);
    }

    private static void Run(string name, Action check)
    {
        cases++;
        try { check(); Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL: " + name + ": " + error.Message); }
    }
}
