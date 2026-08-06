using System.Net;
using System.Text.RegularExpressions;

namespace ForumCenter.Utilities;

/// <summary>
/// HTML 渲染工具 - 移植自 Kotlin HtmlRenderer.kt
/// 支持 Markdown 代码块转义、&lt;audio&gt;/&lt;video&gt; 标签转为链接、&lt;div&gt; 去除
/// 在 MAUI 中通过 Label.TextType = TextType.Html 渲染
/// </summary>
public static class HtmlRenderer
{
    // Markdown 代码块正则
    private static readonly Regex TripleBacktickPattern = new(@"```[^\n]*\n([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex CodeSpanPattern = new(@"(?<!`)`([^`\n]+)`(?!`)", RegexOptions.Compiled);

    // 媒体标签正则
    private static readonly Regex AudioSrcFirstPattern = new(
        @"<audio[^>]*\ssrc=""([^""]*)""[^>]*(?:title=""([^""]*)"")?[^>]*>.*?</audio>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex AudioTitleFirstPattern = new(
        @"<audio[^>]*title=""([^""]*)""[^>]*\ssrc=""([^""]*)""[^>]*>.*?</audio>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex VideoSrcFirstPattern = new(
        @"<video[^>]*\ssrc=""([^""]*)""[^>]*(?:poster=""([^""]*)"")?[^>]*>.*?</video>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex VideoPosterFirstPattern = new(
        @"<video[^>]*poster=""([^""]*)""[^>]*\ssrc=""([^""]*)""[^>]*>.*?</video>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex DivOpenPattern = new(@"<div[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DivClosePattern = new(@"</div>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// 渲染 HTML 内容，返回处理后的 HTML 字符串
    /// - Markdown 代码块中的 HTML 标签会被转义
    /// - &lt;audio&gt; 和 &lt;video&gt; 转为可点击链接
    /// - &lt;div&gt; 包裹去除（保留内容）
    /// </summary>
    public static string Render(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        try
        {
            // 0. 转义 Markdown 代码块中的字面 HTML 标签
            var escapedHtml = EscapeMarkdownCode(html);

            // 1. 预处理：将 <audio> 和 <video> 转为 <a> 链接
            var processedHtml = PreprocessMediaTags(escapedHtml);

            return processedHtml;
        }
        catch
        {
            // 渲染失败时降级为纯文本显示
            try
            {
                return HtmlTagRegex.Replace(html, "").Trim();
            }
            catch
            {
                return "内容渲染失败";
            }
        }
    }

    /// <summary>
    /// 渲染 HTML 并转为纯文本（用于不支持 HTML 渲染的控件）
    /// </summary>
    public static string RenderToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        try
        {
            var escapedHtml = EscapeMarkdownCode(html);
            var processedHtml = PreprocessMediaTags(escapedHtml);

            // 将 <br> 转为换行
            var text = Regex.Replace(processedHtml, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</p\s*>", "\n", RegexOptions.IgnoreCase);
            // 去除所有 HTML 标签
            text = HtmlTagRegex.Replace(text, "");
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
                return "内容渲染失败";
            }
        }
    }

    /// <summary>
    /// 转义 Markdown 代码块中的 HTML 标签
    /// 处理两种 Markdown 代码语法：
    /// 1. 三反引号代码块：```...```
    /// 2. 单反引号代码段：`...`
    /// </summary>
    public static string EscapeMarkdownCode(string html)
    {
        var result = html;

        // 1. 先处理三反引号代码块
        result = TripleBacktickPattern.Replace(result, m =>
        {
            var codeContent = m.Groups[1].Value;
            var escaped = codeContent
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
            return $"```\n{escaped}\n```";
        });

        // 2. 再处理单反引号代码段
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
    /// 预处理 HTML：将 audio 和 video 标签转为可点击的 a 链接
    /// </summary>
    public static string PreprocessMediaTags(string html)
    {
        var result = html;

        // <audio src="url" title="xxx">...</audio> → <a href="url">[音频] xxx</a><br>
        result = AudioSrcFirstPattern.Replace(result, m =>
        {
            var src = m.Groups[1].Value;
            var title = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : "点击播放音频";
            return $@"<a href=""{src}"">[音频] {title}</a><br>";
        });

        // 也处理 title 在 src 前面的情况
        result = AudioTitleFirstPattern.Replace(result, m =>
        {
            var title = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : "点击播放音频";
            var src = m.Groups[2].Value;
            return $@"<a href=""{src}"">[音频] {title}</a><br>";
        });

        // <video src="url" poster="xxx">...</video> → <a href="url">[视频] xxx</a><br>
        result = VideoSrcFirstPattern.Replace(result, m =>
        {
            var src = m.Groups[1].Value;
            var poster = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : "点击播放视频";
            return $@"<a href=""{src}"">[视频] {poster}</a><br>";
        });

        // 处理 poster 在 src 前面的情况
        result = VideoPosterFirstPattern.Replace(result, m =>
        {
            var poster = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : "点击播放视频";
            var src = m.Groups[2].Value;
            return $@"<a href=""{src}"">[视频] {poster}</a><br>";
        });

        // 去掉 <div> 包裹（保留内容）
        result = DivOpenPattern.Replace(result, "");
        result = DivClosePattern.Replace(result, "");

        return result;
    }
}
