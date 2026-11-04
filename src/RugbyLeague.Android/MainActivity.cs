using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using RugbyLeague.Game;

namespace RugbyLeague.Android;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher_round",
    Theme = "@style/AppTheme",
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                           ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AndroidGameActivity
{
    private RugbyGame? _game;

    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);

        _game = new RugbyGame();
        var view = (View?)_game.Services.GetService(typeof(View));
        SetContentView(view);
        _game.Run();
    }
}
