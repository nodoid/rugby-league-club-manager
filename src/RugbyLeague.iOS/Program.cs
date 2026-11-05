using CoreGraphics;
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

        // MonoGame builds its UIWindow in the platform constructor, so it exists before Run(). On
        // iPadOS 26+ apps open in a resizable window, so the window has to match the scene rather
        // than the full screen or the canvas is clipped.
        if (game.Services.GetService(typeof(UIWindow)) is UIWindow window)
        {
            window.WindowScene = scene;
            window.Frame = scene.CoordinateSpace.Bounds;
        }

        game.Run();

        var created = game.Services.GetService(typeof(UIWindow)) as UIWindow;
        if (created is not null)
        {
            created.Frame = scene.CoordinateSpace.Bounds;
            created.MakeKeyAndVisible();
        }



        // MonoGame's touch panel does not receive the window's touches on iOS 26+, so a transparent
        // view above the game view forwards them into the shared input state instead.
        if (game.Services.GetService(typeof(UIViewController)) is UIViewController controller)
            controller.View.AddSubview(new TouchPumpView(controller.View.Bounds) { Game = game });
    }
}

/// <summary>
/// A transparent view above the game that turns UIKit touches into game input. Positions are
/// reported in render pixels so they line up with the game's virtual canvas.
/// </summary>
[Register("TouchPumpView")]
internal sealed class TouchPumpView : UIView
{
    public TouchPumpView(CGRect frame) : base(frame)
    {
        Opaque = false;
        BackgroundColor = UIColor.Clear;
        MultipleTouchEnabled = true;
        AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
    }

    public RugbyGame? Game { get; set; }

    public override void TouchesBegan(NSSet touches, UIEvent? evt) => Report(touches, down: true);

    public override void TouchesMoved(NSSet touches, UIEvent? evt) => Report(touches, down: true);

    public override void TouchesEnded(NSSet touches, UIEvent? evt) => Report(touches, down: false);

    public override void TouchesCancelled(NSSet touches, UIEvent? evt) => Report(touches, down: false);

    private void Report(NSSet touches, bool down)
    {
        // This view shares the game view's coordinate space, which is in points on screen.
        var host = Superview?.Bounds ?? Bounds;
        var viewport = Game?.View;
        var scaleX = viewport is not null && host.Width > 0 ? viewport.ScreenWidth / (float)host.Width : 1f;
        var scaleY = viewport is not null && host.Height > 0 ? viewport.ScreenHeight / (float)host.Height : 1f;

        foreach (UITouch touch in touches)
        {
            var location = touch.LocationInView(this);
            var position = new Microsoft.Xna.Framework.Vector2((float)location.X * scaleX, (float)location.Y * scaleY);
            Game?.Input?.InjectTouch(touch.Handle.GetHashCode(), position, down);
        }
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
