using ForumCenter.Models;
using ForumCenter.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ForumCenter.Services;


public interface IApiService
{
    CommunityType CommunityType { get; }
    string DisplayName { get; }
    Task<bool> LoginAsync(string username, string password);
    Task<List<Post>> GetPostsAsync(int page, string tab);
    Task<PostDetail> GetPostDetailAsync(long postId);
    Task<bool> CreatePostAsync(string title, string content, string tagOrSection);
    Task<bool> SendCommentAsync(long topicId, string content, long? commentId = null);
    Task<bool> VoteTopicAsync(long topicId);
    Task<bool> EditPostAsync(long postId, string title, string content);
    Task<bool> DeletePostAsync(long postId);
    Task<List<Tag>> GetTagsAsync();
    Task<List<Section>> GetSectionsAsync();
    Task<List<Post>> GetPostsByUserAsync(int userId, int page);
    Task<User?> GetCurrentUserInfoAsync();
    Task<string?> UploadImageAsync(byte[] imageData, string fileName);
    bool IsLoggedIn();
    string? GetToken();
    bool IsAdmin();

    
    Task<List<Forum>> GetForumsAsync();

    
    Task<bool> ReplyAsync(long topicId, string content, string? formHash = null);

    
    Task<string?> GetFormHashAsync();

    
    Task<bool> CreateThreadAsync(string fid, string title, string content, string? formHash = null);

    
    void ClearCache();
}


public static class ApiServiceFactory
{
    private static IApiService? _current;

    public static IApiService GetService(CommunityType type)
    {
        return type switch
        {
            CommunityType.Tatans => new TatansApiService(),
            CommunityType.BangMang => new BangMangApiService(),
            CommunityType.ZhengDu => new ZhengDuApiService(),
            CommunityType.AiMang => new AiMangApiService(),
            _ => new TatansApiService()
        };
    }

    public static IApiService Current => _current ??= new TatansApiService();

    public static void SetCurrent(CommunityType type)
    {
        _current = GetService(type);
    }
}


public class TatansApiService : IApiService
{
    private const string BaseUrl = "https://bbs.tatans.cn";
    private const int SuccessCode = 200;
    private static readonly HttpClient _client;

