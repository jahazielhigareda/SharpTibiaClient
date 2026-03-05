using OpenTibia.Common.Objects;
using System.Collections.Generic;
using System.Linq;

namespace OpenTibia.Game.Common.ServerObjects
{
    public class GuildCollection : IGuildCollection
    {
        private List<Guild> guilds = new List<Guild>();

        public int Count
        {
            get
            {
                return guilds.Count;
            }
        }

        public void AddGuild(Guild guild)
        {
            guilds.Add(guild);
        }

        public void RemoveGuild(Guild guild)
        {
            guilds.Remove(guild);
        }

        public Guild GetGuildByName(string name)  
        {
             return GetGuilds()
                .Where(g => g.Name == name)
                .FirstOrDefault();
        }

        public Guild GetGuildByLeader(Player leader)
        {
            return GetGuilds()
                .Where(g => g.IsLeader(leader) )
                .FirstOrDefault();
        }

        public Guild GetGuildThatContainsMember(Player player)
        {
            return GetGuilds()
                .Where(g => g.ContainsMember(player, out _) )
                .FirstOrDefault();
        }

        public IEnumerable<Guild> GetGuildThatContainsInvitation(Player player)
        {
            return GetGuilds()
                .Where(g => g.ContainsInvitation(player, out _) );
        }

        public IEnumerable<Guild> GetGuilds()
        {
            return guilds;
        }
    }
}