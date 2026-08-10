using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AuditCkDayo.Services;

namespace AuditCkDayo.ViewModels
{
    public class AuditSubmissionViewModel
    {
        public int? EstablishmentId { get; set; }
        public string? CombinedDestinationId { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date is required.")]
        public DateTime EntryDate { get; set; } = DateTime.Today;

        public string? Notes { get; set; }

        public string? ReceiptImageUrl { get; set; }

        public List<string> ReceiptImageUrls { get; set; } = new();

        public List<OcrItemResult> Items { get; set; } = new();
    }
}
