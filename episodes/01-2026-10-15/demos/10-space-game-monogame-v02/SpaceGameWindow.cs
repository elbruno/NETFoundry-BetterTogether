/*
 * Goal: Present the deterministic board as a streaming-friendly MonoGame finale.
 * A. Load smooth theme art and a large licensed SpriteFont.
 * B. Process keyboard edges independently of asynchronous inference.
 * C. Letterbox a 1920x1080 canvas and render premultiplied sprites.
 */
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;

namespace DecisionSpaceGame;

public sealed class SpaceGameWindow : Microsoft.Xna.Framework.Game
{
    public const int CanvasWidth = 1920;
    public const int CanvasHeight = 1080;
    public const int AtlasCellSize = 384;
    public const int Cell = 44;
    public const int SpriteSize = 96;
    public const int BoardX = 20;
    public const int BoardY = 78;
    public const string WelcomeBrand = ".NET + Foundry  |  Better Together  |  Decision Space Game  |  Grand Finale";
    public const string WelcomeControls = "Enter / Space: play     F: Foundry     O: Ollama     T: light/dark     Q / Esc: quit";

    private readonly Mission mission;
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private SpriteFont titleFont = null!;
    private Texture2D atlas = null!, pixel = null!;
    private Texture2D backgroundLight = null!, backgroundDark = null!, welcomeLight = null!, welcomeDark = null!;
    private RenderTarget2D canvas = null!;
    private KeyboardState previous;
    private bool suppressHeld = true;
    private bool lightTheme = true;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double previousSeconds;

