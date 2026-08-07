using System.Net;
using System.Text.RegularExpressions;

namespace ForumCenter.Utilities;






public static class HtmlRenderer
{
    
    private static readonly Regex TripleBacktickPattern = new(@"```[^\n]*\n([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex CodeSpanPattern = new(@"(?<!`)`([^`\n]+)`(?!`)", RegexOptions.Compiled);

    
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

    
    
    
    
    
    
    public static string Render(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        try
        {
            
            var escapedHtml = EscapeMarkdownCode(html);

            
            var processedHtml = PreprocessMediaTags(escapedHtml);

            return processedHtml;
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

    
    
    
    public static string RenderToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        try
        {
            var escapedHtml = EscapeMarkdownCode(html);
            var processedHtml = PreprocessMediaTags(escapedHtml);

            
            var text = Regex.Replace(processedHtml, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</p\s*>", "\n", RegexOptions.IgnoreCase);
            
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

    
    
    
    public static string PreprocessMediaTags(string html)
    {
        var result = html;

        
        result = AudioSrcFirstPattern.Replace(result, m =>
        {
            var src = m.Groups[1].Value;
            var title = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : "点击播放音频";
            return $@"<a href=""{src}"">[音频] {title}</a><br>";
        });

        
        result = AudioTitleFirstPattern.Replace(result, m =>
        {
            var title = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : "点击播放音频";
            var src = m.Groups[2].Value;
            return $@"<a href=""{src}"">[音频] {title}</a><br>";
        });

        
        result = VideoSrcFirstPattern.Replace(result, m =>
        {
            var src = m.Groups[1].Value;
            var poster = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : "点击播放视频";
            return $@"<a href=""{src}"">[视频] {poster}</a><br>";
        });

        
        result = VideoPosterFirstPattern.Replace(result, m =>
        {
            var poster = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : "点击播放视频";
            var src = m.Groups[2].Value;
            return $@"<a href=""{src}"">[视频] {poster}</a><br>";
        });

        
        result = DivOpenPattern.Replace(result, "");
        result = DivClosePattern.Replace(result, "");

        return result;
    }
}
