using mtanksl.OpenTibia.Data;
using mtanksl.OpenTibia.Data.Common;

namespace mtanksl.OpenTibia.Data.InMemory;

/// <summary>
/// In-memory account repository — useful for unit tests and development.
/// Not thread-safe; wrap access in a lock or use from a single thread.
/// </summary>
public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<string, Account> _store = new(StringComparer.OrdinalIgnoreCase);

    public void Add(Account account) => _store[account.Name] = account;

    public Account? FindByName(string name)
        => _store.TryGetValue(name, out var acc) ? acc : null;
}

/// <summary>
/// In-memory player repository.
/// </summary>
public sealed class InMemoryPlayerRepository : IPlayerRepository
{
    private readonly Dictionary<string, PlayerRecord> _byName = new(StringComparer.OrdinalIgnoreCase);

    public void Add(PlayerRecord record) => _byName[record.Name] = record;

    public PlayerRecord? FindByName(string name)
        => _byName.TryGetValue(name, out var r) ? r : null;

    public IReadOnlyList<PlayerRecord> FindByAccount(int accountId)
        => _byName.Values.Where(r => r.AccountId == accountId).ToList();

    public void Save(PlayerRecord record) => _byName[record.Name] = record;
}

/// <summary>
/// In-memory unit-of-work.  All repositories share the same in-process
/// dictionaries; Commit() is a no-op.
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public InMemoryUnitOfWork()
    {
        Accounts = new InMemoryAccountRepository();
        Players  = new InMemoryPlayerRepository();
    }

    public IAccountRepository Accounts { get; }
    public IPlayerRepository  Players  { get; }

    public void Commit()  { /* in-memory: nothing to flush */ }
    public void Dispose() { /* nothing to release          */ }
}
