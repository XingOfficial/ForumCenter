using System.Text.RegularExpressions;

namespace ForumCenter.Utilities;




public static class HtmlCompat
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    
    
    
    public static string ParseHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        try
        {
            
            var text = HtmlTagRegex.Replace(html, "");
            return System.Net.WebUtility.HtmlDecode(text);
        }
        catch
        {
            try
            {
                return HtmlTagRegex.Replace(html, "");
            }
            catch
            {
                return html ?? "";
            }
        }
    }

    
    
    
    
    public static bool ParseBool(object? value)
    {
        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s => s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
