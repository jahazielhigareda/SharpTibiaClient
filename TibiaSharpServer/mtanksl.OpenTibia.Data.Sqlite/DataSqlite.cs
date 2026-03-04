using mtanksl.OpenTibia.Data;
using mtanksl.OpenTibia.Data.Common;

namespace mtanksl.OpenTibia.Data.Sqlite;

/// <summary>
/// Sqlite unit-of-work stub.
/// Implement this class with a real ADO.NET / Dapper / EF Core connection.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    // TODO: inject a connection string and open a real Sqlite connection here.
    public IAccountRepository Accounts => throw new NotImplementedException("Sqlite back-end not yet implemented.");
    public IPlayerRepository  Players  => throw new NotImplementedException("Sqlite back-end not yet implemented.");

    public void Commit()  => throw new NotImplementedException();
    public void Dispose() { }
}
