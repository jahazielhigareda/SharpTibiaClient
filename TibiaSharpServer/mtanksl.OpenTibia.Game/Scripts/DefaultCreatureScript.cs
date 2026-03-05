using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Game.Common;

namespace mtanksl.OpenTibia.Game.Scripts;

/// <summary>
/// Default built-in creature lifecycle handler.
/// Logs login, logout, and death events.
/// External plugins may register their own <see cref="ICreatureScript"/>
/// implementations to extend this behaviour.
/// </summary>
public sealed class DefaultCreatureScript : ICreatureScript
{
    public Promise OnLogin(IContext context, Player player)
    {
        Logger.Info($"[Script] Player '{player.Name}' logged in at {player.Position}.");
        return Promise.Completed;
    }

    public Promise OnLogout(IContext context, Player player)
    {
        Logger.Info($"[Script] Player '{player.Name}' logged out.");
        return Promise.Completed;
    }

    public Promise OnDeath(IContext context, Creature creature)
    {
        Logger.Info($"[Script] {creature.Name} died at {creature.Position}.");
        return Promise.Completed;
    }
}
