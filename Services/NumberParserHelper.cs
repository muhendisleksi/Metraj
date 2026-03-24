using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Metraj.Services
{
    public static class NumberParserHelper
    {
        /// <summary>
        /// Parses a number string supporting both Turkish (1.234,56) and English (1,234.56) formats.
        /// Strips prefix/suffix text.
        /// </summary>
        public static bool TryParse(string input, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            // Extract number part using regex
            string cleaned = ExtractNumber(input.Trim());
            if (string.IsNullOrEmpty(cleaned)) return false;

            // Detect format: if has both . and , determine which is decimal separator
            bool hasComma = cleaned.Contains(",");
            bool hasDot = cleaned.Contains(".");

            if (hasComma && hasDot)
            {
                // Find which comes last — that's the decimal separator
                int lastComma = cleaned.LastIndexOf(',');
                int lastDot = cleaned.LastIndexOf('.');

                if (lastComma > lastDot)
                {
                    // Turkish: 1.234,56 → remove dots, replace comma with dot
                    cleaned = cleaned.Replace(".", "").Replace(",", ".");
                }
                else
                {
                    // English: 1,234.56 → remove commas
                    cleaned = cleaned.Replace(",", "");
                }
            }
            else if (hasComma)
            {
                // Could be Turkish decimal (12,5) or Turkish thousands (1,234)
                // If exactly 3 digits after comma, treat as thousands; otherwise decimal
                var parts = cleaned.Split(',');
                if (parts.Length == 2 && parts[1].Length == 3 && parts[0].Length > 0)
                {
                    // Likely thousands: 1,234
                    cleaned = cleaned.Replace(",", "");
                }
                else
                {
                    // Decimal: 12,5 or 12,50
                    cleaned = cleaned.Replace(",", ".");
                }
            }
            // If only dots: could be English decimal or Turkish thousands
            // Default: treat as decimal (most common for measurements)

            return double.TryParse(cleaned, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// Extracts the numeric part from a string with prefix/suffix.
        /// Examples: "A=125,30" → "125,30", "45.6 m²" → "45.6", "L: 12.5m" → "12.5"
        /// </summary>
        public static string ExtractNumber(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Match numbers with optional thousands separators and decimal
            var match = Regex.Match(input, @"-?\d[\d.,]*\d|-?\d+");
            return match.Success ? match.Value : null;
        }

        /// <summary>
        /// Strips a known prefix from the input.
        /// </summary>
        public static string StripPrefix(string input, string prefix)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(prefix)) return input;
            var trimmed = input.TrimStart();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring(prefix.Length);
            return input;
        }

        /// <summary>
        /// Strips a known suffix from the input.
        /// </summary>
        public static string StripSuffix(string input, string suffix)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(suffix)) return input;
            var trimmed = input.TrimEnd();
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring(0, trimmed.Length - suffix.Length);
            return input;
        }
    }
}
