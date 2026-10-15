using System;

namespace DBTool.Core
{
    /// <summary>
    /// Resolves the plan and section variants of a cover block configured in CF.Ga.
    /// Template blocks are drawn in millimetres and are inserted at 1:1 model scale.
    /// </summary>
    public static class CoverBlockNameResolver
    {
        private static readonly string[] ViewSuffixes = { ".MB", ".AA", ".BB" };

        public static string ForView(string? configuredName, string viewSuffix)
        {
            if (string.IsNullOrWhiteSpace(configuredName))
                return string.Empty;

            string normalizedSuffix = viewSuffix.Trim().ToUpperInvariant();
            if (Array.IndexOf(ViewSuffixes, normalizedSuffix) < 0)
                throw new ArgumentOutOfRangeException(nameof(viewSuffix), viewSuffix,
                    "Nắp ga chỉ có block mặt bằng .MB và mặt cắt .AA/.BB.");

            string baseName = configuredName.Trim();
            foreach (string suffix in ViewSuffixes)
            {
                if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName[..^suffix.Length];
                    break;
                }
            }

            return baseName + normalizedSuffix;
        }
    }
}
