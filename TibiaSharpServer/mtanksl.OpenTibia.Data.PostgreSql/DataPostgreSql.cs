using mtanksl.OpenTibia.Data;
using mtanksl.OpenTibia.Data.Common;

namespace mtanksl.OpenTibia.Data.PostgreSql;

/// <summary>
/// PostgreSql unit-of-work stub.
/// Implement this class with a real ADO.NET / Dapper / EF Core connection.
/// </summary>
public sealed class PostgreSqlUnitOfWork : IUnitOfWork
{
    // TODO: inject a connection string and open a real PostgreSql connection here.
    public IAccountRepository Accounts => throw new NotImplementedException("PostgreSql back-end not yet implemented.");
    public IPlayerRepository  Players  => throw new NotImplementedException("PostgreSql back-end not yet implemented.");

    public void Commit()  => throw new NotImplementedException();
    public void Dispose() { }
}
