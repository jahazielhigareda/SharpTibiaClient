using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Game.Common;

namespace mtanksl.OpenTibia.Tests;

/// <summary>
/// Tests for game model types defined in Game.Common.
/// </summary>
public class GameCommonTests
{
    [Fact]
    public void Player_HealthPercent_IsCorrect()
    {
        var player = new Player("Test")
        {
            Health    = 50,
            MaxHealth = 200
        };

        Assert.Equal(0.25f, player.HealthPercent, precision: 4);
    }

    [Fact]
    public void Player_DefaultLevel_IsOne()
    {
        var player = new Player("NewChar");
        Assert.Equal(1, player.Level);
    }

    [Fact]
    public void Creature_IdIsUnique()
    {
        var a = new Player("A");
        var b = new Player("B");
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Promise_Completed_IsNotNull()
    {
        Assert.NotNull(Promise.Completed);
    }

    [Fact]
    public void Position_ToString_ContainsCoordinates()
    {
        var pos = new Position(100, 200, 7);
        string s = pos.ToString();
        Assert.Contains("100", s);
        Assert.Contains("200", s);
        Assert.Contains("7",   s);
    }
}
