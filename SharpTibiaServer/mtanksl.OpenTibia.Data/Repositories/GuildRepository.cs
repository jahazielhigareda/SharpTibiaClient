using Microsoft.EntityFrameworkCore;
using OpenTibia.Data.Contexts;
using OpenTibia.Data.Models;
using System.Threading.Tasks;

namespace OpenTibia.Data.Repositories
{
    public class GuildRepository : IGuildRepository
    {
        private DatabaseContext context;

        public GuildRepository(DatabaseContext context)
        {
            this.context = context;
        }

        public async Task<DbGuild[]> GetGuilds()
        {
           await Task.Yield();

            DbGuild[] guilds = await context.Guilds
                .ToArrayAsync();

            if (guilds.Length > 0)
            {
                await context.GuildMembers
                    .LoadAsync();

                await context.GuildInvitations
                    .LoadAsync();
            }

            return guilds;
        }

        public void AddGuild(DbGuild guild)
        {
            context.Guilds.Add(guild);
        }

        public void RemoveGuild(DbGuild guild)
        {
            context.Guilds.Remove(guild);
        }
    }
}