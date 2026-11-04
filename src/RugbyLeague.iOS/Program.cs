using Foundation;
using RugbyLeague.Game;
using UIKit;

namespace RugbyLeague.iOS;

/// <summary>
/// iOS 27 requires every UIKit app to adopt the scene life cycle. The game window is created by
/// MonoGame, so we hand it the connected UIWindowScene before the run loop starts.
/// </summary>
[Register("AppDelegate")]
internal sealed class AppDelegate : UIApplicationDelegate
{
    internal static RugbyGame? Game { get; private set; }

    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions) => true;

    internal static void StartGame(UIWindowScene scene)
    {
        if (Game is not null) return;

        var game = new RugbyGame();
        Game = game;

        // MonoGame builds its UIWindow in the platform constructor, so it exists before Run().
        if (game.Services.GetService(typeof(UIWindow)) is UIWindow window)
            window.WindowScene = scene;

        game.Run();

        if (game.Services.GetService(typeof(UIWindow)) is UIWindow created)
            created.MakeKeyAndVisible();
    }
}

[Register("SceneDelegate")]
internal sealed class SceneDelegate : UIWindowSceneDelegate
{
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is UIWindowScene windowScene)
            AppDelegate.StartGame(windowScene);
    }
}
