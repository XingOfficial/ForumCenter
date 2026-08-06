using System.Net;
using System.Text.RegularExpressions;
using ForumCenter.Models;
using ForumCenter.Services;
using ForumCenter.Utilities;

namespace ForumCenter.Pages;

/// <summary>
/// 发帖/编辑页面。新建模式调用 CreatePostAsync，编辑模式（带 postId）预填内容后调用 EditPostAsync。
/// 天坦社区用标签（Tags），帮盲社区用板块（Sections），争渡/爱盲用论坛（Forums）。
/// 支持图片上传（仅天坦）、手动插入图片链接、发帖小尾巴（天坦/争渡用 &lt;br&gt;，爱盲用纯文本）。
/// </summary>
[QueryProperty("PostId", "postId")]
public partial class PostCreatePage : ContentPage
{
    private readonly PreferencesService _prefs = new();
    private IApiService Api => ApiServiceFactory.Current;

    private long _postId;
    private bool IsEditMode => _postId > 0;

    private readonly List<Tag> _tags = new();
    private readonly List<Section> _sections = new();
    private readonly List<Forum> _forums = new();
    private bool _initialized;

    // 长按上传按钮检测：长按触发"插入图片链接"对话框（原版行为）
    private DateTime _pressStartTime;
    private bool _longPressHandled;

    public string PostId
    {
        get => _postId.ToString();
        set
        {
            _postId = long.TryParse(value, out var id) ? id : 0;
            Title = IsEditMode ? "编辑帖子" : "发帖";
            PublishButton.Text = IsEditMode ? "保存" : "发布";
        }
    }

    public PostCreatePage()
    {
        InitializeComponent();
        // 长按上传按钮触发"插入图片链接"对话框（原版行为）
        UploadImageButton.Pressed += OnUploadButtonPressed;
        UploadImageButton.Released += OnUploadButtonReleased;
        _ = InitAsync();
    }

    private void OnUploadButtonPressed(object? sender, EventArgs e)
    {
        _pressStartTime = DateTime.Now;
        _longPressHandled = false;
    }

    private async void OnUploadButtonReleased(object? sender, EventArgs e)
    {
        var duration = DateTime.Now - _pressStartTime;
        if (duration.TotalMilliseconds >= 500)
        {
            _longPressHandled = true;
            await InsertImageUrlAsync();
        }
    }

    private async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            switch (Api.CommunityType)
            {
                case CommunityType.Tatans:
                    SectionPicker.Title = "选择标签";
                    var tags = await Api.GetTagsAsync();
                    _tags.Clear();
                    _tags.AddRange(tags);
                    SectionPicker.ItemsSource = _tags.Select(t => t.Name).ToList();
                    break;

                case CommunityType.BangMang:
                    SectionPicker.Title = "选择板块";
                    var sections = await Api.GetSectionsAsync();
                    _sections.Clear();
                    _sections.AddRange(sections);
                    SectionPicker.ItemsSource = _sections.Select(s => s.Name).ToList();
                    break;

                case CommunityType.ZhengDu:
                case CommunityType.AiMang:
                    SectionPicker.Title = "选择论坛";
                    var forums = await Api.GetForumsAsync();
                    _forums.Clear();
                    _forums.AddRange(forums);
                    SectionPicker.ItemsSource = _forums.Select(f => f.Name).ToList();
                    break;
            }

            // 原版：上传按钮始终可见，非天坦社区点击时弹提示
            if (IsEditMode)
                await LoadForEditAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("提示", $"加载板块信息失败：{ex.Message}", "确定");
        }
    }

    private async Task LoadForEditAsync()
    {
        try
        {
            var detail = await Api.GetPostDetailAsync(_postId);
            var topic = detail?.Topic;
            TitleEntry.Text = topic?.Title ?? "";
            ContentEditor.Text = TextUtil.HtmlToPlainText(topic?.Content ?? "");
        }
        catch (Exception ex)
        {
            await DisplayAlert("提示", $"加载帖子内容失败：{ex.Message}", "确定");
        }
    }

    private async void OnPublishClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim();
        var content = ContentEditor.Text?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            await DisplayAlert("提示", "请输入标题", "确定");
            return;
        }
        if (string.IsNullOrEmpty(content))
        {
            await DisplayAlert("提示", "请输入内容", "确定");
            return;
        }

        // 帮盲社区不支持发帖
        if (Api.CommunityType == CommunityType.BangMang)
        {
            await DisplayAlert("提示", "帮盲社区暂不支持发帖", "确定");
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

        PublishButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;

        try
        {
            var tagOrSection = ResolveTagOrSection();

            bool ok;
            if (IsEditMode)
                ok = await Api.EditPostAsync(_postId, title, finalContent);
            else
                ok = await Api.CreatePostAsync(title, finalContent, tagOrSection);

            if (ok)
            {
                await DisplayAlert("成功", IsEditMode ? "编辑成功" : "发布成功", "确定");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("失败", IsEditMode ? "编辑失败，请稍后重试" : "发布失败，请先登录或稍后重试", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
        finally
        {
            PublishButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
        }
    }

    /// <summary>根据当前社区解析标签/板块/论坛值。天坦传标签名，帮盲传板块 Id，争渡/爱盲传论坛 fid。</summary>
    private string ResolveTagOrSection()
    {
        var idx = SectionPicker.SelectedIndex;
        if (idx < 0) return "";

        return Api.CommunityType switch
        {
            CommunityType.Tatans when idx < _tags.Count => _tags[idx].Name ?? "",
            CommunityType.BangMang when idx < _sections.Count => _sections[idx].Id.ToString(),
            CommunityType.ZhengDu when idx < _forums.Count => _forums[idx].Id,
            CommunityType.AiMang when idx < _forums.Count => _forums[idx].Id,
            _ => ""
        };
    }

    /// <summary>上传图片（仅天坦社区支持）。短按触发上传；长按则触发"插入图片链接"。</summary>
    private async void OnUploadImageClicked(object? sender, EventArgs e)
    {
        // 长按已触发"插入图片链接"对话框，跳过本次上传
        if (_longPressHandled)
        {
            _longPressHandled = false;
            return;
        }

        if (Api.CommunityType != CommunityType.Tatans)
        {
            await DisplayAlert("提示", "该社区暂不支持图片上传", "确定");
            return;
        }

        var result = await FilePicker.PickAsync(new PickOptions
        {
            FileTypes = FilePickerFileType.Images,
            PickerTitle = "选择图片"
        });
        if (result == null) return;

        try
        {
            using var stream = await result.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var url = await Api.UploadImageAsync(bytes, result.FileName);
            if (!string.IsNullOrEmpty(url))
            {
                InsertTextAtCursor($"<img src=\"{url}\" />");
                await DisplayAlert("成功", "图片已插入", "确定");
            }
            else
            {
                await DisplayAlert("提示", "图片上传失败", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", $"图片上传失败: {ex.Message}", "确定");
        }
    }

    /// <summary>手动插入图片链接（长按上传按钮触发，原版行为）。</summary>
    private async Task InsertImageUrlAsync()
    {
        var url = await DisplayPromptAsync("插入图片链接", "输入图片URL", "插入", "取消", "https://example.com/image.png");
        if (!string.IsNullOrEmpty(url))
            InsertTextAtCursor($"<img src=\"{url}\" />");
    }

    /// <summary>在内容编辑框末尾插入文本。</summary>
    private void InsertTextAtCursor(string text)
    {
        var current = ContentEditor.Text ?? "";
        ContentEditor.Text = current + text;
    }
}
