using mtanksl.OpenTibia.Data.Common;

namespace mtanksl.OpenTibia.Data;

/// <summary>
/// Aggregate unit-of-work that groups all server repositories.
/// The concrete implementation (InMemory, MySql, etc.) provides the
/// actual storage back-end; this type serves as the composition root
/// for the data layer.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IAccountRepository Accounts { get; }
    IPlayerRepository  Players  { get; }

    /// <summary>Persist any pending changes to the underlying store.</summary>
    void Commit();
}
