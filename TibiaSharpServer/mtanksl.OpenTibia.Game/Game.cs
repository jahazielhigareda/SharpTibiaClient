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
/// connections, and coordinates with the data layer and script registry.
/// </summary>
public sealed class GameEngine : IDisposable
{
    private readonly GameScheduler    _scheduler;
    private readonly GameState        _state;
    private readonly IUnitOfWork      _data;
    private readonly IScriptDispatcher? _registry;

    /// <param name="data">Unit-of-work for player/account persistence.</param>
    /// <param name="registry">
    /// Optional script registry.  When provided, lifecycle events (login,
    /// logout, death, spells) are dispatched to registered scripts.
    /// </param>
    public GameEngine(IUnitOfWork data, IScriptDispatcher? registry = null)
    {
        _data      = data;
        _registry  = registry;
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

    private IContext CreateContext() => new GameContext(_state, _scheduler);

    /// <summary>
    /// Creates an <see cref="IContext"/> for use outside the <c>Game</c> project
    /// (e.g. by the host when dispatching startup/shutdown global events).
    /// </summary>
    public IContext CreatePublicContext() => CreateContext();

    // -----------------------------------------------------------------------
    // Player lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads a player from the database, adds them to the live game state,
    /// and dispatches <c>OnLogin</c> scripts.
    /// </summary>
    public async Task<Player?> LoginPlayerAsync(string name)
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

        if (_registry != null)
            await DispatchLoginAsync(player);

        return player;
    }

    /// <summary>Logs a player out, dispatches <c>OnLogout</c> scripts, then removes them.</summary>
    public async Task LogoutPlayerAsync(uint playerId)
    {
        if (!_state.Players.TryGetValue(playerId, out var player))
            return;

        if (_registry != null)
            await DispatchLogoutAsync(player);

        _state.RemovePlayer(playerId);
        Logger.Info($"Player '{player.Name}' logged out.");
    }

    // -----------------------------------------------------------------------
    // Script dispatch helpers
    // -----------------------------------------------------------------------

    /// <summary>Dispatches <c>OnLogin</c> to all registered <see cref="ICreatureScript"/>s.</summary>
    public Task DispatchLoginAsync(Player player)
    {
        if (_registry == null) return Task.CompletedTask;
        return DispatchOnRegistryAsync(ctx => _registry.DispatchLoginAsync(ctx, player));
    }

    /// <summary>Dispatches <c>OnLogout</c> to all registered <see cref="ICreatureScript"/>s.</summary>
    public Task DispatchLogoutAsync(Player player)
    {
        if (_registry == null) return Task.CompletedTask;
        return DispatchOnRegistryAsync(ctx => _registry.DispatchLogoutAsync(ctx, player));
    }

    /// <summary>Dispatches <c>OnDeath</c> to all registered <see cref="ICreatureScript"/>s.</summary>
    public Task DispatchDeathAsync(Creature creature)
    {
        if (_registry == null) return Task.CompletedTask;
        return DispatchOnRegistryAsync(ctx => _registry.DispatchDeathAsync(ctx, creature));
    }

    /// <summary>Dispatches a spell cast to the matching <see cref="ISpellScript"/>.</summary>
    public Task DispatchSpellAsync(Player caster, string words)
    {
        if (_registry == null) return Task.CompletedTask;
        return DispatchOnRegistryAsync(ctx => _registry.DispatchSpellAsync(ctx, caster, words));
    }

    /// <summary>Dispatches an NPC interaction to the matching <see cref="INpcScript"/>.</summary>
    public Task DispatchNpcSayAsync(Player player, string words, Npc npc)
    {
        if (_registry == null) return Task.CompletedTask;
        return DispatchOnRegistryAsync(ctx => _registry.DispatchNpcSayAsync(ctx, player, words, npc));
    }

    /// <summary>
    /// Applies a one-tile movement delta to <paramref name="player"/>.
    /// Full boundary and collision checking can be added here as the game world
    /// model matures; for now the update is applied unconditionally while the map
    /// layer is still a stub.
    /// </summary>
    /// <param name="player">The player to move.</param>
    /// <param name="dx">X delta (-1, 0 or +1).</param>
    /// <param name="dy">Y delta (-1, 0 or +1).</param>
    public Task MovePlayerAsync(Player player, int dx, int dy)
    {
        // TODO: Add collision detection and movement script dispatch once the
        //       tile/map layer is implemented.  Movement script (IMovementScript)
        //       calls belong here rather than in the network handler.
        var newPos = new Position(
            (ushort)(player.Position.X + dx),
            (ushort)(player.Position.Y + dy),
            player.Position.Z);

        player.Position = newPos;
        Logger.Debug($"[Game] {player.Name} moved to {newPos}.");
        return Task.CompletedTask;
    }

    private Task DispatchOnRegistryAsync(Func<IContext, Task> dispatch)
    {
        var ctx = CreateContext();
        return dispatch(ctx);
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        _data.Dispose();
    }
}
