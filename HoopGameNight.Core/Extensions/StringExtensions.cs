using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HoopGameNight.Core.Extensions
{
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string? value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace(this string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
        }

        public static string ToSlug(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Convert to lowercase
            var slug = input.ToLowerInvariant();

            // Remove special characters
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

            // Replace spaces with hyphens
            slug = Regex.Replace(slug, @"\s+", "-");

            // Remove multiple hyphens
            slug = Regex.Replace(slug, @"-+", "-");

            // Trim hyphens from start and end
            return slug.Trim('-');
        }

        public static string Truncate(this string input, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            return input.Substring(0, maxLength - suffix.Length) + suffix;
        }

        public static bool ContainsIgnoreCase(this string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
                return false;

            return source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        public static string RemoveExtraSpaces(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return Regex.Replace(input.Trim(), @"\s+", " ");
        }
    }
}