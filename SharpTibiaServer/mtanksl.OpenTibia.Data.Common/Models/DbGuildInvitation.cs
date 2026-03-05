using System.ComponentModel.DataAnnotations;

namespace OpenTibia.Data.Models
{
    public class DbGuildInvitation
    {
        public int GuildId { get; set; }

        public int PlayerId { get; set; }

        [Required]
        [StringLength(255)]
        public string RankName { get; set; }


        public DbGuild Guild { get; set; }

        public DbPlayer Player { get; set; }
    }
}