    public SpaceGameWindow(Mission mission)
    {
        this.mission = mission;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = CanvasWidth,
            PreferredBackBufferHeight = CanvasHeight
        };
        Content.RootDirectory = "Content";
        Window.Title = "Better Together: Decision Space Game v02";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        Deactivated += (_, _) => { mission.LoseFocus(); suppressHeld = true; };
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Content.Load<SpriteFont>("Fonts/Hud");
        titleFont = Content.Load<SpriteFont>("Fonts/Title");
        atlas = LoadPng("atlas.png", AtlasCellSize * 3, AtlasCellSize * 3);
        backgroundLight = LoadPng("background-light.png", CanvasWidth, CanvasHeight);
        backgroundDark = LoadPng("background-dark.png", CanvasWidth, CanvasHeight);
        welcomeLight = LoadPng("welcome-light.png", CanvasWidth, CanvasHeight);
        welcomeDark = LoadPng("welcome-dark.png", CanvasWidth, CanvasHeight);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        canvas = new RenderTarget2D(GraphicsDevice, CanvasWidth, CanvasHeight);
    }

    private Texture2D LoadPng(string name, int width, int height)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", name);
        if (!File.Exists(path)) throw new FileNotFoundException($"Required game art missing: Content/{name}");
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);
        if (texture.Width != width || texture.Height != height)
        {
            texture.Dispose();
            throw new InvalidDataException($"Content/{name} must be {width}x{height}.");
        }
        return texture;
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        bool Edge(Keys key) => keys.IsKeyDown(key) && !previous.IsKeyDown(key);
        if (IsActive && (Edge(Keys.Q) || Edge(Keys.Escape))) { mission.Finish("quit"); Exit(); }
        if (IsActive)
        {
            if (Edge(Keys.T)) lightTheme = !lightTheme;
            if (Edge(Keys.N)) { mission.Restart(); suppressHeld = true; }
            else if (Edge(Keys.M)) { mission.Select("manual"); suppressHeld = true; }
            else if (Edge(Keys.F)) { mission.Select("foundry"); suppressHeld = true; }
            else if (Edge(Keys.O)) { mission.Select("ollama"); suppressHeld = true; }
            else if (mission.Welcome && (Edge(Keys.Enter) || Edge(Keys.Space)))
            {
                mission.Select("manual");
                suppressHeld = true;
            }
            else if (Edge(Keys.P)) { mission.TogglePause(); suppressHeld = true; }
        }
        bool actionHeld = keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.D) ||
            keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.Right) ||
            keys.IsKeyDown(Keys.Space) || keys.IsKeyDown(Keys.S);
        if (!actionHeld) suppressHeld = false;
        GameAction action = GameAction.Stop;
        if (IsActive && !suppressHeld)
            action = keys.IsKeyDown(Keys.Space) ? GameAction.Fire :
                keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A) ? GameAction.MoveLeft :
                keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D) ? GameAction.MoveRight : GameAction.Stop;
        double now = clock.Elapsed.TotalSeconds;
        mission.Update(gameTime.ElapsedGameTime.TotalSeconds, action, now - previousSeconds);
        previousSeconds = now;
        previous = keys;
        base.Update(gameTime);
    }

    public static Rectangle AtlasCell(int index) =>
        new(index % 3 * AtlasCellSize, index / 3 * AtlasCellSize, AtlasCellSize, AtlasCellSize);

    public static Rectangle Letterbox(int width, int height)
    {
        float scale = Math.Min(width / (float)CanvasWidth, height / (float)CanvasHeight);
        int w = (int)(CanvasWidth * scale), h = (int)(CanvasHeight * scale);
        return new((width - w) / 2, (height - h) / 2, w, h);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(canvas);
        GraphicsDevice.Clear(lightTheme ? new Color(226, 236, 248) : Color.Black);
        batch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearClamp);
        var backdrop = mission.Welcome
            ? (lightTheme ? welcomeLight : welcomeDark)
            : (lightTheme ? backgroundLight : backgroundDark);
        batch.Draw(backdrop, new Rectangle(0, 0, CanvasWidth, CanvasHeight), Color.White);
        if (mission.Welcome) DrawWelcome();
        else DrawMission();
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(lightTheme ? new Color(226, 236, 248) : Color.Black);
        batch.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Opaque);
        batch.Draw(canvas, Letterbox(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
        batch.End();
        base.Draw(gameTime);
    }

    private void DrawWelcome()
    {
        Panel(new Rectangle(0, 0, CanvasWidth, 72), 0.82f);
        Title(WelcomeBrand, 36, 18, Accent);
        Panel(new Rectangle(0, 968, CanvasWidth, 112), 0.9f);
        Text(WelcomeControls, 36, 984, Ink);
        Text("Closing mission, v02. You fly first. No model or network until F / O. T switches theme.", 36, 1028, Accent);
    }

    private void DrawMission()
    {
        var world = mission.World;
        var driver = mission.Driver;
        Panel(new Rectangle(BoardX - 8, BoardY - 8, Game.Width * Cell + 16, Game.Height * Cell + 16), 0.42f);
        Title(".NET + Foundry  |  Better Together: Decision Space Game", 20, 18, Accent);
        Text(lightTheme ? "LIGHT  |  T toggles theme" : "DARK  |  T toggles theme", 1280, 28, Ink);
        foreach (var enemy in world.Enemies) Sprite(1 + world.EnemyDesign(enemy), enemy);
        foreach (var shot in world.Shots) Sprite(4, shot);
        foreach (var shot in world.HostileShots) Sprite(5, shot);
        Sprite(0, new Position(world.PlayerX, Game.PlayerY));
        if (world.ShowingDamage) Sprite(7, new Position(world.PlayerX, Game.PlayerY));
        Panel(new Rectangle(1444, BoardY - 8, 456, Game.Height * Cell + 16), 0.9f);
        int y = BoardY + 8;
        void Line(string text, Color? color = null) { Text(text, 1460, y, color ?? Ink); y += 36; }
        Line(driver.Provider.ToUpperInvariant(), Accent);
        Line(driver.Model.Length > 24 ? driver.Model[..24] : driver.Model);
        Line($"Hull {world.Lives}/3   Score {world.Score}");
        Line($"Invaders {world.Enemies.Count}/14");
        Line(world.ShowingDamage ? "HIT! Hull damaged" :
            world.FireCooldownTicks == 0 ? "FIRE READY" : $"Cooldown {world.FireCooldownTicks} ticks",
            world.ShowingDamage ? Hit : Warn);
        Line($"Action: {world.LastAppliedAction?.ToString() ?? "none"}");
        Line(mission.FocusPaused ? "FOCUS PAUSE: P to resume" : mission.Paused ? "USER PAUSED" :
            driver.HasPendingRequest ? "THINKING: world paused" : mission.Outcome ?? "PLAYING", Accent);
        var stats = driver.Statistics;
        Line($"Wall {mission.FinalWallSeconds ?? mission.WallSeconds:F1}s / sim {world.Tick * .05:F1}s");
        Line($"Ticks {world.Tick}   pauses {stats.UserPauses}");
        Line($"Calls issued/done {stats.Requests}/{stats.Completed}");
        Line($"Applied {stats.Accepted} rejected {stats.Rejected}");
        Line($"Errors {stats.Failed} cancelled {stats.Cancelled}");
        Line($"Latency mean {Format(stats.MeanLatencyMs)} ms");
        Line($"Median / p95 {Format(stats.MedianLatencyMs)}/{Format(stats.P95LatencyMs)}");
        Line($"Kills {world.Score / 10} shots {world.ShotsFired}");
        Line($"Hit rate {(world.ShotsFired == 0 ? "n/a" : $"{100.0 * (world.Score / 10) / world.ShotsFired:F0}%")}");
        Line($"Controls {(stats.Mixed ? "mixed" : stats.Providers)}");
        Line($"Usage coverage / {stats.UsageResponses} replies", Warn);
        void Tokens(string label, Func<ElBruno.AI.Decisions.DecisionUsage, long?> select)
        {
            var (count, coverage) = stats.Tokens(select);
            Line($"{label}: {(count?.ToString() ?? "unknown")} ({coverage})");
        }
        Tokens("Input", u => u.InputTokens);
        Tokens("Output", u => u.OutputTokens);
        Tokens("Total reported", u => u.TotalTokens);
        Tokens("Cached", u => u.CachedTokens);
        Tokens("Reasoning", u => u.ReasoningTokens);
        Text("A/D or arrows: move   Space: fire   S: stop   M/F/O: mode   T: theme   P: pause   N: new   Q/Esc: quit",
            20, 1016, Ink);
        if (driver.Failed || mission.Outcome is not null)
        {
            Panel(new Rectangle(120, 340, 1180, 280), 0.94f);
            Title($"MISSION: {mission.Outcome ?? "provider failure"}", 156, 364, Warn);
            Text($"Score {world.Score}   Hull {world.Lives}   Kills {world.Score / 10}/14", 156, 430);
            Text(driver.Failed ? driver.Status : "Mission complete. Usage and outcome shown at right.", 156, 478);
            Text(driver.Failed ? "M/F/O: explicit recovery   N: new mission   Q: quit" :
                "N: new manual mission     Q: quit", 156, 536, Accent);
        }
        else if (mission.Paused || mission.FocusPaused)
            Title("PAUSED - P to explicitly resume", 360, 470, Warn);
    }

    private Color Ink => lightTheme ? new Color(16, 32, 64) : Color.White;
    private Color Accent => lightTheme ? new Color(0, 86, 158) : Color.Cyan;
    private Color Warn => lightTheme ? new Color(146, 64, 0) : Color.Yellow;
    private Color Hit => lightTheme ? new Color(176, 48, 24) : Color.OrangeRed;
    private static string Format(double? value) => value?.ToString("F0") ?? "n/a";
    private void Panel(Rectangle rect, float alpha) =>
        batch.Draw(pixel, rect, (lightTheme ? Color.White : Color.Black) * (lightTheme ? Math.Max(alpha, 0.72f) : alpha));
    private void Text(string text, int x, int y, Color? color = null) =>
        batch.DrawString(font, text, new Vector2(x, y), color ?? Ink);
    private void Title(string text, int x, int y, Color? color = null) =>
        batch.DrawString(titleFont, text, new Vector2(x, y), color ?? Ink);
    private void Sprite(int index, Position position) => batch.Draw(atlas,
        new Rectangle(
            BoardX + position.X * Cell + (Cell - SpriteSize) / 2,
            BoardY + position.Y * Cell + (Cell - SpriteSize) / 2,
            SpriteSize, SpriteSize),
        AtlasCell(index), Color.White);

    public ValueTask ShutdownAsync() => mission.DisposeAsync();
    public Color[] ReadCanvas()
    {
        var data = new Color[CanvasWidth * CanvasHeight];
        canvas.GetData(data);
        return data;
    }
    protected override void UnloadContent()
    {
        canvas?.Dispose(); atlas?.Dispose();
        backgroundLight?.Dispose(); backgroundDark?.Dispose();
        welcomeLight?.Dispose(); welcomeDark?.Dispose();
        pixel?.Dispose(); batch?.Dispose();
        base.UnloadContent();
    }
}
