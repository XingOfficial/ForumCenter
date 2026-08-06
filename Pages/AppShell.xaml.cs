using ForumCenter.Pages;

namespace ForumCenter;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // 注册子页面路由，使 Shell.Current.GoToAsync 可正常跳转
        Routing.RegisterRoute(nameof(PostDetailPage), typeof(PostDetailPage));
        Routing.RegisterRoute(nameof(PostCreatePage), typeof(PostCreatePage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
    }
}
