using ForumCenter.Pages;

namespace ForumCenter;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        
        Routing.RegisterRoute(nameof(PostDetailPage), typeof(PostDetailPage));
        Routing.RegisterRoute(nameof(PostCreatePage), typeof(PostCreatePage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
    }
}
