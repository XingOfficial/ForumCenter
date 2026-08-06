using System.Net;
using System.Text.RegularExpressions;
using ForumCenter.Models;
using ForumCenter.Services;
using ForumCenter.Utilities;

namespace ForumCenter.Pages;

/// <summary>
/// 帖子详情页：展示标题/作者/时间/正文、点赞/浏览统计、评论列表，
/// 支持发送评论（含楼中楼回复）、点赞、编辑/删除（带权限检查）、查看用户资料、发帖小尾巴、局部刷新评论。
/// </summary>
[QueryProperty("PostId", "postId")]
public partial class PostDetailPage : ContentPage
{
    private readonly PreferencesService _prefs = new();
    private IApiService Api => ApiServiceFactory.Current;

    private long _postId;
    private PostDetail? _detail;
    private int _currentUserId;

    /// <summary>当前回复的目标评论（楼中楼），为 null 表示普通回复主题。</summary>
    private Comment? _replyToComment;

    public string PostId
    {
        get => _postId.ToString();
        set
        {
            _postId = long.TryParse(value, out var id) ? id : 0;
            if (_postId > 0)
                _ = LoadDetailAsync();
        }
    }

    public PostDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_detail == null && _postId > 0)
            _ = LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        if (_postId <= 0) return;

        LoadingIndicator.IsRunning = true;
        try
        {
            _detail = await Api.GetPostDetailAsync(_postId);

            // 尝试获取当前用户用于作者权限判断
            try
            {
                var me = await Api.GetCurrentUserInfoAsync();
                _currentUserId = me?.Id ?? 0;
            }
            catch
            {
                _currentUserId = 0;
            }

            BindDetail();
        }
        catch (Exception ex)
        {
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
        }
    }

    private void BindDetail()
    {
        if (_detail == null) return;

        var topic = _detail.Topic;
        var user = _detail.TopicUser;

        TitleLabel.Text = topic?.Title ?? "";
        AuthorLabel.Text = user?.Username ?? "";
        TimeLabel.Text = FormatTime(topic?.InTime);

        // 正文：将 HTML 内容转为纯文本展示
        ContentLabel.Text = TextUtil.HtmlToPlainText(topic?.Content ?? "");

        // 点赞数：从 UpIds 解析（逗号分隔的用户 Id 列表）
        var upIds = topic?.UpIds;
        var likeCount = string.IsNullOrEmpty(upIds)
            ? 0
            : upIds.Split(",", StringSplitOptions.RemoveEmptyEntries).Length;
        LikeCountLabel.Text = likeCount > 0 ? likeCount.ToString() : "";

        // 浏览量
        ViewCountLabel.Text = topic?.View is int v && v > 0 ? $"浏览 {v}" : "";

        // 原版：帮盲社区隐藏评论输入框、评论列表，显示不支持提示
        if (Api.CommunityType == CommunityType.BangMang)
        {
            CommentInputArea.IsVisible = false;
            CommentsLabel.IsVisible = false;
            CommentsContainer.IsVisible = false;
            NoCommentHintLabel.IsVisible = true;
        }

        BuildComments(_detail.Comments);
    }

    private void BuildComments(List<Comment>? comments)
    {
        CommentsContainer.Children.Clear();

        if (comments == null || comments.Count == 0)
        {
            CommentsContainer.Children.Add(new Label
            {
                Text = "暂无评论",
                TextColor = GetColor("TextSecondary"),
                Margin = new Thickness(16)
            });
            return;
        }

        foreach (var c in comments)
            CommentsContainer.Children.Add(CreateCommentView(c));
    }

    private StackLayout CreateCommentView(Comment c)
    {
        // 原版 item_comment.xml：padding=12dp，垂直排列，marginTop=6dp
        var layout = new StackLayout
        {
            Spacing = 6,
            Padding = new Thickness(12, 8, 12, 8)
        };

        var header = new HorizontalStackLayout { Spacing = 8 };
        var authorLabel = new Label
        {
            Text = c.Username,
            FontSize = 13,
            TextColor = GetColor("Primary")
        };
        // 点击评论作者名也可跳转资料
        authorLabel.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => _ = GoToUserProfileAsync(c.UserId, c.Username))
        });
        header.Children.Add(authorLabel);
        header.Children.Add(new Label
        {
            Text = FormatTime(c.InTime),
            FontSize = 12,
            TextColor = GetColor("TextSecondary"),
            VerticalOptions = LayoutOptions.Center
        });

        layout.Children.Add(header);
        layout.Children.Add(new Label
        {
            Text = TextUtil.HtmlToPlainText(c.Content),
            FontSize = 14,
            TextColor = GetColor("TextPrimary"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        // 点击评论项弹出操作菜单（回复 / 查看资料）
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await OnCommentTappedAsync(c);
        layout.GestureRecognizers.Add(tap);

        return layout;
    }

    /// <summary>点击评论项弹出操作菜单。</summary>
    private async Task OnCommentTappedAsync(Comment c)
    {
        var action = await DisplayActionSheet(c.Username, "取消", null, "回复", "查看资料");
        switch (action)
        {
            case "回复":
                _replyToComment = c;
                CommentEntry.Placeholder = $"回复 {c.Username}:";
                CommentEntry.Focus();
                break;
            case "查看资料":
                await GoToUserProfileAsync(c.UserId, c.Username);
                break;
        }
    }

    /// <summary>跳转到用户资料页。</summary>
    private async Task GoToUserProfileAsync(int userId, string userName)
    {
        if (userId <= 0)
        {
            await DisplayAlert("提示", "该用户资料不可用", "确定");
            return;
        }
        await Shell.Current.GoToAsync($"{nameof(UserProfilePage)}?userId={userId}&userName={Uri.EscapeDataString(userName)}");
    }

    /// <summary>点击帖子作者名跳转用户资料。</summary>
    private async void OnAuthorTapped(object? sender, TappedEventArgs e)
    {
        var topic = _detail?.Topic;
        var userId = topic?.UserId ?? _detail?.TopicUser?.Id ?? 0;
        var userName = _detail?.TopicUser?.Username ?? "";
        await GoToUserProfileAsync(userId, userName);
    }

    /// <summary>点赞。</summary>
    private async void OnLikeClicked(object? sender, EventArgs e)
    {
        if (!_prefs.IsLoggedIn())
        {
            var go = await DisplayAlert("提示", "请先登录后再点赞", "去登录", "取消");
            if (go)
                await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        // 原版：非天坦社区点击弹 Toast"XX暂不支持点赞"（按钮始终可见）
        if (Api.CommunityType != CommunityType.Tatans)
        {
            await DisplayAlert("提示", $"{Api.DisplayName}暂不支持点赞", "确定");
            return;
        }

        try
        {
            var ok = await Api.VoteTopicAsync(_postId);
            if (ok)
            {
                var current = int.TryParse(LikeCountLabel.Text, out var c) ? c : 0;
                LikeCountLabel.Text = $"{current + 1}";
            }
            else
            {
                await DisplayAlert("提示", "点赞失败，可能已经点过赞", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
    }

    private async void OnSendCommentClicked(object? sender, EventArgs e) => await SendCommentAsync();

    private async void OnCommentEntryCompleted(object? sender, EventArgs e) => await SendCommentAsync();

    private async Task SendCommentAsync()
    {
        var content = CommentEntry.Text?.Trim();
        if (string.IsNullOrEmpty(content))
        {
            await DisplayAlert("提示", "请输入评论内容", "确定");
            return;
        }

        if (!_prefs.IsLoggedIn())
        {
            var go = await DisplayAlert("提示", "请先登录后再评论", "去登录", "取消");
            if (go)
                await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        // 追加发帖小尾巴：天坦和争渡用 <br> 连接，爱盲用 \n 连接纯文本小尾巴
        var finalContent = content;
        if (_prefs.IsPostTailEnabled())
        {
            var tail = _prefs.GetPostTailContent();
            if (!string.IsNullOrEmpty(tail))
            {
                if (Api.CommunityType == CommunityType.AiMang)
                {
                    // 爱盲为纯文本，去除 HTML 标签后用换行连接
                    var plainTail = Regex.Replace(tail, @"<[^>]+>", "");
                    finalContent = $"{content}\n{plainTail}";
                }
                else
                {
                    // 天坦和争渡用 <br> 连接
                    finalContent = $"{content}<br>{tail}";
                }
            }
        }

        // 楼中楼回复传入目标评论 Id
        long? replyCommentId = _replyToComment?.Id;

        SendButton.IsEnabled = false;
        try
        {
            var ok = await Api.SendCommentAsync(_postId, finalContent, replyCommentId);
            if (ok)
            {
                // 在清空 _replyToComment 之前保存回复目标用户名
                var replyToName = _replyToComment?.Username;
                CommentEntry.Text = "";
                _replyToComment = null;
                CommentEntry.Placeholder = "写评论...";

                // 局部刷新：直接在评论列表末尾插入新评论，不重新加载整个详情
                AppendLocalComment(content, replyToName);
            }
            else
            {
                await DisplayAlert("失败", "评论发送失败", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    /// <summary>局部刷新：在评论列表末尾追加刚发送的评论，避免重新拉取整个详情。</summary>
    private void AppendLocalComment(string content, string? replyToName)
    {
        // 若当前显示的是"暂无评论"占位，先清空
        if (CommentsContainer.Children.Count == 1 &&
            CommentsContainer.Children[0] is Label placeholder &&
            placeholder.Text == "暂无评论")
        {
            CommentsContainer.Children.Clear();
        }

        // 原版：回复评论时显示"回复 {用户名}: {内容}"前缀
        var newComment = new Comment
        {
            Id = 0,
            UserId = _currentUserId,
            TopicId = _postId,
            Username = "我",
            Content = !string.IsNullOrEmpty(replyToName) ? $"回复 {replyToName}: {content}" : content,
            InTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // 同步更新内存中的评论列表
        if (_detail?.Comments == null)
            _detail!.Comments = new List<Comment>();
        _detail.Comments.Add(newComment);

        CommentsContainer.Children.Add(CreateCommentView(newComment));
    }

    private Color GetColor(string key) => Application.Current?.Resources?[key] as Color ?? Colors.Gray;

    private static string FormatTime(string? time)
    {
        if (string.IsNullOrEmpty(time)) return "";
        return time;
    }

    private static string FormatTime(long timestamp)
    {
        if (timestamp <= 0) return "";
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime
                .ToString("yyyy-MM-dd HH:mm");
        }
        catch
        {
            return "";
        }
    }
}
