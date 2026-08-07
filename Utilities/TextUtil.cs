using System.Net;
using System.Text.RegularExpressions;

namespace ForumCenter.Utilities;





public static class TextUtil
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex BrRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClosePRegex = new(@"</p\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    
    private static readonly Regex TripleBacktickPattern = new(@"```[^\n]*\n([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex CodeSpanPattern = new(@"(?<!`)`([^`\n]+)`(?!`)", RegexOptions.Compiled);

    
    
    
    
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        try
        {
            
            var escaped = EscapeMarkdownCode(html);

            
            var text = HtmlTagRegex.Replace(escaped, "");
            text = WebUtility.HtmlDecode(text);
            return text.Trim();
        }
        catch
        {
            try
            {
                return HtmlTagRegex.Replace(html, "").Trim();
            }
            catch
            {
                return html;
            }
        }
    }

    
    
    
    
    public static string EscapeMarkdownCode(string html)
    {
        var result = html;

        
        result = TripleBacktickPattern.Replace(result, m =>
        {
            var codeContent = m.Groups[1].Value;
            var escaped = codeContent
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
            return $"```\n{escaped}\n```";
        });

        
        result = CodeSpanPattern.Replace(result, m =>
        {
            var codeContent = m.Groups[1].Value;
            var escaped = codeContent
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
            return $"`{escaped}`";
        });

        return result;
    }

    
    
    
    public static string FormatTime(long timestamp)
    {
        if (timestamp <= 0) return "";
        try
        {
            
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
            return dt.ToString("MM-dd HH:mm");
        }
        catch
        {
            return "";
        }
    }

    
    
    
    public static string FormatTime(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return "";
        try
        {
            
            if (long.TryParse(timeStr, out var ts))
            {
                if (ts > 0)
                {
                    
                    var dto = ts > 1000000000000L
                        ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
                        : DateTimeOffset.FromUnixTimeSeconds(ts);
                    return dto.LocalDateTime.ToString("MM-dd HH:mm");
                }
            }
            return timeStr;
        }
        catch (FormatException)
        {
            return timeStr;
        }
    }

    
    
    
    public static string Summarize(string? html, int maxLen = 80)
    {
        var plain = StripHtml(html);
        if (plain.Length > maxLen)
            return plain[..maxLen] + "...";
        return plain;
    }

    
    
    
    
    public static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        var text = BrRegex.Replace(html, "\n");
        text = ClosePRegex.Replace(text, "\n");
        text = HtmlTagRegex.Replace(text, "");
        text = WebUtility.HtmlDecode(text);

        return text.Trim();
    }
}
