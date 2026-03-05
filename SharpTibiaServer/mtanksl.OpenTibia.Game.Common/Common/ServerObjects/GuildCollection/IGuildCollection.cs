using OpenTibia.Common.Objects;
using System.Collections.Generic;

namespace OpenTibia.Game.Common.ServerObjects
{
    public interface IGuildCollection
    {
        int Count { get; }

        void AddGuild(Guild guild);

        void RemoveGuild(Guild guild);

        Guild GetGuildByName(string name);

        Guild GetGuildByLeader(Player leader);

        Guild GetGuildThatContainsMember(Player player);

        IEnumerable<Guild> GetGuildThatContainsInvitation(Player player);

        IEnumerable<Guild> GetGuilds();
    }
}