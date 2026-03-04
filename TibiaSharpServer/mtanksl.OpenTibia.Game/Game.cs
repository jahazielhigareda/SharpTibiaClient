using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Data;
using mtanksl.OpenTibia.Game.Common;
using mtanksl.OpenTibia.Threading;

namespace mtanksl.OpenTibia.Game;

/// <summary>
/// Simple in-process game state implementation.
/// </summary>
internal sealed class GameState : IGameState
{
    private readonly Dictionary<uint, Player>   _players   = new();
    private readonly Dictionary<uint, Creature> _creatures = new();

    public IReadOnlyDictionary<uint, Player>   Players   => _players;
    public IReadOnlyDictionary<uint, Creature> Creatures => _creatures;

    public void AddPlayer(Player player)
    {
        _players[player.Id]   = player;
        _creatures[player.Id] = player;
    }

    public void RemovePlayer(uint id)
    {
        _players.Remove(id);
        _creatures.Remove(id);
    }
}

/// <summary>
/// Execution context passed to every script or command handler.
/// </summary>
internal sealed class GameContext : IContext
{
    private readonly GameScheduler _scheduler;

    public GameContext(IGameState gameState, GameScheduler scheduler)
    {
        GameState  = gameState;
        _scheduler = scheduler;
    }

    public IGameState GameState { get; }

    public void Schedule(TimeSpan delay, Action action)
    {
        // Fire-and-forget: schedule action on a timer that then posts to the game thread.
        _ = Task.Delay(delay).ContinueWith(_ => _scheduler.Post(action));
    }
}

/// <summary>
/// Central game engine: owns the <see cref="GameScheduler"/>, manages player
/// connections, and coordinates with the data layer.
/// </summary>
public sealed class GameEngine : IDisposable
{
    private readonly GameScheduler _scheduler;
    private readonly GameState     _state;
    private readonly IUnitOfWork   _data;

    public GameEngine(IUnitOfWork data)
    {
        _data      = data;
        _scheduler = new GameScheduler();
        _state     = new GameState();
    }

    /// <summary>
    /// Posts an action to the game thread and awaits its completion.
    /// Use this from network-receive threads.
    /// </summary>
    public Task RunOnGameThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        _scheduler.Post(() =>
        {
            try   { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Loads a player from the database and adds them to the game.
    /// Called on the game thread.
    /// </summary>
    public Player? LoginPlayer(string name)
    {
        var record = _data.Players.FindByName(name);
        if (record == null)
        {
            Logger.Warning($"Login: no player named '{name}'.");
            return null;
        }

        var player = new Player(record.Name)
        {
            Level      = record.Level,
            Health     = record.Health,
            MaxHealth  = record.MaxHealth,
            Mana       = record.Mana,
            MaxMana    = record.MaxMana,
            Capacity   = record.Capacity,
            Experience = record.Experience,
            Position   = new Position(record.PosX, record.PosY, record.PosZ)
        };

        _state.AddPlayer(player);
        Logger.Info($"Player '{player.Name}' logged in at {player.Position}.");
        return player;
    }

    /// <summary>Logs a player out and persists their state.</summary>
    public void LogoutPlayer(uint playerId)
    {
        if (!_state.Players.TryGetValue(playerId, out var player))
            return;

        _state.RemovePlayer(playerId);
        Logger.Info($"Player '{player.Name}' logged out.");
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        _data.Dispose();
    }
}
