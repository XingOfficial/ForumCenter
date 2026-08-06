using ForumCenter.Models;
using Newtonsoft.Json;

namespace ForumCenter.Services;

/// <summary>本地设置管理</summary>
public class PreferencesService
{
    private const string KeyCommunity = "current_community";
    private const string KeyTatansToken = "tatans_token";
    private const string KeyBangMangToken = "bangmang_token";
    private const string KeyZhengDuToken = "zhengdu_token";
    private const string KeyAiMangAuth = "aimang_auth";
    private const string KeyAiMangCookie = "aimang_cookie";
    private const string KeyFontSize = "font_size";
    private const string KeyPostTail = "post_tail_enabled";
    private const string KeyPostTailContent = "post_tail_content";
    private const string KeyZhengDuCookie = "zhengdu_cookie";

    public CommunityType GetCurrentCommunity()
    {
        var val = Preferences.Get(KeyCommunity, "tatans");
        return val switch
        {
            "bangmang" => CommunityType.BangMang,
            "zhengdu" => CommunityType.ZhengDu,
            "aimang" => CommunityType.AiMang,
            _ => CommunityType.Tatans
        };
    }

    public void SetCurrentCommunity(CommunityType type)
    {
        var val = type switch
        {
            CommunityType.BangMang => "bangmang",
            CommunityType.ZhengDu => "zhengdu",
            CommunityType.AiMang => "aimang",
            _ => "tatans"
        };
        Preferences.Set(KeyCommunity, val);
    }

    public string GetCommunityDisplayName() => GetCurrentCommunity() switch
    {
        CommunityType.BangMang => "帮盲社区",
        CommunityType.ZhengDu => "争渡论坛",
        CommunityType.AiMang => "爱盲论坛",
        _ => "天坦社区"
    };

    public string? GetToken(CommunityType type) => type switch
    {
        CommunityType.BangMang => Preferences.Get(KeyBangMangToken, null),
        CommunityType.ZhengDu => Preferences.Get(KeyZhengDuToken, null),
        CommunityType.AiMang => Preferences.Get(KeyAiMangAuth, null),
        _ => Preferences.Get(KeyTatansToken, null)
    };

    public void SetToken(CommunityType type, string? token)
    {
        var key = type switch
        {
            CommunityType.BangMang => KeyBangMangToken,
            CommunityType.ZhengDu => KeyZhengDuToken,
            CommunityType.AiMang => KeyAiMangAuth,
            _ => KeyTatansToken
        };
        if (string.IsNullOrEmpty(token))
            Preferences.Remove(key);
        else
            Preferences.Set(key, token);
    }

    /// <summary>获取爱盲论坛持久化的 Cookie（用于 Discuz! 会话恢复）</summary>
    public string? GetAiMangCookie() => Preferences.Get(KeyAiMangCookie, null);

    /// <summary>持久化爱盲论坛 Cookie</summary>
    public void SetAiMangCookie(string? cookie)
    {
        if (string.IsNullOrEmpty(cookie))
            Preferences.Remove(KeyAiMangCookie);
        else
            Preferences.Set(KeyAiMangCookie, cookie);
    }

    /// <summary>获取争渡论坛持久化的 Cookie</summary>
    public string? GetZhengDuCookie() => Preferences.Get(KeyZhengDuCookie, null);

    /// <summary>持久化争渡论坛 Cookie</summary>
    public void SetZhengDuCookie(string? cookie)
    {
        if (string.IsNullOrEmpty(cookie))
            Preferences.Remove(KeyZhengDuCookie);
        else
            Preferences.Set(KeyZhengDuCookie, cookie);
    }

    public void LogoutCurrent()
    {
        var type = GetCurrentCommunity();
        SetToken(type, null);
        if (type == CommunityType.AiMang)
            SetAiMangCookie(null);
        if (type == CommunityType.ZhengDu)
            SetZhengDuCookie(null);
    }

    public bool IsLoggedIn() => !string.IsNullOrEmpty(GetToken(GetCurrentCommunity()));

    public int GetFontSize() => Preferences.Get(KeyFontSize, 16);
    public void SetFontSize(int size) => Preferences.Set(KeyFontSize, size);

    public bool IsPostTailEnabled() => Preferences.Get(KeyPostTail, true);
    public void SetPostTailEnabled(bool enabled) => Preferences.Set(KeyPostTail, enabled);

    public string GetPostTailContent() => Preferences.Get(KeyPostTailContent,
        "来自<a href=\"https://web.wangru.net/@xing/forumcenterwebsite/\">论坛中心</a>");
    public void SetPostTailContent(string content) => Preferences.Set(KeyPostTailContent, content);
}

/// <summary>GitHub Releases 更新检查服务</summary>
public class GitHubApiService
{
    private const string Repo = "XingOfficial/ForumCenter";
    private const string CurrentVersion = "v1.02-beta-20";
    private static readonly HttpClient _client;

    static GitHubApiService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("User-Agent", "ForumCenter");
        _client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    /// <summary>检查最新版本</summary>
    public async Task<GitHubRelease> CheckUpdateAsync()
    {
        var url = $"https://api.github.com/repos/{Repo}/releases/latest";
        var resp = await _client.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        var release = JsonConvert.DeserializeObject<GitHubRelease>(body)
            ?? throw new Exception("无法解析版本信息");

        release.HasUpdate = IsNewerVersion(release.TagName ?? "", CurrentVersion);

        if (release.Assets != null)
        {
            var apk = release.Assets.FirstOrDefault(a => a.Name?.EndsWith(".apk") == true);
            release.DownloadUrl = apk?.BrowserDownloadUrl;
        }

        return release;
    }

    /// <summary>获取所有版本</summary>
    public async Task<List<GitHubRelease>> GetReleasesAsync()
    {
        var url = $"https://api.github.com/repos/{Repo}/releases";
        var resp = await _client.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<List<GitHubRelease>>(body) ?? new();
    }

    /// <summary>比较版本号</summary>
    private static bool IsNewerVersion(string remote, string current)
    {
        if (string.IsNullOrEmpty(remote)) return false;
        if (remote == current) return false;
        return string.Compare(remote, current, StringComparison.Ordinal) > 0;
    }
}
