using mtanksl.OpenTibia.Data.Common;
using mtanksl.OpenTibia.Data.InMemory;

namespace mtanksl.OpenTibia.Tests;

/// <summary>
/// Unit tests for the in-memory data layer.
/// </summary>
public class DataTests
{
    [Fact]
    public void InMemoryAccountRepository_FindByName_ReturnsAddedAccount()
    {
        var repo = new InMemoryAccountRepository();
        var account = new Account { Id = 1, Name = "admin", Password = "secret" };
        repo.Add(account);

        Account? result = repo.FindByName("admin");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
        Assert.Equal("admin", result.Name);
    }

    [Fact]
    public void InMemoryAccountRepository_FindByName_IsCaseInsensitive()
    {
        var repo = new InMemoryAccountRepository();
        repo.Add(new Account { Id = 1, Name = "Admin" });

        Assert.NotNull(repo.FindByName("ADMIN"));
        Assert.NotNull(repo.FindByName("admin"));
    }

    [Fact]
    public void InMemoryAccountRepository_FindByName_ReturnsNullForMissing()
    {
        var repo = new InMemoryAccountRepository();
        Assert.Null(repo.FindByName("nonexistent"));
    }

    [Fact]
    public void InMemoryPlayerRepository_FindByAccount_ReturnsMultipleChars()
    {
        var repo = new InMemoryPlayerRepository();
        repo.Add(new PlayerRecord { Id = 1, AccountId = 1, Name = "PlayerA" });
        repo.Add(new PlayerRecord { Id = 2, AccountId = 1, Name = "PlayerB" });
        repo.Add(new PlayerRecord { Id = 3, AccountId = 2, Name = "PlayerC" });

        IReadOnlyList<PlayerRecord> chars = repo.FindByAccount(1);

        Assert.Equal(2, chars.Count);
        Assert.Contains(chars, p => p.Name == "PlayerA");
        Assert.Contains(chars, p => p.Name == "PlayerB");
    }

    [Fact]
    public void InMemoryUnitOfWork_CommitDoesNotThrow()
    {
        using var uow = new InMemoryUnitOfWork();
        // Commit is a no-op for in-memory; should not throw.
        uow.Commit();
    }
}
