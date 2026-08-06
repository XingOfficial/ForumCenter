using ForumCenter.Models;
using ForumCenter.Services;

namespace ForumCenter.Pages;

/// <summary>
/// 用户资料页：展示用户基本信息与该用户发布的帖子列表。目前仅天坦社区支持完整资料与帖子查询。
/// </summary>
[QueryProperty("UserId", "userId")]
[QueryProperty("UserName", "userName")]
public partial class UserProfilePage : ContentPage
{
    private IApiService Api => ApiServiceFactory.Current;
    private int _userId;
    private int _currentPage = 1;
    private bool _isLoading;
    private bool _hasMore = true;
    private readonly List<Post> _posts = new();

    public string UserId
    {
        get => _userId.ToString();
        set
        {
            _userId = int.TryParse(value, out var id) ? id : 0;
            _ = LoadDataAsync();
        }
    }

    public string UserName { get; set; } = "";

    public UserProfilePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_userId > 0 && _posts.Count == 0 && !_isLoading)
            _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (_userId <= 0 || _isLoading) return;
        _isLoading = true;

        LoadingIndicator.IsRunning = true;
        Title = $"{UserName} 的资料";
        UsernameLabel.Text = UserName;
        BioLabel.Text = "加载中...";
        PostCountLabel.Text = "帖子: 0";
        CommentCountLabel.Text = "评论: 0";
        ScoreLabel.Text = "积分: 0";

        try
        {
            // 加载用户信息
            if (Api.CommunityType == CommunityType.Tatans)
            {
                var user = await Api.GetCurrentUserInfoAsync();
                if (user != null && (user.Id == _userId || _userId == 0))
                {
                    UsernameLabel.Text = user.Username ?? UserName;
                    BioLabel.Text = string.IsNullOrEmpty(user.Bio) ? "暂无简介" : StripHtml(user.Bio);
                    PostCountLabel.Text = $"帖子: {user.TopicCount ?? 0}";
                    CommentCountLabel.Text = $"评论: {user.CommentCount ?? 0}";
                    ScoreLabel.Text = $"积分: {user.Score ?? 0}";
                }
            }
            else
            {
                BioLabel.Text = $"{Api.DisplayName}暂不支持查看用户资料";
            }

            // 加载用户帖子
            if (Api.CommunityType == CommunityType.Tatans)
            {
                var posts = await Api.GetPostsByUserAsync(_userId, _currentPage);
                _posts.Clear();
                _posts.AddRange(posts);
                PostsListView.ItemsSource = null;
                PostsListView.ItemsSource = _posts;
                PostCountLabel.Text = $"帖子: {_posts.Count}";
                _hasMore = posts.Count > 0;
            }
            else
            {
                _hasMore = false;
            }
        }
        catch (Exception ex)
        {
            BioLabel.Text = $"加载失败: {ex.Message}";
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            _isLoading = false;
        }
    }

    private async void OnPostTapped(object? sender, ItemTappedEventArgs e)
    {
        if (e.Item is Post post)
        {
            PostsListView.SelectedItem = null;
            await Shell.Current.GoToAsync($"{nameof(PostDetailPage)}?postId={post.Id}");
        }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", "").Trim();
    }
}
