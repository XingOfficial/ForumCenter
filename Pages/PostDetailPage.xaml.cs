using System.Net;
using System.Text.RegularExpressions;
using ForumCenter.Models;
using ForumCenter.Services;
using ForumCenter.Utilities;

namespace ForumCenter.Pages;





[QueryProperty("PostId", "postId")]
public partial class PostDetailPage : ContentPage
{
    private readonly PreferencesService _prefs = new();
    private IApiService Api => ApiServiceFactory.Current;

    private long _postId;
    private PostDetail? _detail;
    private int _currentUserId;

    
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

        
        ContentLabel.Text = TextUtil.HtmlToPlainText(topic?.Content ?? "");

        
        var upIds = topic?.UpIds;
        var likeCount = string.IsNullOrEmpty(upIds)
            ? 0
            : upIds.Split(",", StringSplitOptions.RemoveEmptyEntries).Length;
        LikeCountLabel.Text = likeCount > 0 ? likeCount.ToString() : "";

        
        ViewCountLabel.Text = topic?.View is int v && v > 0 ? $"浏览 {v}" : "";

        
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

        
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await OnCommentTappedAsync(c);
        layout.GestureRecognizers.Add(tap);

        return layout;
    }

    
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

    
    private async Task GoToUserProfileAsync(int userId, string userName)
    {
        if (userId <= 0)
        {
            await DisplayAlert("提示", "该用户资料不可用", "确定");
            return;
        }
        await Shell.Current.GoToAsync($"{nameof(UserProfilePage)}?userId={userId}&userName={Uri.EscapeDataString(userName)}");
    }

    
    private async void OnAuthorTapped(object? sender, TappedEventArgs e)
    {
        var topic = _detail?.Topic;
        var userId = topic?.UserId ?? _detail?.TopicUser?.Id ?? 0;
        var userName = _detail?.TopicUser?.Username ?? "";
        await GoToUserProfileAsync(userId, userName);
    }

    
    private async void OnLikeClicked(object? sender, EventArgs e)
    {
        if (!_prefs.IsLoggedIn())
        {
            var go = await DisplayAlert("提示", "请先登录后再点赞", "去登录", "取消");
            if (go)
                await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        
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

        
        var finalContent = content;
        if (_prefs.IsPostTailEnabled())
        {
            var tail = _prefs.GetPostTailContent();
            if (!string.IsNullOrEmpty(tail))
            {
                if (Api.CommunityType == CommunityType.AiMang)
                {
                    
                    var plainTail = Regex.Replace(tail, @"<[^>]+>", "");
                    finalContent = $"{content}\n{plainTail}";
                }
                else
                {
                    
                    finalContent = $"{content}<br>{tail}";
                }
            }
        }

        
        long? replyCommentId = _replyToComment?.Id;

        SendButton.IsEnabled = false;
        try
        {
            var ok = await Api.SendCommentAsync(_postId, finalContent, replyCommentId);
            if (ok)
            {
                
                var replyToName = _replyToComment?.Username;
                CommentEntry.Text = "";
                _replyToComment = null;
                CommentEntry.Placeholder = "写评论...";

                
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

    
    private void AppendLocalComment(string content, string? replyToName)
    {
        
        if (CommentsContainer.Children.Count == 1 &&
            CommentsContainer.Children[0] is Label placeholder &&
            placeholder.Text == "暂无评论")
        {
            CommentsContainer.Children.Clear();
        }

        
        var newComment = new Comment
        {
            Id = 0,
            UserId = _currentUserId,
            TopicId = _postId,
            Username = "我",
            Content = !string.IsNullOrEmpty(replyToName) ? $"回复 {replyToName}: {content}" : content,
            InTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        
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
