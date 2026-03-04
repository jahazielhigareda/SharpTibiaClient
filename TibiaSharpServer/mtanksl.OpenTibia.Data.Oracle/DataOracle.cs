using mtanksl.OpenTibia.Data;
using mtanksl.OpenTibia.Data.Common;

namespace mtanksl.OpenTibia.Data.Oracle;

/// <summary>
/// Oracle unit-of-work stub.
/// Implement this class with a real ADO.NET / Dapper / EF Core connection.
/// </summary>
public sealed class OracleUnitOfWork : IUnitOfWork
{
    // TODO: inject a connection string and open a real Oracle connection here.
    public IAccountRepository Accounts => throw new NotImplementedException("Oracle back-end not yet implemented.");
    public IPlayerRepository  Players  => throw new NotImplementedException("Oracle back-end not yet implemented.");

    public void Commit()  => throw new NotImplementedException();
    public void Dispose() { }
}
