/*
 * Goal: Keep game rules deterministic and independent of the AI provider.
 * A. Define the four legal actions and their exact effects.
 * B. Advance movement, collisions, cooldowns, and score on fixed ticks.
 * C. Serialize a compact state snapshot for a decision request.
 * Inputs: Seeded randomness and validated game actions.
 * Output: Updated game state and an explicit coordinate/time contract.
 * Boundary: The model cannot change collisions, bounds, or scoring.
 */
using System.Text.Json;

namespace DecisionSpaceGame;

public enum GameAction { Stop, MoveLeft, MoveRight, Fire }
public readonly record struct Position(int X, int Y);

public static class Actions
{
    // A. Descriptions define action semantics, not a request for generated prose.
    public static readonly IReadOnlyDictionary<string, string?> Options = new Dictionary<string, string?>
    {
        ["move_left"] = "Move one column left each 50ms tick for the next four ticks; do not fire.",
        ["move_right"] = "Move one column right each 50ms tick for the next four ticks; do not fire.",
        ["fire"] = "Stay at the current column and fire one upward shot now if cooldown is zero.",
        ["stop"] = "Stay at the current column without firing."
    };

    public static bool TryMap(string label, out GameAction action)
    {
        action = label switch
        {
            "move_left" => GameAction.MoveLeft,
            "move_right" => GameAction.MoveRight,
            "fire" => GameAction.Fire,
            _ => GameAction.Stop
        };
        return Options.ContainsKey(label);
    }

    public static bool TryKey(ConsoleKey key, out GameAction action)
    {
        action = key switch
        {
            ConsoleKey.LeftArrow or ConsoleKey.A => GameAction.MoveLeft,
            ConsoleKey.RightArrow or ConsoleKey.D => GameAction.MoveRight,
            ConsoleKey.Spacebar => GameAction.Fire,
            _ => GameAction.Stop
        };
        return key is ConsoleKey.LeftArrow or ConsoleKey.A or ConsoleKey.RightArrow or
            ConsoleKey.D or ConsoleKey.Spacebar or ConsoleKey.S;
    }

    public const string Instructions =
        "Choose exactly one supplied action to protect the ship and destroy invaders. " +
        "Coordinates: x increases right; y increases down; ship stays at y=18. " +
        "Shots move up one row per 50ms tick; hostile shots move down one row every two ticks. " +
        "Enemies move one column every ten ticks, bounce at the edges and descend one row on a bounce. " +
        "Movement lasts four ticks (200ms), moving FOUR columns, clamped to the board edges. " +
        "Fire creates only one shot and requires fireCooldownTicks=0. " +
        "The world is PAUSED during inference, then advances four ticks for your action. " +
        "Priority 1: escape a hostile shot in your column that reaches y=18 within four ticks. " +
        "Priority 2: if fireCooldownTicks=0 and a predictedFireLane matches player.x, fire. " +
        "Priority 3: move toward the nearest predictedFireLane, accounting for the four-column move. " +
        "If already aligned but cooling down, stop. Never fire during cooldown. " +
        "Predicted fire lanes account for enemy movement while a new shot travels; " +
        "they are geometric hints, not guaranteed kills. Choose a supplied label, not prose.";
}

public sealed class Game
{
    public const int Width = 32, Height = 20, PlayerY = 18, StepMilliseconds = 50, ActionTicks = 4;
    private readonly Random _random;
    private readonly List<Position> _enemies = [];
    private readonly Dictionary<Position, int> _enemyDesigns = [];
    private readonly List<Position> _shots = [];
    private readonly List<Position> _hostileShots = [];
    private int _direction = 1, _lease;
    private GameAction _action;

