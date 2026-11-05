using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RugbyLeague.Core.Careers;
using RugbyLeague.Core.Persistence;
using RugbyLeague.Game.Demo;
using RugbyLeague.Game.Graphics;
using RugbyLeague.Game.Input;
using RugbyLeague.Game.Screens;
using RugbyLeague.Game.UI;

namespace RugbyLeague.Game;

/// <summary>
/// The shared MonoGame host. Every platform head instantiates this class; the only per-platform
/// difference is the graphics backend supplied by the MonoGame framework package.
/// </summary>
public sealed class RugbyGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private float _noticeTimer;

    public RugbyGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = false,
            HardwareModeSwitch = false
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.FromMilliseconds(30);

        Window.AllowUserResizing = true;
        Window.Title = "Rugby League Club Manager";
    }

    public VirtualViewport View { get; } = new();
    public Theme Theme { get; private set; } = null!;
    public InputState Input { get; private set; } = null!;
    public UiContext Ui { get; private set; } = null!;
    public ScreenManager Screens { get; } = new();

    public Career? Career { get; set; }
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Runs the scripted store-capture tour when the build is launched with RL_DEMO/--demo.</summary>
    public DemoTour? Demo { get; private set; }

    public static bool DemoTourRequested =>
        Environment.GetEnvironmentVariable("RL_DEMO") is "1" or "true" ||
        Environment.GetCommandLineArgs().Any(argument => argument == "--demo");

    public bool IsDesktop =>
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    public bool HasSave => SaveGame.Exists();

    public string BuildLabel => $"v1.0  ·  {DateTime.UtcNow:yyyy.MM.dd}";

    protected override void Initialize()
    {
        if (IsDesktop)
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Theme = new Theme
        {
            Small = Content.Load<SpriteFont>("Fonts/FontSmall"),
            Body = Content.Load<SpriteFont>("Fonts/FontBody"),
            Subhead = Content.Load<SpriteFont>("Fonts/FontSubhead"),
            Heading = Content.Load<SpriteFont>("Fonts/FontHeading"),
            Title = Content.Load<SpriteFont>("Fonts/FontTitle"),
            PanelTexture = Content.Load<Texture2D>("Textures/panel"),
            ButtonTexture = Content.Load<Texture2D>("Textures/button"),
            BallTexture = Content.Load<Texture2D>("Textures/ball"),
            MarkerTexture = Content.Load<Texture2D>("Textures/marker"),
            ShieldTexture = Content.Load<Texture2D>("Textures/shield"),
            LogoTexture = Content.Load<Texture2D>("Textures/logo"),
            SplashTexture = Content.Load<Texture2D>("Textures/splash"),
            GrassTexture = Content.Load<Texture2D>("Textures/grass"),
            PitchTexture = Content.Load<Texture2D>("Textures/pitch"),
            TrophyTexture = Content.Load<Texture2D>("Textures/trophy"),
            BackgroundTexture = Content.Load<Texture2D>("Textures/background"),
            Pixel = _pixel
        };

        Input = new InputState(View);
        Ui = new UiContext(Theme, _spriteBatch, Input, View);

        if (DemoTourRequested) Demo = new DemoTour(this);

        Screens.Push(new SplashScreen(this));
    }

    protected override void Update(GameTime gameTime)
    {
        View.Update(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Money is shown in the player's own currency, so it changes with promotion and relegation.
        Graphics.Theme.CurrencySymbol = Career?.CurrencySymbol ?? "$";

        Demo?.Update(gameTime);

        Input.Update(gameTime);
        Ui.Update();

        if (_noticeTimer > 0) _noticeTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

        Screens.Update(gameTime);

        if (Input.IsNewKeyPress(Microsoft.Xna.Framework.Input.Keys.F12))
            SaveScreenshot();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Theme.BackgroundDeep);

        Screens.Current?.DrawWorld(GraphicsDevice);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, View.Transform);

        Screens.Current?.Draw();

        if (_noticeTimer > 0 && Notice.Length > 0) DrawNotice();

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawNotice()
    {
        var font = Theme.Body;
        var width = Math.Min((int)View.Width - 80, (int)font.MeasureString(Notice).X + 56);
        var rectangle = new Rectangle((int)(View.Width / 2 - width / 2), (int)View.Height - 110, width, 52);

        var alpha = Math.Clamp(_noticeTimer * 2f, 0f, 1f) * 0.96f;
        Ui.NineSlice(rectangle, new Color(18, 34, 44) * alpha);
        Ui.Border(rectangle, Theme.Accent * alpha, 2);
        Ui.TextIn(Notice, rectangle, Theme.Text * alpha, TextSize.Body, TextAlign.Center);
    }

    public void ShowNotice(string message, float seconds = 3.5f)
    {
        Notice = message;
        _noticeTimer = seconds;
    }

    public void SaveCareer()
    {
        if (Career is null) return;

        try
        {
            SaveGame.Save(Career);
            ShowNotice("Career saved.");
        }
        catch (Exception exception)
        {
            ShowNotice($"Could not save: {exception.Message}", 5f);
        }
    }

    public bool LoadCareer()
    {
        var loaded = SaveGame.TryLoad();
        if (loaded is null)
        {
            ShowNotice("No save game found.");
            return false;
        }

        Career = loaded;
        return true;
    }

    public void StartCareer(Career career)
    {
        Career = career;
        SaveCareer();
        Screens.ReplaceAll(new CareerHubScreen(this));
    }

    public void ExitToMenu()
    {
        SaveCareer();
        Screens.ReplaceAll(new MainMenuScreen(this));
    }

    public void SaveScreenshot()
    {
        try
        {
            var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[width * height];
            GraphicsDevice.GetBackBufferData(data);

            using var texture = new Texture2D(GraphicsDevice, width, height);
            texture.SetData(data);

            var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"shot-{DateTime.Now:HHmmss}.png");

            using var stream = File.Create(path);
            texture.SaveAsPng(stream, width, height);
            ShowNotice($"Screenshot saved to {path}", 4f);
        }
        catch (Exception exception)
        {
            ShowNotice($"Screenshot failed: {exception.Message}");
        }
    }
}
