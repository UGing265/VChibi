using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace VChibi.Services;

public static class NaturalSortHelper
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    public static int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return StrCmpLogicalW(x, y);
            }
            catch
            {
                // Fallback to managed natural compare below
            }
        }

        return FallbackNaturalCompare(x, y);
    }

    private static int FallbackNaturalCompare(string x, string y)
    {
        var regex = new Regex(@"(\d+)|(\D+)", RegexOptions.Compiled);
        var xMatches = regex.Matches(x);
        var yMatches = regex.Matches(y);

        int count = Math.Min(xMatches.Count, yMatches.Count);
        for (int i = 0; i < count; i++)
        {
            var xPart = xMatches[i].Value;
            var yPart = yMatches[i].Value;

            if (char.IsDigit(xPart[0]) && char.IsDigit(yPart[0]))
            {
                if (ulong.TryParse(xPart, out var xVal) && ulong.TryParse(yPart, out var yVal))
                {
                    int comp = xVal.CompareTo(yVal);
                    if (comp != 0) return comp;
                }
            }

            int strComp = string.Compare(xPart, yPart, StringComparison.CurrentCultureIgnoreCase);
            if (strComp != 0) return strComp;
        }

        return xMatches.Count.CompareTo(yMatches.Count);
    }
}
