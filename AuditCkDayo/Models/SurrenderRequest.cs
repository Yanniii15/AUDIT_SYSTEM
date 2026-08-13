using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum SurrenderStatus
    {
        Pending,
        Confirmed,
        Rejected,
        Cancelled
    }


    public class SurrenderRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BuyerId { get; set; }

        [ForeignKey("BuyerId")]
        public virtual User Buyer { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal DeclaredAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? ConfirmedAmount { get; set; }

        [Required]
        public SurrenderStatus Status { get; set; } = SurrenderStatus.Pending;



        [Required]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        public DateTime? ActionDate { get; set; }

        public int? ActionByUserId { get; set; }

        [ForeignKey("ActionByUserId")]
        public virtual User? ActionByUser { get; set; }

        public int? AssignedReceiverId { get; set; }

        [ForeignKey("AssignedReceiverId")]
        public virtual User? AssignedReceiver { get; set; }

        [MaxLength(255)]
        public string? BuyerNotes { get; set; }

        [MaxLength(255)]
        public string? ActionNotes { get; set; }
    }
}
