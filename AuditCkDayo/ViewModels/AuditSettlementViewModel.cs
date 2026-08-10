using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.ViewModels
{
    public class AuditSettlementViewModel
    {
        public int? PcfReleaseId { get; set; }
        public int? ResponsibleManagerId { get; set; }
        public string? ReceiverName { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalPCReleased { get; set; }

        [Range(0, double.MaxValue)]
        public decimal TotalAcceptedExpenses { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ActualChangeReturned { get; set; }

        public decimal ExpectedChange { get; set; }
        public decimal ShortOverAmount { get; set; }
    }
}
