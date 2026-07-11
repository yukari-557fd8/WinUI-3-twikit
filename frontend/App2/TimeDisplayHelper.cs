using System;
using System.Globalization;

namespace App2
{
    public static class TimeDisplayHelper
    {
        private static readonly string[] KnownFormats =
        [
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm"
        ];

        public static ulong? TryParseSnowflakeId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return ulong.TryParse(id, out var value) ? value : null;
        }

        public static DateTime? TryParse(string? createdAt)
        {
            if (string.IsNullOrWhiteSpace(createdAt))
                return null;

            if (DateTime.TryParse(createdAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var dt))
                return dt;

            if (DateTime.TryParseExact(createdAt, KnownFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt))
                return dt;

            return null;
        }

        public static string FormatAbsolute(DateTime dateTime)
            => dateTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

        public static string FormatRelative(DateTime dateTime, DateTime? now = null)
        {
            var reference = now ?? DateTime.Now;
            var elapsed = reference - dateTime;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            if (elapsed < TimeSpan.FromMinutes(1))
                return "たった今";

            if (elapsed < TimeSpan.FromHours(1))
                return $"{(int)elapsed.TotalMinutes}分前";

            if (elapsed < TimeSpan.FromDays(1))
                return $"{(int)elapsed.TotalHours}時間前";

            return $"{(int)elapsed.TotalDays}日前";
        }

        public static string FormatAbsoluteDisplay(string? createdAt)
        {
            var parsed = TryParse(createdAt);
            if (parsed is null)
                return createdAt ?? string.Empty;

            return FormatAbsolute(parsed.Value);
        }

        public static string FormatRelativeDisplay(string? createdAt, DateTime? now = null)
        {
            var parsed = TryParse(createdAt);
            if (parsed is null)
                return string.Empty;

            return FormatRelative(parsed.Value, now);
        }

        public static string FormatDisplay(string? createdAt, DateTime? now = null)
        {
            var parsed = TryParse(createdAt);
            if (parsed is null)
                return createdAt ?? string.Empty;

            return $"{FormatAbsolute(parsed.Value)} ・ {FormatRelative(parsed.Value, now)}";
        }

        public static string FormatNowForStorage()
            => FormatAbsolute(DateTime.Now);
    }
}
