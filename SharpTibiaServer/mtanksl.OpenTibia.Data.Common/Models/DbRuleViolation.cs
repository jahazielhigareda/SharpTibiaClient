using System;
using System.ComponentModel.DataAnnotations;

namespace OpenTibia.Data.Models
{
    public class DbRuleViolation
    {
        public int Id { get; set; }

        public int PlayerId { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; }

        public byte Reason { get; set; }

        public byte Action { get; set; }

        [Required]
        [StringLength(255)]
        public string Comment { get; set; }

        public int? StatmentPlayerId { get; set; }

        [StringLength(255)]
        public string Statment { get; set; }

        public DateTime? StatmentDate { get; set; }

        [Required]
        [StringLength(255)]
        public string StatmentIPAddress { get; set; }

        public bool IPAddressBanishment { get; set; }

        public DateTime CreationDate { get; set; }


        public DbPlayer Player { get; set; }

        public DbPlayer StatmentPlayer { get; set; }
    }
}