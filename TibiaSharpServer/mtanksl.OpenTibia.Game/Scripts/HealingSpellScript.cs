using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Game.Common;

namespace mtanksl.OpenTibia.Game.Scripts;

/// <summary>
/// Built-in healing spell script.
/// Restores a configurable amount of HP to the caster when they cast a
/// healing word (e.g. "exura", "exura gran").
/// Register per spell word via <see cref="IPluginRegistry.RegisterSpell"/>.
/// </summary>
public sealed class HealingSpellScript : ISpellScript
{
    private readonly int _healAmount;

    /// <param name="healAmount">Maximum HP restored per cast.</param>
    public HealingSpellScript(int healAmount = 50)
    {
        if (healAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(healAmount), "Heal amount must be greater than zero.");
        _healAmount = healAmount;
    }

    public int HealAmount => _healAmount;

    public Promise OnCast(IContext context, Player caster, string words)
    {
        int missing = caster.MaxHealth - caster.Health;
        int healed  = Math.Min(_healAmount, missing);
        caster.Health += healed;

        Logger.Info(
            $"[Spell '{words}'] '{caster.Name}' healed {healed} HP. " +
            $"HP: {caster.Health}/{caster.MaxHealth}");

        return Promise.Completed;
    }
}
