namespace mtanksl.OpenTibia.Data.Common;

/// <summary>
/// Represents a player account loaded from the database.
/// </summary>
public sealed class Account
{
    public int    Id       { get; init; }
    public string Name     { get; init; } = "";
    public string Password { get; init; } = "";
    public bool   Premium  { get; init; }
    public int    PremiumDays { get; init; }
}

/// <summary>
/// Represents a player character record stored in the database.
/// </summary>
public sealed class PlayerRecord
{
    public int      Id        { get; init; }
    public int      AccountId { get; init; }
    public string   Name      { get; init; } = "";
    public string   World     { get; init; } = "";
    public int      Level     { get; init; } = 1;
    public int      Health    { get; init; } = 150;
    public int      MaxHealth { get; init; } = 150;
    public int      Mana      { get; init; } = 55;
    public int      MaxMana   { get; init; } = 55;
    public int      Capacity  { get; init; } = 400;
    public int      Experience { get; init; }
    public ushort   PosX      { get; init; } = 1000;
    public ushort   PosY      { get; init; } = 1000;
    public byte     PosZ      { get; init; } = 7;
}

/// <summary>
/// Read-only repository interfaces that the data layer implements.
/// </summary>
public interface IAccountRepository
{
    Account? FindByName(string name);
}

public interface IPlayerRepository
{
    PlayerRecord? FindByName(string name);
    IReadOnlyList<PlayerRecord> FindByAccount(int accountId);
    void Save(PlayerRecord record);
}
