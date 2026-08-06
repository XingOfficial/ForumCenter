using System.Text.RegularExpressions;

namespace ForumCenter.Utilities;

/// <summary>
/// HTML 兼容工具 - 移植自 Kotlin HtmlCompat.kt
/// </summary>
public static class HtmlCompat
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// 解析 HTML 为纯文本（降级安全处理）
    /// </summary>
    public static string ParseHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        try
        {
            // .NET MAUI 没有 Html.fromHtml，用正则去除标签
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

    /// <summary>
    /// 安全解析布尔值：兼容 Boolean / Int / String 类型
    /// 移植自 Kotlin parseBool 扩展函数
    /// </summary>
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
