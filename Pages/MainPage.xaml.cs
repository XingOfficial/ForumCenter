using System.Net;
using System.Text.RegularExpressions;
using ForumCenter.Models;
using ForumCenter.Services;
using ForumCenter.Utilities;

namespace ForumCenter.Pages;




public partial class MainPage : ContentPage
{
    private readonly PreferencesService _prefs = new();
    private IApiService _api = ApiServiceFactory.Current;

    private readonly List<Post> _posts = new();
    private int _page = 1;
    private string _tab = "all";
    private bool _isLoading;
    private bool _hasMore = true;

    private static readonly string[] Tabs = { "all", "new", "hot", "good" };

    public MainPage()
    {
        InitializeComponent();
        InitCommunityPicker();
        InitTabs();
        ApplyCurrentCommunity(switchApi: true);
        UpdateTabStyles();
    }

    private void InitCommunityPicker()
    {
        CommunityPicker.Items.Clear();
        CommunityPicker.Items.Add("天坦社区");
        CommunityPicker.Items.Add("帮盲社区");
        CommunityPicker.Items.Add("争渡论坛");
        CommunityPicker.Items.Add("爱盲论坛");

        var current = _prefs.GetCurrentCommunity();
        CommunityPicker.SelectedIndex = current switch
        {
            CommunityType.BangMang => 1,
            CommunityType.ZhengDu => 2,
            CommunityType.AiMang => 3,
            _ => 0
        };
        CommunityPicker.SelectedIndexChanged += OnCommunityChanged;
    }

    private void InitTabs()
    {
        var buttons = new[] { TabAll, TabNew, TabHot, TabGood };
        for (var i = 0; i < buttons.Length; i++)
            buttons[i].ClassId = Tabs[i];
    }

    private void ApplyCurrentCommunity(bool switchApi)
    {
        var type = _prefs.GetCurrentCommunity();
        if (switchApi)
        {
            
            ApiServiceFactory.SetCurrent(type);
            _api = ApiServiceFactory.Current;
            _api.ClearCache();
        }
    }

    private void OnCommunityChanged(object? sender, EventArgs e)
    {
        var type = CommunityPicker.SelectedIndex switch
        {
            1 => CommunityType.BangMang,
            2 => CommunityType.ZhengDu,
            3 => CommunityType.AiMang,
            _ => CommunityType.Tatans
        };

        _prefs.SetCurrentCommunity(type);
        ApiServiceFactory.SetCurrent(type);
        _api = ApiServiceFactory.Current;
        _api.ClearCache();

        ResetList();
        _ = LoadPostsAsync(reset: true);
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.ClassId is string t)
        {
            if (t == _tab)
            {
                
                PostsListView.ScrollTo(0, position: ScrollToPosition.Start, animated: true);
                return;
            }

            _tab = t;
            UpdateTabStyles();
            ResetList();
            _ = LoadPostsAsync(reset: true);
        }
    }

    private void UpdateTabStyles()
    {
        var buttons = new[] { TabAll, TabNew, TabHot, TabGood };
        var indicators = new[] { IndicatorAll, IndicatorNew, IndicatorHot, IndicatorGood };
        for (var i = 0; i < buttons.Length; i++)
        {
            var active = buttons[i].ClassId == _tab;
            
            buttons[i].BackgroundColor = Colors.Transparent;
            buttons[i].TextColor = Colors.White;
            buttons[i].FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            
            indicators[i].Color = active ? Colors.White : Colors.Transparent;
        }
    }

    private Color GetColor(string key) => Application.Current?.Resources?[key] as Color ?? Colors.Gray;

    private void ResetList()
    {
        _posts.Clear();
        _hasMore = true;
        PostsListView.ItemsSource = null;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        
        var current = _prefs.GetCurrentCommunity();
        var expectedIndex = current switch
        {
            CommunityType.BangMang => 1,
            CommunityType.ZhengDu => 2,
            CommunityType.AiMang => 3,
            _ => 0
        };

        if (CommunityPicker.SelectedIndex != expectedIndex)
        {
            CommunityPicker.SelectedIndex = expectedIndex;
            ApplyCurrentCommunity(switchApi: true);
        }

        if (_posts.Count == 0)
            _ = LoadPostsAsync(reset: true);
    }

    private async Task LoadPostsAsync(bool reset)
    {
        if (_isLoading) return;

        if (reset)
        {
            _page = 1;
            _hasMore = true;
        }

        if (!_hasMore) return;

        _isLoading = true;
        BottomHintLabel.Text = "正在加载...";

        try
        {
            var result = await _api.GetPostsAsync(_page, _tab);
            if (result != null)
            {
                
                foreach (var p in result)
                {
                    if (!string.IsNullOrEmpty(p.Content))
                        p.Content = TextUtil.Summarize(p.Content, 80);
                }
                _posts.AddRange(result);
            }

            
            PostsListView.ItemsSource = null;
            PostsListView.ItemsSource = _posts;

            _hasMore = result is { Count: > 0 };
            BottomHintLabel.Text = _hasMore ? "" : "暂无更多";
        }
        catch (Exception ex)
        {
            BottomHintLabel.Text = "";
            await DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            _isLoading = false;
            PostsListView.EndRefresh();
        }
    }

    private void OnRefreshing(object? sender, EventArgs e) => _ = LoadPostsAsync(reset: true);

    private void OnItemAppearing(object? sender, ItemVisibilityEventArgs e)
    {
        if (e.Item is not Post p || _posts.Count == 0) return;

        
        if (p.Id == _posts[^1].Id && _hasMore && !_isLoading)
        {
            _page++;
            _ = LoadPostsAsync(reset: false);
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

    private async void OnCreatePostClicked(object? sender, EventArgs e)
    {
        
        await Shell.Current.GoToAsync(nameof(PostCreatePage));
    }
}
