/*
 * Goal: Let bounded decisions control the platformer without blocking rendering.
 * A. Keep manual controls as the default and handle mode changes on the game thread.
 * B. Freeze simulation while one provider request runs asynchronously.
 * C. Validate and apply responses only to the current level generation.
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Platformer2D.Core;
using Platformer2D.Core.Effects;
using Platformer2D.Core.Inputs;
using Platformer2D.Core.Localization;
using Platformer2D.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using ElBruno.AI.Decisions;
using PlatformerDecisions;

namespace Platformer2D.Screens;

/// <summary>
/// This screen implements the actual game logic and manages the gameplay experience.
/// It controls level loading, player interaction, game state updates, and rendering
/// for the active gameplay session.
/// </summary>
partial class GameplayScreen : GameScreen
{
    /// <summary>
    /// Content manager for loading and managing game assets.
    /// </summary>
    ContentManager content;

    /// <summary>
    /// Controls the opacity of the pause screen overlay when the game is paused.
    /// </summary>
    float pauseAlpha;

    /// <summary>
    /// SpriteBatch instance used for rendering 2D elements.
    /// </summary>
    private SpriteBatch spriteBatch;

    /// <summary>
    /// Current level index (zero-based) in the game progression.
    /// </summary>
    private int levelIndex = -1;

    /// <summary>
    /// Reference to the currently active Level object.
    /// </summary>
    private Level level;

    /// <summary>
    /// Manager for particle effects in the game.
    /// </summary>
    private ParticleManager particleManager;

    /// <summary>
    /// Manager for leaderboard data and high scores.
    /// </summary>
    private SettingsManager<Platformer2DLeaderboard> leaderboardManager;

    /// <summary>
    /// Text message displayed at the end of a level (completion, failure, etc.).
    /// </summary>
    private string endOfLevelMessage;

    /// <summary>
    /// Tracks the display state of the end-of-level message.
    /// </summary>
    private EndOfLevelMessageState endOfLevelMessgeState;

    /// <summary>
    /// Spacing in pixels between text elements and screen edges.
    /// </summary>
    private const int textEdgeSpacing = 10;

    private KeyboardState previousKeyboard;
    private string provider = "manual";
    private string model = "keyboard";
    private string status = "MANUAL CONTROLS";
    private string lastAction = "none";
    private ControlAction activeAction;
    private int actionStepsRemaining;
    private int lastAppliedSteps;
    private bool userPaused;
    private bool failed;
    private bool quitting;
    private bool quitFinalized;
    private long generation;
    private long requestGeneration;
    private long runId;
    private long requestRunId;
    private Task<DecisionAttempt> pending;
    private CancellationTokenSource requestCancellation;
    private readonly Dictionary<string, IDecisionClient> clients = new();
    private readonly Stopwatch aiWallClock = new();
    private readonly Stopwatch sessionWallClock = new();
    private TimeSpan simulationTotal;
    private GameTime simulationGameTime = new(TimeSpan.Zero, TimeSpan.Zero);
    private int requestCount;
    private int acceptedCount;
    private int rejectedCount;
    private int errorCount;
    private int cancelledCount;
    private double lastDurationMs;
    private double? lastProbability;
    private readonly DecisionUsageLedger usageLedger = new();
    private readonly HashSet<string> runModes = new();
    private int userPauses;

    internal Level DemoLevel => level;
    internal int Requests => requestCount;
    internal int Accepted => acceptedCount;
    internal bool Pending => pending != null;
    internal bool Failed => failed;
    internal string Status => status;
    internal DecisionUsageLedger Usage => usageLedger;
    internal void RegisterClient(string name, IDecisionClient client) => clients.Add(name, client);

    private bool IsTerminal => level != null && (level.ReachedExit || !level.Player.IsAlive ||
        level.TimeTaken >= level.MaximumTimeToCompleteLevel);
    private bool IsSimulationFrozen => userPaused || quitting ||
        (provider != "manual" && (pending != null || actionStepsRemaining == 0 || failed || IsTerminal));

    /// <summary>
    /// Initializes a new instance of the GameplayScreen class.
    /// Sets up transition times for smooth screen changes.
    /// </summary>
    public GameplayScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(1.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    /// <summary>
    /// Loads all content required for the gameplay screen.
    /// This includes loading the initial level, music, and acquiring necessary services.
    /// </summary>
    public override void LoadContent()
    {
        base.LoadContent();

        if (content == null)
            content = new ContentManager(ScreenManager.Game.Services, "Content");

        spriteBatch = ScreenManager.SpriteBatch;
        sessionWallClock.Start();

        MediaPlayer.IsRepeating = true;
        MediaPlayer.Play(content.Load<Song>("Sounds/Music"));

        particleManager ??= ScreenManager.Game.Services.GetService<ParticleManager>();

        leaderboardManager ??= ScreenManager.Game.Services.GetService<SettingsManager<Platformer2DLeaderboard>>();

        LoadNextLevel();

        // once the load has finished, we use ResetElapsedTime to tell the game's
        // timing mechanism that we have just finished a very long frame, and that
        // it should not try to catch up.
        ScreenManager.Game.ResetElapsedTime();
    }

    /// <summary>
    /// Loads the next level in sequence, cycling back to the first level after the last one.
    /// Handles level disposal, initialization, and leaderboard setup.
    /// </summary>
    private void LoadNextLevel()
    {
        // move to the next level
        levelIndex = (levelIndex + 1) % Level.NUMBER_OF_LEVELS;

        // Unloads the content for the current level before loading the next one.
        if (level != null)
            level.Dispose();

        // Load the level.
        var levelPath = string.Format("Content/Levels/{0:00}.txt", levelIndex);
        level = new Level(ScreenManager, levelPath, levelIndex);
        level.ParticleManager = particleManager;

        var levelFileName = Path.GetFileName(levelPath);
        var leaderboardFileName = Path.ChangeExtension(levelFileName, ".json");
        leaderboardManager.Storage.SettingsFileName = leaderboardFileName;
        level.LeaderboardManager = leaderboardManager;

        endOfLevelMessgeState = EndOfLevelMessageState.NotShowing;
    }

    private void RestartDemoLevel()
    {
        levelIndex = -1;
        simulationTotal = TimeSpan.Zero;
        simulationGameTime = new GameTime(TimeSpan.Zero, TimeSpan.Zero);
        LoadNextLevel();
        ScreenManager.Game.ResetElapsedTime();
    }

    /// <summary>
    /// Reloads the current level after a failure.
    /// Decrements the level index and calls LoadNextLevel to reset the current level.
    /// </summary>
    private void ReloadCurrentLevel()
    {
        --levelIndex;
        LoadNextLevel();
    }

    /// <summary>
    /// Unloads all content used by the gameplay screen when it's no longer needed.
    /// </summary>
    public override void UnloadContent()
    {
        generation++;
        requestCancellation?.Cancel();
        DisposeClients();
        level?.Dispose();
        content.Unload();
    }

    /// <summary>
    /// Updates the game state, including level logic, end-of-level conditions, and screen transitions.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values for frame-based updates.</param>
    /// <param name="otherScreenHasFocus">Indicates if another screen has focus.</param>
    /// <param name="coveredByOtherScreen">Indicates if this screen is covered by another screen.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus,
                                                   bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, false);

        // Gradually fade in or out depending on whether we are covered by the pause screen.
        if (coveredByOtherScreen)
            pauseAlpha = Math.Min(pauseAlpha + 1f / 32, 1);
        else
            pauseAlpha = Math.Max(pauseAlpha - 1f / 32, 0);

        level.Paused = !IsActive || IsSimulationFrozen;

        if (IsActive)
        {
            if (level.ParticleManager.Finished)
            {
                switch (endOfLevelMessgeState)
                {
                    case EndOfLevelMessageState.NotShowing:
                        if (level.TimeTaken == level.MaximumTimeToCompleteLevel)
                        {
                            if (level.ReachedExit)
                            {
                                endOfLevelMessage = GetLevelStats(Resources.LevelCompleted);
                            }
                            else
                            {
                                endOfLevelMessage = GetLevelStats(Resources.TimeRanOut);
                            }

                            endOfLevelMessgeState = EndOfLevelMessageState.Show;
                        }
                        else if (!level.Player.IsAlive)
                        {
                            endOfLevelMessage = GetLevelStats(Resources.YouDied);
                            endOfLevelMessgeState = EndOfLevelMessageState.Show;
                        }
                        break;
                    case EndOfLevelMessageState.Showing:
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Generates a formatted message with level statistics for display at the end of a level.
    /// </summary>
    /// <param name="messageTitle">The title message indicating completion or failure state.</param>
    /// <returns>A formatted string containing the level statistics.</returns>
    private string GetLevelStats(string messageTitle)
    {
        var message = messageTitle + Environment.NewLine + Environment.NewLine;

        if (level.NewHighScore)
            message += Resources.NewHighScore + Environment.NewLine + Environment.NewLine;

        message +=
            Resources.Score + ": " + level.Score + Environment.NewLine +
            Resources.Time + ": " + level.TimeTaken + Environment.NewLine +
            Resources.GemsCollected + $": {level.GemsCollected:D2}/ {level.GemsCount:D2}";

        return message;
    }

    /// <summary>
    /// Processes player input and updates game state accordingly.
    /// Handles pausing, hot level reloading, level continuation, and player actions.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="inputState">Current input state for all input devices.</param>
    /// <exception cref="ArgumentNullException">Thrown if inputState is null.</exception>
    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        ArgumentNullException.ThrowIfNull(inputState);

        KeyboardState keyboard = Keyboard.GetState();
        HandleDemoKeys(keyboard);
        PollDecision();

        if (quitting)
        {
            previousKeyboard = keyboard;
            return;
        }

        if (provider != "manual" && aiWallClock.Elapsed >= TimeSpan.FromSeconds(300) && !failed)
        {
            generation++;
            CancelPending();
            actionStepsRemaining = 0;
            failed = true;
            status = "AI wall-time limit reached. Press M or N.";
        }

        // The engine remains on its fixed clock. Only this call advances gameplay.
        if (IsActive && !userPaused && !IsTerminal && !failed &&
            (provider == "manual" || (pending == null && actionStepsRemaining > 0)))
        {
            float? movement = null;
            bool? jump = null;
            if (provider != "manual")
            {
                movement = activeAction.Movement;
                jump = activeAction.Jump;
            }
            simulationTotal += ControlAction.StepDuration;
            runModes.Add(provider);
            simulationGameTime = new GameTime(simulationTotal, ControlAction.StepDuration, gameTime.IsRunningSlowly);
            level.Update(simulationGameTime, inputState, ScreenManager.Game.Window.CurrentOrientation,
                movementOverride: movement, jumpOverride: jump);
            if (provider != "manual" && actionStepsRemaining > 0)
            {
                actionStepsRemaining--;
                lastAppliedSteps = ControlAction.SimulationSteps - actionStepsRemaining;
            }
            if (IsTerminal)
            {
                actionStepsRemaining = 0;
                status = level.ReachedExit ? "VICTORY" :
                    !level.Player.IsAlive ? "DEFEAT" : "TIME EXPIRED";
                CancelPending();
            }
        }
        else
        {
            simulationGameTime = new GameTime(simulationTotal, TimeSpan.Zero, gameTime.IsRunningSlowly);
        }

        if (provider != "manual" && !userPaused && !failed && !IsTerminal &&
            pending == null && actionStepsRemaining == 0)
        {
            if (aiWallClock.Elapsed >= TimeSpan.FromSeconds(300) || requestCount >= 600)
            {
                failed = true;
                status = "AI run limit reached. Press M for manual control or N to restart.";
            }
            else
            {
                StartDecision();
            }
        }

        previousKeyboard = keyboard;
    }

    /// <summary>
    /// Renders the gameplay elements, including the level, UI, and transition effects.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values for frame-based rendering.</param>
    public override void Draw(GameTime gameTime)
    {
        // This game has a blue background. Why? Because!
        ScreenManager.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 0, 0);

        level.Draw(simulationGameTime, spriteBatch);

        base.Draw(gameTime);

        DrawDecisionHud();

        // If the game is transitioning on or off, fade it out to black.
        if (TransitionPosition > 0 || pauseAlpha > 0)
        {
            float alpha = MathHelper.Lerp(1f - TransitionAlpha, 1f, pauseAlpha / 2);

            ScreenManager.FadeBackBufferToBlack(alpha);
        }
    }

    internal void HandleDemoKeys(KeyboardState keyboard)
    {
        if (NewKey(keyboard, Keys.Q) || NewKey(keyboard, Keys.Escape))
        {
            RequestQuit();
            return;
        }
        if (NewKey(keyboard, Keys.M))
        {
            generation++;
            CancelPending();
            provider = "manual";
            model = "keyboard";
            activeAction = default;
            actionStepsRemaining = 0;
            userPaused = false;
            failed = false;
            aiWallClock.Stop();
            status = "MANUAL CONTROLS";
            ScreenManager.Game.ResetElapsedTime();
        }
        if (NewKey(keyboard, Keys.F)) SelectProvider("foundry");
        if (NewKey(keyboard, Keys.O)) SelectProvider("ollama");
        if (NewKey(keyboard, Keys.P))
        {
            userPaused = !userPaused;
            if (userPaused) userPauses++;
            if (!userPaused) ScreenManager.Game.ResetElapsedTime();
            status = userPaused ? "USER PAUSED" :
                pending != null ? "THINKING - SIMULATION PAUSED" :
                provider == "manual" ? "MANUAL CONTROLS" : "AI RESUMED";
        }
        if (NewKey(keyboard, Keys.N))
        {
            generation++;
            CancelPending();
            actionStepsRemaining = 0;
            activeAction = default;
            failed = false;
            userPaused = false;
            requestCount = acceptedCount = rejectedCount = errorCount = cancelledCount = 0;
            lastDurationMs = 0;
            lastProbability = null;
            usageLedger.Clear();
            runModes.Clear();
            userPauses = 0;
            lastAppliedSteps = 0;
            runId++;
            if (provider == "manual")
                aiWallClock.Reset();
            else
                aiWallClock.Restart();
            lastAction = "none";
            status = "RESTARTED";
            RestartDemoLevel();
        }
    }

    private bool NewKey(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !previousKeyboard.IsKeyDown(key);

    private void SelectProvider(string selected)
    {
        generation++;
        CancelPending();
        actionStepsRemaining = 0;
        activeAction = default;
        userPaused = false;
        failed = false;
        provider = selected;
        try
        {
            if (!clients.ContainsKey(selected))
                clients[selected] = DecisionProviderFactory.Create(selected);
            model = DecisionProviderFactory.ModelLabel(selected);
            if (!aiWallClock.IsRunning) aiWallClock.Start();
            status = pending == null ? "THINKING" : "WAITING FOR PRIOR REQUEST CANCELLATION";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            failed = true;
            status = "Configuration missing or invalid. Press M for manual control.";
            errorCount++;
        }
    }

    private void StartDecision()
    {
        if (!clients.ContainsKey(provider))
        {
            try { clients[provider] = DecisionProviderFactory.Create(provider); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                failed = true;
                errorCount++;
                status = "Configuration missing or invalid. Press M for manual control.";
                return;
            }
        }
        string snapshot = DecisionObservation.AddControlContext(level.CaptureDecisionObservation(), lastAction,
            lastAppliedSteps, level.Player.JumpButtonHeld);
        requestGeneration = generation;
        requestRunId = runId;
        requestCancellation = new CancellationTokenSource();
        requestCount++;
        status = "THINKING - SIMULATION PAUSED";
        IDecisionClient activeClient = clients[provider];
        CancellationToken token = requestCancellation.Token;
        pending = Task.Run(() => DecisionCalls.ChooseAsync(activeClient, snapshot, token));
    }

    private void PollDecision()
    {
        if (pending == null || !pending.IsCompleted) return;
        DecisionAttempt attempt = pending.GetAwaiter().GetResult();
        bool cancelled = requestCancellation!.IsCancellationRequested;
        pending = null;
        requestCancellation.Dispose();
        requestCancellation = null;
        bool belongsToCurrentRun = requestRunId == runId;
        if (belongsToCurrentRun)
        {
            lastDurationMs = attempt.DurationMs;
            if (attempt.Decision != null) usageLedger.Record(attempt.Decision.Usage);
        }

        if (quitting)
        {
            if (belongsToCurrentRun) cancelledCount++;
            FinishQuit();
            return;
        }

        if (requestGeneration != generation || provider == "manual")
        {
            if (belongsToCurrentRun)
            {
                if (cancelled || attempt.Decision == null) cancelledCount++;
                else rejectedCount++;
            }
            status = provider == "manual" ? "MANUAL CONTROLS" : "OLD RESPONSE DISCARDED";
            return;
        }
        if (cancelled || attempt.Error != null || attempt.Decision == null)
        {
            if (cancelled) cancelledCount++;
            else errorCount++;
            failed = true;
            status = cancelled ? "REQUEST CANCELLED. Press M or N." : "REQUEST FAILED. Press M or N to retry.";
            actionStepsRemaining = 0;
            return;
        }

        ChoiceDecision decision = attempt.Decision;
        if (!ControlAction.IsLegal(decision, out ControlAction action))
        {
            rejectedCount++;
            failed = true;
            status = "ILLEGAL DECISION REJECTED. Press M or N.";
            return;
        }
        acceptedCount++;
        activeAction = action;
        actionStepsRemaining = ControlAction.SimulationSteps;
        lastAction = action.Label;
        lastAppliedSteps = 0;
        lastProbability = decision.Probabilities.TryGetValue(decision.Choice, out double probability)
            ? probability : null;
        status = userPaused ? "USER PAUSED / DECISION READY" : $"APPLIED {action.Label}";
        ScreenManager.Game.ResetElapsedTime();
    }

    private void CancelPending()
    {
        if (pending != null && !pending.IsCompleted)
        {
            requestCancellation?.Cancel();
            return;
        }
        if (pending != null)
            PollDecision();
    }

    private void RequestQuit()
    {
        generation++;
        quitting = true;
        CancelPending();
        status = "QUITTING - CANCELLING REQUEST";
        if (pending == null) FinishQuit();
    }

    private void FinishQuit()
    {
        if (quitFinalized) return;
        quitFinalized = true;
        DisposeClients();
        ScreenManager.Game.Exit();
    }

    private void DisposeClients()
    {
        foreach (IDecisionClient client in clients.Values)
            if (client is IDisposable disposable) disposable.Dispose();
        clients.Clear();
    }

    private void DrawDecisionHud()
    {
        string TokenValue(string name, Func<DecisionUsage, long?> selector)
        {
            (long? total, int coverage) = usageLedger.Sum(selector);
            return $"{name}: {(total?.ToString() ?? "unknown")} ({coverage}/{usageLedger.Responses})";
        }

        string label = provider.ToUpperInvariant() + " / " + model;
        string confidence = lastProbability.HasValue ? $" | reported p={lastProbability.Value:F2}" : "";
        string top = $"{label}   {status}   last={lastAction}{confidence}";
        top += $"   pauses={userPauses}" + (runModes.Count > 1 ? " / MIXED CONTROLS" : "");
        string details = $"player=({level.Player.Position.X:F0},{level.Player.Position.Y:F0}) gems={level.GemsCollected}/{level.GemsCount} " +
            $"sim={simulationTotal.TotalSeconds:F1}s session-wall={sessionWallClock.Elapsed.TotalSeconds:F1}s ai-wall={aiWallClock.Elapsed.TotalSeconds:F1}s " +
            $"requests={requestCount} accepted={acceptedCount} rejected={rejectedCount} errors={errorCount} cancelled={cancelledCount} " +
            $"latency={lastDurationMs:F0}ms";
        string usage = $"{TokenValue("Input", item => item.InputTokens)}   {TokenValue("Output", item => item.OutputTokens)}";
        string otherUsage = $"{TokenValue("Total", item => item.TotalTokens)}   {TokenValue("Cached", item => item.CachedTokens)}   " +
            $"{TokenValue("Reasoning", item => item.ReasoningTokens)}";

        spriteBatch.Begin(transformMatrix: ScreenManager.GlobalTransformation);
        spriteBatch.Draw(content.Load<Texture2D>("Sprites/blank"), new Rectangle(0, 0, 1280, 124), Color.Black * 0.78f);
        spriteBatch.DrawString(content.Load<SpriteFont>("Fonts/Hud"), top, new Vector2(textEdgeSpacing, 8), Color.White);
        spriteBatch.DrawString(content.Load<SpriteFont>("Fonts/Hud"), details, new Vector2(textEdgeSpacing, 34), Color.LightGreen);
        spriteBatch.DrawString(content.Load<SpriteFont>("Fonts/Hud"), usage, new Vector2(textEdgeSpacing, 58), Color.LightGray);
        spriteBatch.DrawString(content.Load<SpriteFont>("Fonts/Hud"), otherUsage, new Vector2(textEdgeSpacing, 78), Color.LightGray);
        spriteBatch.DrawString(content.Load<SpriteFont>("Fonts/Hud"),
            "Arrows/A-D move | Space/W jump | M manual | F Foundry | O Ollama | P pause | N restart | Q quit",
            new Vector2(textEdgeSpacing, 100), Color.Yellow);
        spriteBatch.End();
    }
}