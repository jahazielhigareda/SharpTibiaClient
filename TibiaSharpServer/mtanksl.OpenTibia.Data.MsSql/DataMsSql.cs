using mtanksl.OpenTibia.Data;
using mtanksl.OpenTibia.Data.Common;

namespace mtanksl.OpenTibia.Data.MsSql;

/// <summary>
/// MsSql unit-of-work stub.
/// Implement this class with a real ADO.NET / Dapper / EF Core connection.
/// </summary>
public sealed class MsSqlUnitOfWork : IUnitOfWork
{
    // TODO: inject a connection string and open a real MsSql connection here.
    public IAccountRepository Accounts => throw new NotImplementedException("MsSql back-end not yet implemented.");
    public IPlayerRepository  Players  => throw new NotImplementedException("MsSql back-end not yet implemented.");

    public void Commit()  => throw new NotImplementedException();
    public void Dispose() { }
}