    public Game(int seed)
    {
        _random = new Random(seed);
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 7; column++)
            {
                var position = new Position(4 + column * 3, 2 + row * 2);
                _enemies.Add(position);
                _enemyDesigns.Add(position, (row * 7 + column) % 3);
            }
    }

    public int Tick { get; private set; }
    public int PlayerX { get; private set; } = Width / 2;
    public int Score { get; private set; }
    public int ShotsFired { get; private set; }
    public int Lives { get; private set; } = 3;
    public int? LastDamageTick { get; private set; }
    public bool ShowingDamage => LastDamageTick is int hit && Tick - hit < 6;
    public int FireCooldownTicks { get; private set; }
    public bool Finished => Lives <= 0 || _enemies.Count == 0;
    public GameAction ActiveAction => _action;
    public GameAction? LastAppliedAction { get; private set; }
    public bool HasActiveAction => _lease > 0;
    public IReadOnlyList<Position> Enemies => _enemies.AsReadOnly();
    public IReadOnlyList<Position> Shots => _shots.AsReadOnly();
    public IReadOnlyList<Position> HostileShots => _hostileShots.AsReadOnly();
    public int EnemyDesign(Position enemy) => _enemyDesigns[enemy];

    public IReadOnlyDictionary<string, string?> AllowedOptions() =>
        Actions.Options.Where(option => option.Key switch
        {
            "fire" => FireCooldownTicks == 0,
            "move_left" => PlayerX > 0,
            "move_right" => PlayerX < Width - 1,
            _ => true
        }).ToDictionary();

    public bool Apply(GameAction action)
    {
        if (!Enum.IsDefined(action) || Finished) return false;
        _action = action;
        LastAppliedAction = action;
        _lease = ActionTicks;
        if (action == GameAction.Fire && FireCooldownTicks == 0)
        {
            _shots.Add(new Position(PlayerX, PlayerY - 1));
            ShotsFired++;
            FireCooldownTicks = 6;
        }
        return true;
    }

    public void Step()
    {
        // B. Fixed ticks make seeded runs reproducible independently of network time.
        if (Finished) return;
        Tick++;
        if (_lease > 0)
        {
            PlayerX = Math.Clamp(PlayerX + (_action == GameAction.MoveLeft ? -1 :
                _action == GameAction.MoveRight ? 1 : 0), 0, Width - 1);
            if (--_lease == 0) _action = GameAction.Stop;
        }
        if (FireCooldownTicks > 0) FireCooldownTicks--;
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            Position before = _shots[i];
            Position after = before with { Y = before.Y - 1 };
            int enemy = _enemies.FindIndex(e => e.X == after.X && (e.Y == before.Y || e.Y == after.Y));
            if (enemy >= 0)
            {
                _enemies.RemoveAt(enemy);
                _shots.RemoveAt(i);
                Score += 10;
            }
            else if (after.Y < 0) _shots.RemoveAt(i);
            else _shots[i] = after;
        }
        if (Tick % 10 == 0 && _enemies.Count > 0)
        {
            bool bounce = _enemies.Any(e => e.X + _direction < 0 || e.X + _direction >= Width);
            if (bounce) _direction = -_direction;
            var designs = _enemies.Select(EnemyDesign).ToArray();
            _enemyDesigns.Clear();
            for (int i = 0; i < _enemies.Count; i++)
            {
                _enemies[i] = bounce ? _enemies[i] with { Y = _enemies[i].Y + 1 } :
                    _enemies[i] with { X = _enemies[i].X + _direction };
                _enemyDesigns.Add(_enemies[i], designs[i]);
            }
            // Resolve an enemy moving into an already airborne shot.
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                int enemy = _enemies.IndexOf(_shots[i]);
                if (enemy < 0) continue;
                _enemies.RemoveAt(enemy);
                _shots.RemoveAt(i);
                Score += 10;
            }
        }
        if (Tick % 20 == 0 && _enemies.Count > 0)
        {
            Position enemy = _enemies[_random.Next(_enemies.Count)];
            _hostileShots.Add(enemy with { Y = enemy.Y + 1 });
        }
        if (Tick % 2 == 0)
        {
            bool hit = false;
            for (int i = _hostileShots.Count - 1; i >= 0; i--)
            {
                Position shot = _hostileShots[i] with { Y = _hostileShots[i].Y + 1 };
                if (shot.Y == PlayerY && shot.X == PlayerX) hit = true;
                if (shot.Y >= PlayerY) _hostileShots.RemoveAt(i);
                else _hostileShots[i] = shot;
            }
            if (hit)
            {
                Lives--;
                LastDamageTick = Tick;
                _hostileShots.Clear();
            }
        }
        if (_enemies.Any(e => e.Y >= PlayerY)) Lives = 0;
    }

    // C. Text state exposes the rules the model needs without a screenshot.
    public string Snapshot() => JsonSerializer.Serialize(new
    {
        tick = Tick, tickDurationMs = StepMilliseconds, width = Width, height = Height,
        coordinateSystem = "origin top-left; x right, y down",
        actionDurationTicks = ActionTicks,
        player = new { x = PlayerX, y = PlayerY, lives = Lives },
        fireCooldownTicks = FireCooldownTicks, enemyDirectionX = _direction,
        simulationPausedDuringInference = true,
        predictedFireLanes = PredictFireLanes(),
        ticksUntilEnemyMove = 10 - Tick % 10,
        enemies = _enemies.Select(p => new { x = p.X, y = p.Y }),
        shots = _shots.Select(p => new { x = p.X, y = p.Y }),
        hostileShots = _hostileShots.Select(p => new { x = p.X, y = p.Y }),
        score = Score
    });

    // Predict collision columns using the same movement/bounce order as Step.
    // This is observable geometry, not an automatic choice of the ship's action.
    public int[] PredictFireLanes()
    {
        var enemies = _enemies.ToArray();
        int direction = _direction;
        var lanes = new HashSet<int>();
        int shotY = PlayerY - 1;
        for (int ahead = 1; shotY >= 0 && enemies.Length > 0; ahead++, shotY--)
        {
            foreach (var enemy in enemies)
                if (enemy.Y == shotY || enemy.Y == shotY - 1) lanes.Add(enemy.X);
            if ((Tick + ahead) % 10 != 0) continue;
            bool bounce = enemies.Any(e => e.X + direction < 0 || e.X + direction >= Width);
            if (bounce) direction = -direction;
            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = bounce ? enemies[i] with { Y = enemies[i].Y + 1 } :
                    enemies[i] with { X = enemies[i].X + direction };
            foreach (var enemy in enemies)
                if (enemy.Y == shotY - 1) lanes.Add(enemy.X);
        }
        return lanes.Order().ToArray();
    }
}
