using ForumCenter.Models;
using ForumCenter.Services;

namespace ForumCenter.Pages;

/// <summary>
/// 设置页：账号状态、登录/退出、字体大小、发帖小尾巴。布局与原版 activity_settings.xml 保持一致。
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly PreferencesService _prefs = new();

    /// <summary>字体滑块进度 0 对应的实际字号。</summary>
    private const int FontSizeBase = 12;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        RefreshAccountStatus();

        // 滑块范围 0-20，实际字号 = 12 + 进度
        var stored = _prefs.GetFontSize();
        FontSizeSlider.Value = Math.Clamp(stored - FontSizeBase, 0, 20);
        FontSizeLabel.Text = $"{stored}sp";

        PostTailSwitch.IsToggled = _prefs.IsPostTailEnabled();
    }

    private void RefreshAccountStatus()
    {
        var loggedIn = _prefs.IsLoggedIn();
        var community = _prefs.GetCommunityDisplayName();

        AccountStatusLabel.Text = loggedIn
            ? $"已登录：{community}"
            : $"未登录（当前社区：{community}）";

        // 同一个按钮根据登录状态切换文字
        LoginLogoutButton.Text = loggedIn ? "退出登录" : "登录";
    }

    /// <summary>登录/退出按钮：根据当前登录状态执行对应操作。</summary>
    private async void OnLoginLogoutClicked(object? sender, EventArgs e)
    {
        if (_prefs.IsLoggedIn())
        {
            // 原版：直接退出，无确认对话框
            _prefs.LogoutCurrent();
            ApiServiceFactory.SetCurrent(_prefs.GetCurrentCommunity());
            RefreshAccountStatus();
            await DisplayAlert("提示", "已退出登录", "确定");
        }
        else
        {
            await Shell.Current.GoToAsync(nameof(LoginPage));
        }
    }

    private void OnFontSizeChanged(object? sender, ValueChangedEventArgs e)
    {
        // 滑块进度 0-20，实际字号 = 12 + 进度
        var progress = (int)Math.Round(e.NewValue);
        var size = FontSizeBase + progress;
        _prefs.SetFontSize(size);
        FontSizeLabel.Text = $"{size}sp";
    }

    private async void OnPostTailToggled(object? sender, ToggledEventArgs e)
    {
        _prefs.SetPostTailEnabled(e.Value);
        // 原版：切换后弹 Toast"保存成功"
        await DisplayAlert("提示", "保存成功", "确定");
    }
}
