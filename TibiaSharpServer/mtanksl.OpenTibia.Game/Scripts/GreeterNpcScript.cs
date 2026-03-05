using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Game.Common;

namespace mtanksl.OpenTibia.Game.Scripts;

/// <summary>
/// Built-in NPC greeting script.
/// Responds to any message addressed to the NPC and logs the interaction.
/// Register per NPC name via <see cref="IPluginRegistry.RegisterNpc"/>.
/// </summary>
public sealed class GreeterNpcScript : INpcScript
{
    private readonly string _greeting;

    public GreeterNpcScript(string greeting = "Hello, adventurer!")
    {
        _greeting = greeting;
    }

    public Promise OnSay(IContext context, Player player, string words, Npc npc)
    {
        Logger.Info($"[NPC '{npc.Name}'] Player '{player.Name}' says: \"{words}\" → \"{_greeting}\"");
        return Promise.Completed;
    }
}
