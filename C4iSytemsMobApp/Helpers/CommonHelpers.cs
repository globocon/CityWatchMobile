using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Net;
using System.Reflection;


namespace C4iSytemsMobApp.Helpers
{
    public static class CommonHelper
    {
        public static bool IsValidEmail(string email)
        {
            var trimmedEmail = email.Trim();
            if (trimmedEmail.EndsWith("."))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == trimmedEmail;
            }
            catch
            {
                return false;
            }
        }

        public static string GetSanitizedFileNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));

            var uri = new Uri(url);

            // Extract file name from URL path
            var rawFileName = Path.GetFileName(uri.LocalPath);

            // Decode URL encoding (%20 -> space)
            var decodedFileName = WebUtility.UrlDecode(rawFileName);

            // Remove invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(decodedFileName
                .Where(c => !invalidChars.Contains(c))
                .ToArray());

            return sanitized;
        }
    }

    public static class DateTimeExtensions
    {
        public static DateTime UtcToAest(this DateTime utcDateTime)
        {
            return utcDateTime.AddHours(10);
        }
    }

    public static class EnumExtensions
    {
        public static string ToDescription(this Enum value)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (name != null)
            {
                FieldInfo field = type.GetField(name);
                if (field != null)
                {
                    DescriptionAttribute attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
                    if (attr != null)
                    {
                        return attr.Description;
                    }
                }
            }
            return null;
        }

        public static string ToDisplayName(this Enum value)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (name != null)
            {
                FieldInfo field = type.GetField(name);
                if (field != null)
                {
                    DisplayAttribute attr = Attribute.GetCustomAttribute(field, typeof(DisplayAttribute)) as DisplayAttribute;
                    if (attr != null)
                    {
                        return attr.GetName();
                    }
                }
            }
            return name;
        }
    }

    // Task p6#73_TimeZone issue -- added by Binoy - Start
    public static class DateTimeHelper
    {
        public static DateTime GetCurrentLocalTimeFromUtcMinute(int utcmin)
        {
            var CurrLocalTime = DateTime.UtcNow.AddMinutes(utcmin);
            return CurrLocalTime;
        }


        public static DateTime GetLogbookEndTimeFromDate(DateTime ldtm)
        {
            return new DateTime(ldtm.Year, ldtm.Month, ldtm.Day, 23, 59, 00);
        }
    }

    // Task p6#73_TimeZone issue -- added by Binoy - End


    public static class TimeZoneHelper
    {

        public static string GetCurrentTimeZone()
        {
            TimeZoneInfo localZone = TimeZoneInfo.Local;
            var mint = (int)localZone.BaseUtcOffset.TotalMinutes;
            string[] arr = Convert.ToString(localZone.BaseUtcOffset).Split(":");
            var CurrLocalTime = localZone.StandardName + " " + string.Format("GMT{0}:{1}", mint > 0 ? '+' + arr[0] : arr[0], arr[1]);
            return CurrLocalTime;
        }
        public static string GetCurrentTimeZoneShortName()
        {
            TimeZoneInfo localZone = TimeZoneInfo.Local;
            var mint = (int)localZone.BaseUtcOffset.TotalMinutes;
            string[] arr = Convert.ToString(localZone.BaseUtcOffset).Split(":");
            var CurrLocalTime = string.Format("GMT{0}:{1}", mint > 0 ? '+' + arr[0] : arr[0], arr[1]);
            return CurrLocalTime;
        }

        public static int GetCurrentTimeZoneOffsetMinute()
        {
            TimeZoneInfo localZone = TimeZoneInfo.Local;
            var CurrLocalTime = localZone.BaseUtcOffset;
            return (int)CurrLocalTime.TotalMinutes;
        }

        public static DateTime GetCurrentTimeZoneCurrentTime()
        {
            var CurrLocalTime = DateTime.Now;
            return CurrLocalTime;
        }

        public static DateTimeOffset GetCurrentTimeZoneCurrentTimeWithOffset()
        {
            var CurrLocalTime = DateTimeOffset.Now;
            return CurrLocalTime;
        }



    }

    public static class ColorConvertorHelper
    {
        public static string GetHexToRGBConvertedColorCode(string HexColorCode)
        {
            var color = ColorTranslator.FromHtml(HexColorCode); // System.Drawing.Color.FromString(HexColorCode);
            // Convert HEX to RGB 
            int r = Convert.ToInt16(color.R);
            int g = Convert.ToInt16(color.G);
            int b = Convert.ToInt16(color.B);

            string rgbColor = string.Format("rgba({0}, {1}, {2});", r, g, b);

            return rgbColor;
        }
    }


}
