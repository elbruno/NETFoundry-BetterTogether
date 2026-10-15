/*
 * Goal: Present the deterministic board as a welcoming MonoGame finale.
 * A. Load explicit runtime art and a licensed SpriteFont.
 * B. Process keyboard edges independently of asynchronous inference.
 * C. Letterbox a logical canvas, render straight-alpha sprites and honest telemetry.
 */
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;

namespace DecisionSpaceGame;

public sealed class SpaceGameWindow : Microsoft.Xna.Framework.Game
{
    private readonly Mission mission;
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private Texture2D atlas = null!, background = null!, welcome = null!, pixel = null!;
    private RenderTarget2D canvas = null!;
    private KeyboardState previous;
    private bool suppressHeld = true;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private double previousSeconds;
    public const string WelcomeBrand = ".NET + Foundry  |  Better Together  |  Decision Space Game  |  Grand Finale";
    public const string WelcomeControls = "Enter / Space: play manual     F: Foundry     O: Ollama     Q / Esc: quit";

    public SpaceGameWindow(Mission mission)
    {
        this.mission = mission;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720
        };
        Content.RootDirectory = "Content";
        Window.Title = "Better Together: Decision Space Game";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        Deactivated += (_, _) => { mission.LoseFocus(); suppressHeld = true; };
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Content.Load<SpriteFont>("Fonts/Hud");
        atlas = LoadPng("atlas.png", 768, 768);
        background = LoadPng("background.png", 1280, 720);
        welcome = LoadPng("welcome.png", 1280, 720);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        canvas = new RenderTarget2D(GraphicsDevice, 1280, 720);
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

    public static Rectangle AtlasCell(int index) => new(index % 3 * 256, index / 3 * 256, 256, 256);
    public static Rectangle Letterbox(int width, int height)
    {
        float scale = Math.Min(width / 1280f, height / 720f);
        int w = (int)(1280 * scale), h = (int)(720 * scale);
        return new((width - w) / 2, (height - h) / 2, w, h);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(canvas);
        GraphicsDevice.Clear(Color.Black);
        batch.Begin(blendState: BlendState.NonPremultiplied, samplerState: SamplerState.PointClamp);
        batch.Draw(mission.Welcome ? welcome : background, new Rectangle(0, 0, 1280, 720), Color.White);
        if (mission.Welcome)
        {
            Panel(new Rectangle(0, 0, 1280, 48), 0.9f);
            Text(WelcomeBrand, 28, 14, Color.Cyan);
            Panel(new Rectangle(0, 642, 1280, 78), 0.95f);
            Text(WelcomeControls, 100, 650, Color.White);
            Text("Closing mission: you fly first. No model or network until F / O.", 240, 681, Color.Cyan);
        }
        else DrawMission();
        batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        batch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Opaque);
        batch.Draw(canvas, Letterbox(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
        batch.End();
        base.Draw(gameTime);
    }

    private void DrawMission()
    {
        var world = mission.World;
        var driver = mission.Driver;
        Panel(new Rectangle(16, 60, 960, 600), 0.55f);
        Text(".NET + Foundry  |  Better Together: Decision Space Game", 16, 16, Color.Cyan);
        foreach (var enemy in world.Enemies) Sprite(1 + world.EnemyDesign(enemy), enemy);
        foreach (var shot in world.Shots) Sprite(4, shot);
        foreach (var shot in world.HostileShots) Sprite(5, shot);
        Sprite(0, new Position(world.PlayerX, Game.PlayerY));
        if (world.ShowingDamage) Sprite(7, new Position(world.PlayerX, Game.PlayerY));
        Panel(new Rectangle(986, 60, 286, 600), 0.93f);
        int y = 70;
        void Line(string text, Color? color = null) { Text(text, 998, y, color ?? Color.White, 0.82f); y += 24; }
        Line(driver.Provider.ToUpperInvariant(), Color.Cyan);
        Line(driver.Model.Length > 30 ? driver.Model[..30] : driver.Model);
        Line($"Hull {world.Lives}/3   Score {world.Score}");
        Line($"Invaders {world.Enemies.Count}/14");
        Line(world.ShowingDamage ? "HIT! Hull damaged" :
            world.FireCooldownTicks == 0 ? "FIRE READY" : $"Cooldown {world.FireCooldownTicks} ticks",
            world.ShowingDamage ? Color.OrangeRed : Color.Yellow);
        Line($"Action: {world.LastAppliedAction?.ToString() ?? "none"}");
        Line(mission.FocusPaused ? "FOCUS PAUSE: P to resume" : mission.Paused ? "USER PAUSED" :
            driver.HasPendingRequest ? "THINKING: world paused" : mission.Outcome ?? "PLAYING", Color.Cyan);
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
        Line($"Usage coverage / {stats.UsageResponses} replies", Color.Yellow);
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
        Text("A/D or arrows: move   Space: fire   S: stop   M/F/O: mode   P: pause   N: new   Q/Esc: quit",
            16, 674, Color.White, 0.9f);
        if (driver.Failed || mission.Outcome is not null)
        {
            Panel(new Rectangle(80, 230, 820, 210), 0.97f);
            Text($"MISSION: {mission.Outcome ?? "provider failure"}", 110, 250, Color.Yellow, 1.2f);
            Text($"Score {world.Score}   Hull {world.Lives}   Kills {world.Score / 10}/14", 110, 294);
            Text(driver.Failed ? driver.Status : "Mission complete. Usage and outcome shown at right.", 110, 334, scale: 0.9f);
            Text(driver.Failed ? "M/F/O: explicit recovery   N: new mission   Q: quit" :
                "N: new manual mission     Q: quit", 110, 384, Color.Cyan);
        }
        else if (mission.Paused || mission.FocusPaused)
            Text("PAUSED - P to explicitly resume", 300, 330, Color.Yellow, 1.3f);
    }

    private static string Format(double? value) => value?.ToString("F0") ?? "n/a";
    private void Panel(Rectangle rect, float alpha) => batch.Draw(pixel, rect, Color.Black * alpha);
    private void Text(string text, int x, int y, Color? color = null, float scale = 1) =>
        batch.DrawString(font, text, new Vector2(x, y), color ?? Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    private void Sprite(int index, Position position) => batch.Draw(atlas,
        new Rectangle(16 + position.X * 30, 60 + position.Y * 30, 30, 30), AtlasCell(index), Color.White);

    public ValueTask ShutdownAsync() => mission.DisposeAsync();
    public Color[] ReadCanvas()
    {
        var data = new Color[1280 * 720];
        canvas.GetData(data);
        return data;
    }
    protected override void UnloadContent()
    {
        canvas?.Dispose(); atlas?.Dispose(); background?.Dispose(); welcome?.Dispose();
        pixel?.Dispose(); batch?.Dispose();
        base.UnloadContent();
    }
}
