using System;
using System.Globalization;

namespace XanaduYonder.Runtime.Dino
{
    internal enum DinoCelebration { None, Festival, Halloween, Christmas, Birthday }

    internal static class DinoCelebrationCalendar
    {
        private static readonly ChineseLunisolarCalendar Lunar = new ChineseLunisolarCalendar();

        // The caller supplies a device-local date once on entering Dino. Do not read
        // the clock here: a paused/resized run must retain the same celebration.
        // These are celebration dates, not a calendar of statutory leave or makeup workdays.
        internal static DinoCelebration Resolve(DateTime localDate)
        {
            var date = localDate.Date;
            int lunarMonth = 0, lunarDay = 0;
            if (date >= Lunar.MinSupportedDateTime && date <= Lunar.MaxSupportedDateTime)
            {
                int year = Lunar.GetYear(date);
                int month = Lunar.GetMonth(date);
                int leap = Lunar.GetLeapMonth(year);
                if (month != leap)
                {
                    // GetMonth inserts the leap month into its 1..13 numbering. Celebrations
                    // use ordinary 1..12 month names, and never repeat in the leap month.
                    lunarMonth = leap > 0 && month > leap ? month - 1 : month;
                    lunarDay = Lunar.GetDayOfMonth(date);
                }
            }

            if ((lunarMonth == 4 && lunarDay == 15) || (lunarMonth == 5 && lunarDay == 16))
                return DinoCelebration.Birthday;

            if (date.Month == 10 && date.Day == 31) return DinoCelebration.Halloween;
            if (date.Month == 12 && date.Day == 25) return DinoCelebration.Christmas;
            if ((date.Month == 1 && date.Day == 1) || (date.Month == 5 && date.Day == 1) ||
                (date.Month == 10 && date.Day <= 7)) return DinoCelebration.Festival;
            if ((lunarMonth == 1 && lunarDay <= 7) || (lunarMonth == 5 && lunarDay == 5) ||
                (lunarMonth == 8 && lunarDay == 15)) return DinoCelebration.Festival;
            return DinoCelebration.None;
        }
    }
}