    static TatansApiService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _client.DefaultRequestHeaders.Add("User-Agent", "ForumCenter/1.0");
    }

    public CommunityType CommunityType => CommunityType.Tatans;
    public string DisplayName => "天坦社区";

    private string? _token;
    private bool _isAdmin;

    public string? GetToken() => _token;
    public bool IsLoggedIn() => !string.IsNullOrEmpty(_token);
    public bool IsAdmin() => _isAdmin;

    private void SetAuth()
    {
        _client.DefaultRequestHeaders.Remove("token");
        _client.DefaultRequestHeaders.Remove("Cookie");
        if (!string.IsNullOrEmpty(_token))
        {
            _client.DefaultRequestHeaders.Add("token", _token);
            _client.DefaultRequestHeaders.Add("Cookie", $"token={_token}");
        }
    }

    public async Task<bool> LoginAsync(string phone, string password)
    {
        var md5Pass = Md5(password);
        var json = $"{{\"phone\":\"{phone}\",\"password\":\"{md5Pass}\"}}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _client.PostAsync($"{BaseUrl}/api/login", content);
        var body = await resp.Content.ReadAsStringAsync();

        
        try
        {
            var root = JObject.Parse(body);
            var code = root["code"]?.Value<int>() ?? -1;
            if (code == SuccessCode)
            {
                var detail = root["detail"] as JObject;
                if (detail == null)
                    throw new Exception("登录返回数据异常");
                var token = detail["token"]?.ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    _token = token;
                    _isAdmin = false;
                    return true;
                }
                throw new Exception("登录返回数据异常");
            }
            else
            {
                var desc = root["description"]?.ToString() ?? "";
                var msg = desc switch
                {
                    _ when desc.Contains("密码") || desc.Contains("手机号") => "手机号或密码错误",
                    _ when desc.Contains("不存在") => "账号不存在",
                    _ when desc.Contains("频繁") => "操作太频繁",
                    _ when string.IsNullOrEmpty(desc) => "登录失败",
                    _ => desc
                };
                throw new Exception(msg);
            }
        }
        catch (Exception ex) when (ex.Message.Contains("登录"))
        {
            throw; 
        }
        catch (Exception ex)
        {
            throw new Exception($"登录数据解析异常: {ex.Message}");
        }
    }

    public async Task<List<Post>> GetPostsAsync(int page, string tab)
    {
        SetAuth();
        var tagId = "";
        var url = $"{BaseUrl}/api/index?pageNo={page}&tab={tab}&tagId={tagId}";
        var cacheKey = $"posts_{tab}_{page}_{tagId}";

        
        if (page == 1)
        {
            var cached = MemoryCache.Get(cacheKey);
            if (cached != null)
            {
                try
                {
                    var cachedResult = JsonConvert.DeserializeObject<ApiResponse<Page<Post>>>(cached);
                    if (cachedResult?.IsSuccess == true && cachedResult.Detail?.Records != null)
                    {
                        
                        _ = RefreshCacheAsync(url, cacheKey);
                        return cachedResult.Detail.Records;
                    }
                }
                catch { }
            }
        }

        
        var body = await GetWithRetryAsync(url);
        MemoryCache.Put(cacheKey, body);
        var result = JsonConvert.DeserializeObject<ApiResponse<Page<Post>>>(body);

        return result?.IsSuccess == true ? result.Detail?.Records ?? new() : new();
    }

    
    private async Task RefreshCacheAsync(string url, string cacheKey)
    {
        try
        {
            SetAuth();
            var resp = await _client.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            MemoryCache.Put(cacheKey, body);
        }
        catch { }
    }

    
    private async Task<string> GetWithRetryAsync(string url, int maxRetry = 2)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt <= maxRetry; attempt++)
        {
            try
            {
                var resp = await _client.GetAsync(url);
                
                if ((int)resp.StatusCode >= 500 && (int)resp.StatusCode <= 599 && attempt < maxRetry)
                {
                    await Task.Delay(500 * (attempt + 1));
                    continue;
                }
                return await resp.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                lastException = e;
                if (attempt < maxRetry)
                    await Task.Delay(500 * (attempt + 1));
                else
                    throw;
            }
        }
        throw lastException ?? new Exception("请求失败");
    }

    public async Task<PostDetail> GetPostDetailAsync(long postId)
    {
        SetAuth();
        var url = $"{BaseUrl}/api/topic/{postId}";
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<PostDetail>>(body);

        return result?.Detail ?? new PostDetail();
    }

    public async Task<bool> CreatePostAsync(string title, string content, string tag)
    {
        SetAuth();
        var json = $"{{\"title\":\"{EscapeJson(title)}\",\"content\":\"{EscapeJson(content)}\",\"tag\":\"{EscapeJson(tag)}\"}}";
        var resp = await _client.PostAsync($"{BaseUrl}/api/topic",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
        return result?.Code == SuccessCode;
    }

    public async Task<bool> SendCommentAsync(long topicId, string content, long? commentId = null)
    {
        SetAuth();
        var cid = commentId?.ToString() ?? "null";
        var json = $"{{\"topicId\":{topicId},\"content\":\"{EscapeJson(content)}\",\"commentId\":{cid}}}";
        var resp = await _client.PostAsync($"{BaseUrl}/api/comment",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
        return result?.Code == SuccessCode;
    }

    public async Task<bool> VoteTopicAsync(long topicId)
    {
        SetAuth();
        var resp = await _client.GetAsync($"{BaseUrl}/api/topic/{topicId}/vote");
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
        return result?.Code == SuccessCode;
    }

    public async Task<bool> EditPostAsync(long postId, string title, string content)
    {
        SetAuth();
        var json = $"{{\"id\":{postId},\"title\":\"{EscapeJson(title)}\",\"content\":\"{EscapeJson(content)}\"}}";
        var resp = await _client.PutAsync($"{BaseUrl}/api/topic",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
        return result?.Code == SuccessCode;
    }

    public async Task<bool> DeletePostAsync(long postId)
    {
        SetAuth();
        var resp = await _client.DeleteAsync($"{BaseUrl}/api/topic/{postId}");
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<object>>(body);
        return result?.Code == SuccessCode;
    }

    public async Task<List<Tag>> GetTagsAsync()
    {
        SetAuth();
        var resp = await _client.GetAsync($"{BaseUrl}/api/tags");
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<Page<Tag>>>(body);
        return result?.Detail?.Records ?? new();
    }

    public Task<List<Section>> GetSectionsAsync() => Task.FromResult(new List<Section>());

    public async Task<List<Post>> GetPostsByUserAsync(int userId, int page)
    {
        SetAuth();
        var url = $"{BaseUrl}/api/index?userId={userId}&pageNo={page}";
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<Page<Post>>>(body);
        return result?.Detail?.Records ?? new();
    }

    public async Task<User?> GetCurrentUserInfoAsync()
    {
        SetAuth();
        var resp = await _client.GetAsync($"{BaseUrl}/api/user/info");
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse<User>>(body);
        if (result?.Detail != null)
        {
            _isAdmin = result.Detail.Id <= 10;
            result.Detail.IsAdmin = _isAdmin;
        }
        return result?.Detail;
    }

    public async Task<string?> UploadImageAsync(byte[] imageData, string fileName)
    {
        SetAuth();
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageData);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", fileName);

        var resp = await _client.PostAsync($"{BaseUrl}/api/file/upload", form);

        
        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new Exception("服务器暂不支持图片上传");

        var body = await resp.Content.ReadAsStringAsync();

        
        try
        {
            var root = JObject.Parse(body);
            var code = root["code"]?.Value<int>() ?? -1;
            if (code == SuccessCode)
            {
                var detail = root["detail"];
                if (detail is JObject detailObj)
                {
                    var url = detailObj["url"]?.ToString()
                              ?? detailObj["path"]?.ToString()
                              ?? "";
                    return string.IsNullOrEmpty(url) ? "ok" : url;
                }
                if (detail != null)
                    return detail.ToString();
            }
            else
            {
                var desc = root["description"]?.ToString() ?? "上传失败";
                throw new Exception(desc);
            }
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw new Exception($"数据解析异常: {ex.Message}");
        }
        return null;
    }

    
    public Task<List<Forum>> GetForumsAsync() => Task.FromResult(new List<Forum>());

    
    public Task<bool> ReplyAsync(long topicId, string content, string? formHash = null) => Task.FromResult(false);

    
    public Task<string?> GetFormHashAsync() => Task.FromResult<string?>(null);

    
    public Task<bool> CreateThreadAsync(string fid, string title, string content, string? formHash = null) => Task.FromResult(false);

    
    public void ClearCache() => MemoryCache.Clear();

    private static string Md5(string input)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}


public class BangMangApiService : IApiService
{
    private const string BaseUrl = "http://bbs.abm365.cn";
    private static readonly HttpClient _client;

    static BangMangApiService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("User-Agent", "ForumCenter/1.0");
    }

    public CommunityType CommunityType => CommunityType.BangMang;
    public string DisplayName => "帮盲社区";

    private string? _token;

    public string? GetToken() => _token;
    public bool IsLoggedIn() => !string.IsNullOrEmpty(_token);
    public bool IsAdmin() => false;

    private void SetAuth()
    {
        _client.DefaultRequestHeaders.Remove("token");
        if (!string.IsNullOrEmpty(_token))
            _client.DefaultRequestHeaders.Add("token", _token);
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var json = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";
        var resp = await _client.PostAsync($"{BaseUrl}/api/user/login",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<BangMangResponse<string>>(body);
        if (result?.IsSuccess == true && result.Data != null)
        {
            _token = result.Data;
            return true;
        }
        return false;
    }

    public async Task<List<Post>> GetPostsAsync(int page, string tab)
    {
        SetAuth();
        var mappedTab = tab switch
        {
            "hot" => "hot",
            "good" => "essence",
            _ => "new"
        };
        var url = $"{BaseUrl}/api/post/{mappedTab}?page={page}";
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<BangMangResponse<BangMangPage<BangMangPost>>>(body);

        return result?.Data?.List?.Select(p => p.ToPost()).ToList() ?? new();
    }

    public async Task<PostDetail> GetPostDetailAsync(long postId)
    {
        SetAuth();
        var resp = await _client.GetAsync($"{BaseUrl}/api/post/{postId}");
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<BangMangResponse<BangMangPostDetail>>(body);
        return result?.Data?.ToPostDetail() ?? new PostDetail();
    }

    public Task<bool> CreatePostAsync(string title, string content, string tagOrSection)
        => Task.FromResult(false);

    public Task<bool> SendCommentAsync(long topicId, string content, long? commentId = null)
        => Task.FromResult(false);

    public Task<bool> VoteTopicAsync(long topicId) => Task.FromResult(false);

    public Task<bool> EditPostAsync(long postId, string title, string content)
        => Task.FromResult(false);

    public Task<bool> DeletePostAsync(long postId) => Task.FromResult(false);

    public Task<List<Tag>> GetTagsAsync() => Task.FromResult(new List<Tag>());

    public async Task<List<Section>> GetSectionsAsync()
    {
        SetAuth();
        var resp = await _client.GetAsync($"{BaseUrl}/api/section");
        var body = await resp.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<BangMangResponse<List<Section>>>(body);
        return result?.Data ?? new();
    }

    public Task<List<Post>> GetPostsByUserAsync(int userId, int page)
        => Task.FromResult(new List<Post>());

    public Task<User?> GetCurrentUserInfoAsync() => Task.FromResult<User?>(null);

    public Task<string?> UploadImageAsync(byte[] imageData, string fileName)
        => Task.FromResult<string?>(null);

    
    public Task<List<Forum>> GetForumsAsync() => Task.FromResult(new List<Forum>());

    
    public Task<bool> ReplyAsync(long topicId, string content, string? formHash = null) => Task.FromResult(false);

    
    public Task<string?> GetFormHashAsync() => Task.FromResult<string?>(null);

    
    public Task<bool> CreateThreadAsync(string fid, string title, string content, string? formHash = null) => Task.FromResult(false);

    
    public void ClearCache() { }
}


public class AiMangApiService : IApiService
{
    private const string BaseUrl = "https://www.aimang.net";
    private const string ApiPath = "/api/mobile/index.php";
    private static readonly HttpClient _client;
    private static readonly CookieContainer _cookies;

    static AiMangApiService()
    {
        _cookies = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10)");

        
        try
        {
            var saved = new PreferencesService().GetAiMangCookie();
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var pair in saved.Split("; ", StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        try
                        {
                            _cookies.Add(new Uri(BaseUrl), new Cookie(parts[0].Trim(), parts[1].Trim()));
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    public CommunityType CommunityType => CommunityType.AiMang;
    public string DisplayName => "爱盲论坛";

    private string? _token;

    public string? GetToken() => _token;
    public bool IsLoggedIn() => !string.IsNullOrEmpty(_token);
    public bool IsAdmin() => false;

    
    private static string BuildUrl(string module, Dictionary<string, string>? extra = null)
    {
        var param = $"module={module}&version=4";
        if (extra != null)
            foreach (var kv in extra)
                param += $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}";
        return $"{BaseUrl}{ApiPath}?{param}";
    }

    
    private static JObject? ExtractVariables(string body)
    {
        try
        {
            var root = JObject.Parse(body);
            return root["Variables"] as JObject;
        }
        catch { return null; }
    }

    
    private static void PersistCookies()
    {
        try
        {
            var cookies = _cookies.GetCookies(new Uri(BaseUrl));
            var str = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
            new PreferencesService().SetAiMangCookie(str);
        }
        catch { }
    }

    
    private static bool ParseDiscuzWriteResponse(string body)
    {
        try
        {
            var root = JObject.Parse(body);
            var msg = root["Message"] as JObject;
            if (msg != null)
            {
                var msgVal = msg["messageval"]?.ToString() ?? "";
                if (msgVal.Contains("succeed") || msgVal.Contains("success")) return true;
                return false;
            }
            
            return root["Variables"] != null;
        }
        catch { return false; }
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var param = new Dictionary<string, string>
        {
            ["module"] = "login",
            ["version"] = "4",
            ["username"] = username,
            ["password"] = password
        };
        var content = new FormUrlEncodedContent(param);
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", content);
        var body = await resp.Content.ReadAsStringAsync();
        var variables = ExtractVariables(body);
        if (variables == null) return false;

        var auth = variables["auth"]?.ToString();
        var memberUid = variables["member_uid"]?.ToString() ?? "0";
        if (!string.IsNullOrEmpty(auth) && memberUid != "0")
        {
            _token = auth;
            PersistCookies();
            return true;
        }
        return false;
    }

    public async Task<List<Post>> GetPostsAsync(int page, string tab)
    {
        
        var fid = tab switch
        {
            "all" => "43",
            "new" => "46",
            "hot" => "39",
            "good" => "42",
            _ => "43"
        };
        var url = BuildUrl("forumdisplay", new() { ["fid"] = fid, ["page"] = page.ToString() });
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var variables = ExtractVariables(body);
        if (variables == null) return new();

        var threadList = variables["forum_threadlist"] as JArray;
        if (threadList == null) return new();

        var threads = threadList
            .Select(t => t.ToObject<AiMangThread>())
            .Where(t => t != null && !string.IsNullOrEmpty(t.Tid))
            .Select(t => t!)
            .ToList();

        return threads.Select(t => new Post
        {
            Id = long.TryParse(t.Tid, out var tid) ? tid : 0,
            Title = t.Subject,
            Username = t.Author,
            UserId = t.AuthorIdInt,
            CommentCount = t.ReplyCount,
            View = t.ViewCount,
            Top = t.IsTop,
            Good = t.IsDigest,
            DisplayTime = t.Dateline,
            Avatar = t.Avatar
        }).ToList();
    }

    public async Task<PostDetail> GetPostDetailAsync(long postId)
    {
        var url = BuildUrl("viewthread", new() { ["tid"] = postId.ToString(), ["page"] = "1" });
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var variables = ExtractVariables(body);
        if (variables == null) return new PostDetail();

        var thread = (variables["thread"] as JObject)?.ToObject<AiMangThread>();
        var postListArr = variables["postlist"] as JArray;
        var postList = postListArr?.Select(p => p.ToObject<AiMangPost>())
            .Where(p => p != null)
            .Select(p => p!)
            .ToList() ?? new();

        var firstPost = postList.FirstOrDefault(p => p.IsMainPost) ?? postList.FirstOrDefault();

        return new PostDetail
        {
            Topic = new Topic
            {
                Id = int.TryParse(thread?.Tid, out var tid) ? tid : 0,
                Title = thread?.Subject,
                Content = firstPost?.Message,
                UserId = firstPost?.AuthorIdInt,
                CommentCount = thread?.ReplyCount,
                View = thread?.ViewCount,
                Top = thread?.IsTop,
                Good = thread?.IsDigest,
                InTime = thread?.Dateline
            },
            TopicUser = new User { Username = firstPost?.Author, Avatar = firstPost?.Avatar },
            Comments = postList.Where(p => !p.IsMainPost).Select(p => new Comment
            {
                Id = p.PidLong,
                Username = p.Author,
                Content = p.Message,
                UserId = p.AuthorIdInt,
                InTime = p.DbDatelineLong,
                Avatar = p.Avatar
            }).ToList()
        };
    }

    
    public async Task<bool> SendCommentAsync(long topicId, string content, long? commentId = null)
    {
        var formHash = await GetFormHashAsync();
        if (string.IsNullOrEmpty(formHash)) return false;

        var param = new Dictionary<string, string>
        {
            ["module"] = "sendreply",
            ["version"] = "4",
            ["tid"] = topicId.ToString(),
            ["message"] = content,
            ["formhash"] = formHash!,
            ["handlekey"] = "sendreply",
            ["replysubmit"] = "true"
        };
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", new FormUrlEncodedContent(param));
        var body = await resp.Content.ReadAsStringAsync();
        return ParseDiscuzWriteResponse(body);
    }

    
    public async Task<bool> CreatePostAsync(string title, string content, string tagOrSection)
    {
        if (string.IsNullOrEmpty(tagOrSection)) return false;
        return await CreateThreadAsync(tagOrSection, title, content);
    }

    public Task<bool> VoteTopicAsync(long topicId) => Task.FromResult(false);
    public Task<bool> EditPostAsync(long postId, string title, string content) => Task.FromResult(false);
    public Task<bool> DeletePostAsync(long postId) => Task.FromResult(false);
    public Task<List<Tag>> GetTagsAsync() => Task.FromResult(new List<Tag>());
    public Task<List<Section>> GetSectionsAsync() => Task.FromResult(new List<Section>());
    public Task<List<Post>> GetPostsByUserAsync(int userId, int page) => Task.FromResult(new List<Post>());
    public Task<User?> GetCurrentUserInfoAsync() => Task.FromResult<User?>(null);
    public Task<string?> UploadImageAsync(byte[] imageData, string fileName) => Task.FromResult<string?>(null);

    
    public async Task<List<Forum>> GetForumsAsync()
    {
        var url = BuildUrl("forumindex");
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var variables = ExtractVariables(body);
        if (variables == null) return new();

        var forumList = variables["forumlist"] as JArray;
        if (forumList == null) return new();

        return forumList.Select(f => f.ToObject<AiMangForum>())
            .Where(f => f != null && !string.IsNullOrEmpty(f!.Fid))
            .Select(f => new Forum { Id = f!.Fid, Name = f.Name })
            .ToList();
    }

    
    public async Task<string?> GetFormHashAsync()
    {
        var url = BuildUrl("forumindex");
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        var variables = ExtractVariables(body);
        return variables?["formhash"]?.ToString();
    }

    
    public async Task<bool> ReplyAsync(long topicId, string content, string? formHash = null)
    {
        formHash ??= await GetFormHashAsync();
        if (string.IsNullOrEmpty(formHash)) return false;

        var param = new Dictionary<string, string>
        {
            ["module"] = "sendreply",
            ["version"] = "4",
            ["tid"] = topicId.ToString(),
            ["message"] = content,
            ["formhash"] = formHash!,
            ["handlekey"] = "sendreply",
            ["replysubmit"] = "true"
        };
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", new FormUrlEncodedContent(param));
        var body = await resp.Content.ReadAsStringAsync();
        return ParseDiscuzWriteResponse(body);
    }

    
    public async Task<bool> CreateThreadAsync(string fid, string title, string content, string? formHash = null)
    {
        formHash ??= await GetFormHashAsync();
        if (string.IsNullOrEmpty(formHash)) return false;

        var param = new Dictionary<string, string>
        {
            ["module"] = "newthread",
            ["version"] = "4",
            ["fid"] = fid,
            ["subject"] = title,
            ["message"] = content,
            ["formhash"] = formHash!,
            ["handlekey"] = "newthread",
            ["topicsubmit"] = "true"
        };
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", new FormUrlEncodedContent(param));
        var body = await resp.Content.ReadAsStringAsync();
        return ParseDiscuzWriteResponse(body);
    }

    
    public void ClearCache() { }
}


public class ZhengDuApiService : IApiService
{
    private const string BaseUrl = "https://www.zhengdu.cc";
    private const string ApiPath = "/api/mobile/index.php";
    private static readonly HttpClient _client;
    private System.Net.CookieContainer? _cookies;

    static ZhengDuApiService()
    {
        var handler = new HttpClientHandler();
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10)");
    }

    public CommunityType CommunityType => CommunityType.ZhengDu;
    public string DisplayName => "争渡论坛";

    private string? _token;
    private bool _isAdmin;

    public string? GetToken() => _token;
    public bool IsLoggedIn() => !string.IsNullOrEmpty(_token) || _cookies != null;
    public bool IsAdmin() => _isAdmin;

    private string BuildUrl(string module, Dictionary<string, string>? extra = null)
    {
        var param = $"module={module}&mobile=no&version=4";
        if (extra != null)
            foreach (var kv in extra)
                param += $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}";
        return $"{BaseUrl}{ApiPath}?{param}";
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var param = new Dictionary<string, string>
        {
            ["module"] = "login",
            ["mobile"] = "no",
            ["version"] = "4",
            ["username"] = username,
            ["password"] = password
        };
        var content = new FormUrlEncodedContent(param);
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", content);
        var body = await resp.Content.ReadAsStringAsync();

        
        try
        {
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            if (result != null && result.ContainsKey("cookiepre"))
            {
                _token = result["cookiepre"]?.ToString();
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task<List<Post>> GetPostsAsync(int page, string tab)
    {
        var fid = tab switch { "hot" => "2", "good" => "2", _ => "1" };
        var url = BuildUrl("forumdisplay", new() { ["page"] = page.ToString(), ["fid"] = fid });
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        try
        {
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            if (result?.ContainsKey("Variables") == true)
            {
                var vars = JsonConvert.DeserializeObject<Dictionary<string, object>>(result["Variables"].ToString()!);
                if (vars?.ContainsKey("forum_threadlist") == true)
                {
                    var threads = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(
                        vars["forum_threadlist"].ToString()!);
                    return threads?.Select(t => new Post
                    {
                        Id = long.TryParse(t.GetValueOrDefault("tid"), out var tid) ? tid : 0,
                        Title = t.GetValueOrDefault("subject", ""),
                        Username = t.GetValueOrDefault("author", ""),
                        CommentCount = int.TryParse(t.GetValueOrDefault("replies"), out var r) ? r : 0,
                        View = int.TryParse(t.GetValueOrDefault("views"), out var v) ? v : 0,
                        DisplayTime = t.GetValueOrDefault("dbdateline", ""),
                        Top = t.GetValueOrDefault("displayorder") != "0"
                    }).ToList() ?? new();
                }
            }
        }
        catch { }
        return new();
    }

    public async Task<PostDetail> GetPostDetailAsync(long postId)
    {
        var url = BuildUrl("viewthread", new() { ["tid"] = postId.ToString() });
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        try
        {
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            if (result?.ContainsKey("Variables") == true)
            {
                var vars = JsonConvert.DeserializeObject<Dictionary<string, object>>(result["Variables"].ToString()!);
                var postList = vars?.ContainsKey("postlist") == true
                    ? JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(vars["postlist"].ToString()!)
                    : null;

                var firstPost = postList?.FirstOrDefault();
                if (firstPost != null)
                {
                    return new PostDetail
                    {
                        Topic = new Topic
                        {
                            Id = int.TryParse(firstPost.GetValueOrDefault("tid"), out var tid) ? tid : 0,
                            Title = firstPost.GetValueOrDefault("subject"),
                            Content = firstPost.GetValueOrDefault("message"),
                            UserId = int.TryParse(firstPost.GetValueOrDefault("authorid"), out var uid) ? uid : 0,
                            CommentCount = postList.Count - 1
                        },
                        TopicUser = new User { Username = firstPost.GetValueOrDefault("author") },
                        Comments = postList.Skip(1).Select(p => new Comment
                        {
                            Id = long.TryParse(p.GetValueOrDefault("pid"), out var pid) ? pid : 0,
                            Username = p.GetValueOrDefault("author", ""),
                            Content = p.GetValueOrDefault("message", ""),
                            UserId = int.TryParse(p.GetValueOrDefault("authorid"), out var uid) ? uid : 0,
                            InTime = long.TryParse(p.GetValueOrDefault("dateline"), out var dl) ? dl : 0
                        }).ToList()
                    };
                }
            }
        }
        catch { }
        return new PostDetail();
    }

    public async Task<bool> CreatePostAsync(string title, string content, string fid)
    {
        var param = new Dictionary<string, string>
        {
            ["module"] = "sendreply",
            ["mobile"] = "no",
            ["version"] = "4",
            ["fid"] = fid,
            ["subject"] = title,
            ["message"] = content
        };
        var content2 = new FormUrlEncodedContent(param);
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", content2);
        var body = await resp.Content.ReadAsStringAsync();
        var (success, _) = ParseWriteResponse(body);
        return success;
    }

    public async Task<bool> SendCommentAsync(long topicId, string content, long? commentId = null)
    {
        var param = new Dictionary<string, string>
        {
            ["module"] = "sendreply",
            ["mobile"] = "no",
            ["version"] = "4",
            ["tid"] = topicId.ToString(),
            ["message"] = content
        };
        var content2 = new FormUrlEncodedContent(param);
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", content2);
        var body = await resp.Content.ReadAsStringAsync();
        var (success, _) = ParseWriteResponse(body);
        return success;
    }

    public Task<bool> VoteTopicAsync(long topicId) => Task.FromResult(false);

    public async Task<bool> EditPostAsync(long postId, string title, string content)
    {
        var param = new Dictionary<string, string>
        {
            ["module"] = "editpost",
            ["mobile"] = "no",
            ["version"] = "4",
            ["pid"] = postId.ToString(),
            ["subject"] = title,
            ["message"] = content
        };
        var content2 = new FormUrlEncodedContent(param);
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", content2);
        var body = await resp.Content.ReadAsStringAsync();
        var (success, _) = ParseWriteResponse(body);
        return success;
    }

    public async Task<bool> DeletePostAsync(long postId)
    {
        var param = new Dictionary<string, string>
        {
            ["module"] = "deletepost",
            ["mobile"] = "no",
            ["version"] = "4",
            ["pid"] = postId.ToString()
        };
        var content2 = new FormUrlEncodedContent(param);
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", content2);
        var body = await resp.Content.ReadAsStringAsync();
        var (success, _) = ParseWriteResponse(body);
        return success;
    }

    public Task<List<Tag>> GetTagsAsync() => Task.FromResult(new List<Tag>());
    public Task<List<Section>> GetSectionsAsync() => Task.FromResult(new List<Section>());
    public Task<List<Post>> GetPostsByUserAsync(int userId, int page) => Task.FromResult(new List<Post>());
    public Task<User?> GetCurrentUserInfoAsync() => Task.FromResult<User?>(null);
    public Task<string?> UploadImageAsync(byte[] imageData, string fileName) => Task.FromResult<string?>(null);

    
    public async Task<List<Forum>> GetForumsAsync()
    {
        var url = BuildUrl("forumindex");
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        try
        {
            var root = JObject.Parse(body);
            var variables = root["Variables"] as JObject;
            var forumList = variables?["forumlist"] as JArray;
            if (forumList == null) return new();
            return forumList.Select(f => f.ToObject<AiMangForum>())
                .Where(f => f != null && !string.IsNullOrEmpty(f!.Fid))
                .Select(f => new Forum { Id = f!.Fid, Name = f.Name })
                .ToList();
        }
        catch { return new(); }
    }

    
    public async Task<string?> GetFormHashAsync()
    {
        var url = BuildUrl("forumindex");
        var resp = await _client.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        try
        {
            var root = JObject.Parse(body);
            return (root["Variables"] as JObject)?["formhash"]?.ToString();
        }
        catch { return null; }
    }

    
    public async Task<bool> ReplyAsync(long topicId, string content, string? formHash = null)
    {
        formHash ??= await GetFormHashAsync();
        var param = new Dictionary<string, string>
        {
            ["module"] = "sendreply",
            ["mobile"] = "no",
            ["version"] = "4",
            ["tid"] = topicId.ToString(),
            ["message"] = content
        };
        if (!string.IsNullOrEmpty(formHash))
            param["formhash"] = formHash!;
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", new FormUrlEncodedContent(param));
        var body = await resp.Content.ReadAsStringAsync();
        var (success, _) = ParseWriteResponse(body);
        return success;
    }

    
    public async Task<bool> CreateThreadAsync(string fid, string title, string content, string? formHash = null)
    {
        formHash ??= await GetFormHashAsync();
        var param = new Dictionary<string, string>
        {
            ["module"] = "newthread",
            ["mobile"] = "no",
            ["version"] = "4",
            ["fid"] = fid,
            ["subject"] = title,
            ["message"] = content
        };
        if (!string.IsNullOrEmpty(formHash))
            param["formhash"] = formHash!;
        var resp = await _client.PostAsync($"{BaseUrl}{ApiPath}", new FormUrlEncodedContent(param));
        var body = await resp.Content.ReadAsStringAsync();
        var (success, _) = ParseWriteResponse(body);
        return success;
    }

    
    private static (bool success, string message) ParseWriteResponse(string rawBody)
    {
        var body = rawBody.Trim().TrimStart('\uFEFF');

        
        if (body.StartsWith('{') || body.StartsWith('['))
        {
            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                if (json != null)
                {
                    var status = json.GetValueOrDefault("status")?.ToString();
                    var msg = json.GetValueOrDefault("Message")?.ToString();
                    if (status == "1")
                        return (true, "操作成功");
                    return (false, msg ?? "操作失败");
                }
            }
            catch { }
        }

        
        if (body.Contains('<') || body.ToLower().Contains("<script"))
        {
            var alertMatch = System.Text.RegularExpressions.Regex.Match(
                body, @"alert\(\s*['""](.+?)['""]\s*\)");
            if (alertMatch.Success)
            {
                var alertContent = alertMatch.Groups[1].Value;
                if (alertContent.Contains("失败") || alertContent.Contains("错误") ||
                    alertContent.Contains("没有权限") || alertContent.Contains("无权"))
                    return (false, alertContent);
                return (true, alertContent);
            }
            return (true, "操作成功");
        }

        return (false, body.Length > 100 ? "操作失败" : body);
    }

    
    public void ClearCache() { }
}

public static class DictExtensions
{
    public static string GetValueOrDefault(this Dictionary<string, string> dict, string key, string def = "")
        => dict.TryGetValue(key, out var val) ? val : def;
}
