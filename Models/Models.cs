using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace ForumCenter.Models;

/// <summary>社区类型</summary>
public enum CommunityType
{
    Tatans,
    BangMang,
    ZhengDu,
    AiMang
}

/// <summary>帖子列表项（通用模型）</summary>
public class Post
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Username { get; set; } = "";
    public int UserId { get; set; }
    public string? Avatar { get; set; }
    public int CommentCount { get; set; }
    public int View { get; set; }
    public bool Top { get; set; }
    public bool Good { get; set; }
    public string? Content { get; set; }
    public long InTime { get; set; }
    public string? DisplayTime { get; set; }
    public string? SectionName { get; set; }
}

/// <summary>帖子详情聚合</summary>
public class PostDetail
{
    public Topic? Topic { get; set; }
    public User? TopicUser { get; set; }
    public List<Comment>? Comments { get; set; }
}

/// <summary>主题详情</summary>
public class Topic
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int? UserId { get; set; }
    public int? CommentCount { get; set; }
    public int? CollectCount { get; set; }
    public int? View { get; set; }
    public bool? Top { get; set; }
    public bool? Good { get; set; }
    public string? UpIds { get; set; }
    public string? InTime { get; set; }
    public string? LastCommentTime { get; set; }
}

/// <summary>评论</summary>
public class Comment
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public long TopicId { get; set; }
    public string Username { get; set; } = "";
    public string Content { get; set; } = "";
    public long InTime { get; set; }
    public string? Avatar { get; set; }
    public long? CommentId { get; set; }
}

/// <summary>用户信息</summary>
public class User
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public int? Score { get; set; }
    public int? TopicCount { get; set; }
    public int? CommentCount { get; set; }
    public int? UpvoteCount { get; set; }
    public bool IsAdmin { get; set; }
}

/// <summary>标签</summary>
public class Tag
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>板块</summary>
public class Section
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>通用API响应（天坦社区）</summary>
public class ApiResponse<T>
{
    [JsonProperty("code")]
    public int Code { get; set; }
    [JsonProperty("description")]
    public string? Description { get; set; }
    [JsonProperty("detail")]
    public T? Detail { get; set; }

    public bool IsSuccess => Code == 200 && Detail != null;
}

/// <summary>通用分页（天坦社区）</summary>
public class Page<T>
{
    [JsonProperty("records")]
    public List<T>? Records { get; set; }
    [JsonProperty("total")]
    public int Total { get; set; }
    [JsonProperty("size")]
    public int Size { get; set; }
    [JsonProperty("current")]
    public int Current { get; set; }
    [JsonProperty("pages")]
    public int Pages { get; set; }
}

/// <summary>帮盲社区API响应</summary>
public class BangMangResponse<T>
{
    [JsonProperty("code")]
    public int Code { get; set; }
    [JsonProperty("errMsg")]
    public string? ErrMsg { get; set; }
    [JsonProperty("data")]
    public T? Data { get; set; }

    public bool IsSuccess => Code == 0 && Data != null;
}

/// <summary>帮盲社区分页</summary>
public class BangMangPage<T>
{
    [JsonProperty("pageNum")]
    public int PageNum { get; set; }
    [JsonProperty("pageSize")]
    public int PageSize { get; set; }
    [JsonProperty("pages")]
    public int Pages { get; set; }
    [JsonProperty("total")]
    public int Total { get; set; }
    [JsonProperty("list")]
    public List<T>? List { get; set; }
}

/// <summary>帮盲社区帖子</summary>
public class BangMangPost
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("userName")] public string? UserName { get; set; }
    [JsonProperty("replyCount")] public int? ReplyCount { get; set; }
    [JsonProperty("viewCount")] public int? ViewCount { get; set; }
    [JsonProperty("likeCount")] public int? LikeCount { get; set; }
    [JsonProperty("createTime")] public string? CreateTime { get; set; }
    [JsonProperty("createTimeCn")] public string? CreateTimeCn { get; set; }
    [JsonProperty("sectionId")] public int? SectionId { get; set; }
    [JsonProperty("sectionName")] public string? SectionName { get; set; }

    [JsonProperty("essence")] private object? EssenceRaw { get; set; }
    [JsonProperty("top")] private object? TopRaw { get; set; }

    public bool IsEssence => ParseBool(EssenceRaw);
    public bool IsTop => ParseBool(TopRaw);

    private static bool ParseBool(object? val) => val switch
    {
        bool b => b,
        int i => i == 1,
        string s => s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public Post ToPost() => new()
    {
        Id = Id,
        Title = Title ?? "",
        Username = UserName ?? "",
        CommentCount = ReplyCount ?? 0,
        View = ViewCount ?? 0,
        Top = IsTop,
        Good = IsEssence,
        DisplayTime = CreateTimeCn ?? CreateTime,
        SectionName = SectionName
    };
}

