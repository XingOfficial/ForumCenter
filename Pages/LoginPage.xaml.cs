using ForumCenter.Models;
using ForumCenter.Services;

namespace ForumCenter.Pages;

/// <summary>
/// 登录页：通过 RadioButton 选择社区后调用对应社区的 LoginAsync，成功后保存 token 并返回。
/// </summary>
public partial class LoginPage : ContentPage
{
    private readonly PreferencesService _prefs = new();

    public LoginPage()
    {
        InitializeComponent();
        InitCommunityRadio();
    }

    /// <summary>根据持久化的当前社区，选中对应的 RadioButton 并初始化提示文字。</summary>
    private void InitCommunityRadio()
    {
        var current = _prefs.GetCurrentCommunity();
        switch (current)
        {
            case CommunityType.BangMang:
                RbBangMang.IsChecked = true;
                break;
            case CommunityType.ZhengDu:
                RbZhengDu.IsChecked = true;
                break;
            case CommunityType.AiMang:
                RbAiMang.IsChecked = true;
                break;
            default:
                RbTatans.IsChecked = true;
                break;
        }

        UpdateUsernamePlaceholder(GetSelectedCommunity());
    }

    /// <summary>RadioButton 选中状态变化时，动态更新用户名输入框的提示文字。</summary>
    private void OnCommunityRadioChanged(object? sender, CheckedChangedEventArgs e)
    {
        // 只在选中时响应，避免取消选中时也触发
        if (!e.Value) return;
        UpdateUsernamePlaceholder(GetSelectedCommunity());
    }

    /// <summary>根据社区类型更新用户名输入框提示。</summary>
    private void UpdateUsernamePlaceholder(CommunityType type)
    {
        UsernameEntry.Placeholder = type switch
        {
            CommunityType.BangMang => "请输入用户名",
            CommunityType.ZhengDu => "邮箱",
            CommunityType.AiMang => "用户名",
            _ => "请输入手机号"
        };
    }

    /// <summary>获取当前选中的社区类型。</summary>
    private CommunityType GetSelectedCommunity()
    {
        if (RbBangMang.IsChecked) return CommunityType.BangMang;
        if (RbZhengDu.IsChecked) return CommunityType.ZhengDu;
        if (RbAiMang.IsChecked) return CommunityType.AiMang;
        return CommunityType.Tatans;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var type = GetSelectedCommunity();

        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            await DisplayAlert("提示", "请输入用户名和密码", "确定");
            return;
        }

        LoginButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;

        try
        {
            // 切换为当前社区对应的 API 服务实例，登录态保留在缓存实例中
            ApiServiceFactory.SetCurrent(type);
            var api = ApiServiceFactory.Current;

            var ok = await api.LoginAsync(username, password);
            if (ok)
            {
                _prefs.SetCurrentCommunity(type);
                _prefs.SetToken(type, api.GetToken());

                await DisplayAlert("成功", $"已登录 {api.DisplayName}", "确定");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("登录失败", "用户名或密码错误，请重试", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("登录错误", ex.Message, "确定");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
        }
    }
}
