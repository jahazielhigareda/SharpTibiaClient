using OpenTibia.Common.Objects;
using System.Collections.Generic;

namespace OpenTibia.Game.Common.ServerObjects
{
    public class Guild
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string MessageOfTheDay { get; set; }

        public int /* databasePlayerId */ Leader { get; set; }

        public bool IsLeader(Player player)
        {
            return Leader == player.DatabasePlayerId;
        }

        private Dictionary<int /* databasePlayerId */, string> members = new Dictionary<int, string>();

        public int CountMembers
        {
            get
            {
                return members.Count;
            }
        }

        public void AddMember(int databasePlayerId, string rankName)
        {
            members.Add(databasePlayerId, rankName);
        }

        public void AddMember(Player player, string rankName)
        {
            AddMember(player.DatabasePlayerId, rankName);
        }

        public void RemoveMember(Player player)
        {
            members.Remove(player.DatabasePlayerId);
        }

        public bool ContainsMember(Player player, out string rankName)
        {
            return members.TryGetValue(player.DatabasePlayerId, out rankName);
        }

        public IEnumerable<KeyValuePair<int /* databasePlayerId */, string> > GetMembers()
        {
            return members;
        }

        private Dictionary<int /* databasePlayerId */, string> invitations = new Dictionary<int, string>();

        public int CountInvitations
        {
            get
            {
                return invitations.Count;
            }
        }

        public void AddInvitation(int databasePlayerId, string rankName)
        {
            invitations.Add(databasePlayerId, rankName);
        }

        public void AddInvitation(Player player, string rankName)
        {
            AddInvitation(player.DatabasePlayerId, rankName);
        }

        public void RemoveInvitation(Player player)
        {
            invitations.Remove(player.DatabasePlayerId);
        }

        public bool ContainsInvitation(Player player, out string rankName)
        {
            return invitations.TryGetValue(player.DatabasePlayerId, out rankName);
        }

        public IEnumerable<KeyValuePair<int /* databasePlayerId */, string> > GetInvitations()
        {
            return invitations;
        }
    }
}