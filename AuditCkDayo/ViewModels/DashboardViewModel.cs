using System;
using System.Collections.Generic;
using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class DashboardViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public AuditStatus? Status { get; set; }
        public int? EstablishmentId { get; set; }
        public int? BuyerId { get; set; }

        public List<AuditItem> Audits { get; set; } = new List<AuditItem>();
        public List<AuditItem> TodayAudits { get; set; } = new List<AuditItem>();
        public List<User> CashOnHandUsers { get; set; } = new List<User>();
        public decimal TotalAmount { get; set; }
    }
}