/// <summary>帮盲社区帖子详情</summary>
public class BangMangPostDetail
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("body")] public string? Body { get; set; }
    [JsonProperty("richBody")] public string? RichBody { get; set; }
    [JsonProperty("userName")] public string? UserName { get; set; }
    [JsonProperty("userId")] public int? UserId { get; set; }
    [JsonProperty("avatar")] public string? Avatar { get; set; }
    [JsonProperty("sectionId")] public int? SectionId { get; set; }
    [JsonProperty("sectionName")] public string? SectionName { get; set; }
    [JsonProperty("level")] public string? Level { get; set; }
    [JsonProperty("viewCount")] public int? ViewCount { get; set; }
    [JsonProperty("likeCount")] public int? LikeCount { get; set; }
    [JsonProperty("replyCount")] public int? ReplyCount { get; set; }
    [JsonProperty("createTime")] public string? CreateTime { get; set; }

    [JsonProperty("essence")] private object? EssenceRaw { get; set; }
    [JsonProperty("top")] private object? TopRaw { get; set; }

    public bool IsEssence => EssenceRaw switch
    {
        bool b => b, int i => i == 1, string s => s == "1", _ => false
    };
    public bool IsTop => TopRaw switch
    {
        bool b => b, int i => i == 1, string s => s == "1", _ => false
    };

    public PostDetail ToPostDetail() => new()
    {
        Topic = new Topic
        {
            Id = Id,
            Title = Title,
            Content = RichBody ?? Body,
            UserId = UserId,
            CommentCount = ReplyCount,
            View = ViewCount,
            Top = IsTop,
            Good = IsEssence,
            InTime = CreateTime
        },
        TopicUser = new User { Username = UserName, Avatar = Avatar },
        Comments = null
    };
}

/// <summary>通用板块（争渡/爱盲）</summary>
public class Forum
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>爱盲论坛板块信息（Discuz! API）</summary>
public class AiMangForum
{
    [JsonProperty("fid")] public string Fid { get; set; } = "";
    [JsonProperty("name")] public string Name { get; set; } = "";
}

/// <summary>爱盲论坛帖子列表项（Discuz! API）</summary>
public class AiMangThread
{
    [JsonProperty("tid")] public string Tid { get; set; } = "";
    [JsonProperty("author")] public string Author { get; set; } = "";
    [JsonProperty("authorid")] public string AuthorId { get; set; } = "0";
    [JsonProperty("subject")] public string Subject { get; set; } = "";
    [JsonProperty("dateline")] public string Dateline { get; set; } = "";
    [JsonProperty("views")] public string Views { get; set; } = "0";
    [JsonProperty("replies")] public string Replies { get; set; } = "0";
    [JsonProperty("displayorder")] public string DisplayOrder { get; set; } = "0";
    [JsonProperty("digest")] public string Digest { get; set; } = "0";
    [JsonProperty("avatar")] public string? Avatar { get; set; }
    public bool IsTop => DisplayOrder != "0";
    public bool IsDigest => Digest != "0";
    public int ViewCount => int.TryParse(Views, out var v) ? v : 0;
    public int ReplyCount => int.TryParse(Replies, out var r) ? r : 0;
    public int AuthorIdInt => int.TryParse(AuthorId, out var a) ? a : 0;
}

/// <summary>爱盲论坛帖子楼层（Discuz! API）</summary>
public class AiMangPost
{
    [JsonProperty("pid")] public string Pid { get; set; } = "";
    [JsonProperty("tid")] public string Tid { get; set; } = "";
    [JsonProperty("first")] public string First { get; set; } = "0";
    [JsonProperty("author")] public string Author { get; set; } = "";
    [JsonProperty("authorid")] public string AuthorId { get; set; } = "0";
    [JsonProperty("dateline")] public string Dateline { get; set; } = "";
    [JsonProperty("message")] public string Message { get; set; } = "";
    [JsonProperty("username")] public string Username { get; set; } = "";
    [JsonProperty("avatar")] public string? Avatar { get; set; }
    [JsonProperty("dbdateline")] public string DbDateline { get; set; } = "0";
    public bool IsMainPost => First == "1";
    public int AuthorIdInt => int.TryParse(AuthorId, out var a) ? a : 0;
    public long PidLong => long.TryParse(Pid, out var p) ? p : 0;
    public long DbDatelineLong => long.TryParse(DbDateline, out var d) ? d : 0;
}

/// <summary>爱盲论坛帖子详情聚合（Discuz! API）</summary>
public class AiMangThreadDetail
{
    public AiMangThread? Thread { get; set; }
    public List<AiMangPost>? PostList { get; set; }
    public string Fid { get; set; } = "";
}

/// <summary>GitHub Release 信息</summary>
public class GitHubRelease
{
    [JsonProperty("tag_name")] public string? TagName { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("body")] public string? Body { get; set; }
    [JsonProperty("published_at")] public string? PublishedAt { get; set; }
    [JsonProperty("html_url")] public string? HtmlUrl { get; set; }
    [JsonProperty("assets")] public List<GitHubAsset>? Assets { get; set; }

    public bool HasUpdate { get; set; }
    public string? DownloadUrl { get; set; }
}

/// <summary>GitHub Release 资产</summary>
public class GitHubAsset
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    [JsonProperty("size")] public long Size { get; set; }
}
