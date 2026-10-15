/*
 * Goal: Opt in to exercising the real DesktopGL rendering context.
 * A. Open the welcome image without constructing any provider.
 * B. Draw the battlefield and move the manual ship.
 * C. Assert rendered pixel diversity and the logical movement.
 */
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class GraphicsTests
{
    [GraphicsFact]
    [Trait("Category", "Graphics")]
    public async Task WelcomeAndManualRenderOnActualGraphicsDevice()
    {
        int constructed = 0;
        var mission = new Mission(() => { constructed++; return new ReplayClient(); },
            () => throw new Exception("No cloud"));
        using var window = new SpaceGameWindow(mission);
        try
        {
            window.RunOneFrame();
            Assert.True(mission.Welcome);
            Assert.Equal(0, constructed);
            var welcome = window.ReadCanvas();
            Assert.True(welcome.Distinct().Take(100).Count() == 100);
            Assert.Contains(".NET + Foundry", SpaceGameWindow.WelcomeBrand);
            mission.Select("manual");
            mission.Update(.05, GameAction.MoveLeft);
            window.RunOneFrame();
            Assert.Equal(15, mission.World.PlayerX);
            Assert.False(welcome.SequenceEqual(window.ReadCanvas()));
            mission.LoseFocus();
            int tick = mission.World.Tick;
            window.RunOneFrame();
            Assert.Equal(tick, mission.World.Tick);
            Assert.True(mission.FocusPaused);
            mission.TogglePause();
            mission.Update(.05);
            Assert.Equal(tick + 1, mission.World.Tick);
            mission.Select("foundry");
            mission.Update(0);
            Assert.Equal(1, constructed);
            Assert.True(mission.Driver.HasPendingRequest);
            mission.TogglePause();
            tick = mission.World.Tick;
            window.RunOneFrame();
            Assert.Equal(tick, mission.World.Tick);
            mission.Restart();
            window.RunOneFrame();
            Assert.Equal("manual", mission.Driver.Provider);
            Assert.Equal(0, mission.Driver.Statistics.Accepted);
        }

        finally { await window.ShutdownAsync(); }
    }
}

public sealed class GraphicsFactAttribute : FactAttribute
{
    public GraphicsFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SPACEGAME_GRAPHICS_TESTS") != "1")
            Skip = "Set SPACEGAME_GRAPHICS_TESTS=1 to open a real DesktopGL window.";
    }
}
