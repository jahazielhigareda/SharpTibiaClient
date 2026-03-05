using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpenTibia.Data.Models
{
    public class DbGuild
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; }

        [StringLength(255)]
        public string MessageOfTheDay { get; set; }

        public int LeaderId { get; set; }


        public DbPlayer Leader { get; set; }

        public ICollection<DbGuildMember> Members { get; set; } = new List<DbGuildMember>();

        public ICollection<DbGuildInvitation> Invitations { get; set; } = new List<DbGuildInvitation>();
    }
}