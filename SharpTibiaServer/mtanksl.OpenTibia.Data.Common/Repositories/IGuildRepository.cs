using OpenTibia.Data.Models;
using System.Threading.Tasks;

namespace OpenTibia.Data.Repositories
{
    public interface IGuildRepository
    {
        Task<DbGuild[]> GetGuilds();

        void AddGuild(DbGuild guild);

        void RemoveGuild(DbGuild guild);
    }
}