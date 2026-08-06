using System.Net;
using System.Text.RegularExpressions;

namespace ForumCenter.Utilities;

/// <summary>
/// 文本处理工具类 - 移植自 Kotlin TextUtil.kt
/// 统一 StripHtml / FormatTime / Summarize 等重复方法
/// </summary>
public static class TextUtil
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex BrRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClosePRegex = new(@"</p\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Markdown 代码块正则
    private static readonly Regex TripleBacktickPattern = new(@"```[^\n]*\n([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex CodeSpanPattern = new(@"(?<!`)`([^`\n]+)`(?!`)", RegexOptions.Compiled);

    /// <summary>
    /// 去除 HTML 标签，返回纯文本
    /// 会先转义 Markdown 代码块中的字面 HTML 标签（如 &lt;img&gt;），防止被当作真标签
    /// </summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        try
        {
            // 先转义反引号代码块中的 HTML 标签
            var escaped = EscapeMarkdownCode(html);

            // 用正则去除 HTML 标签（等价于 Html.fromHtml(escaped).toString()）
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

    /// <summary>
    /// 转义 Markdown 代码块中的 HTML 标签
    /// 处理三反引号代码块和单反引号代码段
    /// </summary>
    public static string EscapeMarkdownCode(string html)
    {
        var result = html;

        // 1. 先处理三反引号代码块 ```...```
        result = TripleBacktickPattern.Replace(result, m =>
        {
            var codeContent = m.Groups[1].Value;
            var escaped = codeContent
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
            return $"```\n{escaped}\n```";
        });

        // 2. 再处理单反引号代码段 `...`
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

    /// <summary>
    /// 格式化时间戳为 "MM-dd HH:mm"
    /// </summary>
    public static string FormatTime(long timestamp)
    {
        if (timestamp <= 0) return "";
        try
        {
            // 原版用毫秒级时间戳
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
            return dt.ToString("MM-dd HH:mm");
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 格式化时间字符串：先尝试解析为时间戳，失败则原样返回
    /// </summary>
    public static string FormatTime(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return "";
        try
        {
            // 尝试解析为秒级时间戳
            if (long.TryParse(timeStr, out var ts))
            {
                if (ts > 0)
                {
                    // 判断是秒还是毫秒
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

    /// <summary>
    /// 截取摘要，超长自动加省略号
    /// </summary>
    public static string Summarize(string? html, int maxLen = 80)
    {
        var plain = StripHtml(html);
        if (plain.Length > maxLen)
            return plain[..maxLen] + "...";
        return plain;
    }

    /// <summary>
    /// 将 HTML 内容转为纯文本（用于编辑模式预填内容）
    /// 将 &lt;br&gt; 转为换行，去除所有标签，HTML 解码
    /// </summary>
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
