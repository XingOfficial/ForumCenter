using ForumCenter.Models;
using ForumCenter.Services;

namespace ForumCenter.Pages;




public partial class SettingsPage : ContentPage
{
    private readonly PreferencesService _prefs = new();

    
    private const int FontSizeBase = 12;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        RefreshAccountStatus();

        
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

        
        LoginLogoutButton.Text = loggedIn ? "退出登录" : "登录";
    }

    
    private async void OnLoginLogoutClicked(object? sender, EventArgs e)
    {
        if (_prefs.IsLoggedIn())
        {
            
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
        
        var progress = (int)Math.Round(e.NewValue);
        var size = FontSizeBase + progress;
        _prefs.SetFontSize(size);
        FontSizeLabel.Text = $"{size}sp";
    }

    private async void OnPostTailToggled(object? sender, ToggledEventArgs e)
    {
        _prefs.SetPostTailEnabled(e.Value);
        
        await DisplayAlert("提示", "保存成功", "确定");
    }
}
