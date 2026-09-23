# Sales Report Bank Deposit Slip Verification Design Specification

> **Date:** September 23, 2026  
> **Topic:** Daily Sales Report Bank Deposit Slip Verification & Treasury Accountability  
> **Status:** Approved Design  
> **Target Audience:** Owner, Managers, Auditors, Admin  

---

## 1. Executive Summary & Business Goal

### Problem
Branch daily sales (morning and evening closing logbooks) are currently reviewed and confirmed by managers into the company Treasury. However, there has been no mechanism verifying that the confirmed cash sales collected from each branch were physically deposited into the company's bank accounts. The business owner requested proof of deposit per establishment to prevent cash diversion and ensure 100% auditability.

### Solution
1. **Two-Stage Workflow**: Managers confirm the daily sales logbook cash handover first (immediately posting to the Treasury), and upload the bank deposit slip once the physical bank transaction is executed.
2. **Treasury Red Alert Indicator**: Confirmed sales handovers posted to the Treasury that lack an uploaded deposit slip are prominently highlighted in **RED** with a `NO DEPOSIT SLIP` warning. Once the slip is uploaded, the warning clears and displays a green `DEPOSITED` badge with clickable slip proof.
3. **Tesseract OCR Smart Extraction**: When uploading a deposit slip photo, the system runs local Tesseract OCR to auto-detect the Deposited Amount, Bank Name, Reference/Trace Number, and Transaction Date, pre-filling the form fields for fast 1-click confirmation.
4. **Discrepancy Reporting**: Strict 1:1 reconciliation per establishment. If the deposited amount differs from the confirmed cash sales, a mandatory variance explanation must be recorded.
5. **Auditor Checking**: The deposit slip image and transaction metadata are permanently attached to the sales report and treasury entry, allowing the Auditor to inspect and verify physical proof at any time during periodic audits.

---

## 2. Architecture & Data Model

### A. Model Extension on `SalesReport` (`AuditCkDayo/Models/SalesReport.cs`)

The `SalesReport` entity is extended with dedicated bank deposit slip tracking fields:

```csharp
namespace AuditCkDayo.Models
{
    public class SalesReport
    {
        // ... existing properties ...

        // --- Bank Deposit Slip Verification Fields ---
        [MaxLength(255)]
        public string? DepositSlipImageUrl { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DepositedAmount { get; set; }

        [MaxLength(100)]
        public string? DepositBankName { get; set; }

        [MaxLength(100)]
        public string? DepositReferenceNumber { get; set; }

        public DateTime? DepositDate { get; set; }

        [MaxLength(500)]
        public string? DepositVarianceReason { get; set; }

        public int? DepositUploadedByUserId { get; set; }

        [ForeignKey("DepositUploadedByUserId")]
        public virtual User? DepositUploadedByUser { get; set; }

        public DateTime? DepositUploadedAt { get; set; }

        // --- Computed Helper Properties ---
        [NotMapped]
        public bool HasDepositSlip => !string.IsNullOrWhiteSpace(DepositSlipImageUrl);

        [NotMapped]
        public decimal DepositVariance => (DepositedAmount ?? 0m) - ConfirmedCashToHandover;

        [NotMapped]
        public bool IsDepositMatched => HasDepositSlip && Math.Abs(DepositVariance) < 0.01m;

        [NotMapped]
        public bool IsDepositDiscrepancy => HasDepositSlip && Math.Abs(DepositVariance) >= 0.01m;
    }
}
```

### B. Direct Relationship in `CashFlowEntry` (`AuditCkDayo/Models/CashFlowEntry.cs`)

To make querying deposit slip status in the Treasury fast and eliminate unnecessary lookups:

```csharp
namespace AuditCkDayo.Models
{
    public class CashFlowEntry
    {
        // ... existing properties ...

        public int? SalesReportId { get; set; }

        [ForeignKey("SalesReportId")]
        public virtual SalesReport? SalesReport { get; set; }
    }
}
```

When `PostConfirmedSalesReportToTreasuryAsync` runs in `SalesReportsController.cs`, it sets `entry.SalesReportId = report.Id`.

---

## 3. Tesseract OCR Extraction Pipeline

### A. Pre-processing & Recognition Flow
1. **Upload**: Manager takes a photo or selects an image of the bank deposit slip (BDO, BPI, Metrobank, Security Bank, etc.).
2. **Image Pre-processing (`SixLabors.ImageSharp`)**:
   - Auto-orientation from EXIF metadata.
   - Grayscale conversion and high-contrast thresholding for machine/teller ink legibility.
   - Max dimension constrained to 1800px.
3. **Tesseract Engine Processing**:
   - Analyzed using `tessdata/eng.traineddata`.
4. **Pattern Extraction**:
   - **Bank Name**: Detected from keywords (`BDO`, `BPI`, `Bank of the Philippine Islands`, `Metrobank`, `Security Bank`, `Land Bank`, `PNB`, `China Bank`, `UnionBank`).
   - **Deposit Amount**: Regex matching currency amounts (`₱`, `PHP`, `P`, or comma-delimited figures like `45,780.00`).
   - **Reference / Trace #**: Regex detecting `TRN`, `REF`, `TRACE`, `BATCH`, or teller validation lines.
   - **Transaction Date**: Regex parsing standard banking date stamps (`YYYY-MM-DD`, `MM/DD/YYYY`, `DD-MMM-YYYY`).
5. **Fallback Safety**:
   - If OCR fails or detects nothing, it returns `"success": false` without an error exception. All fields remain manually editable by the manager.

### B. OCR API Endpoint
- **Endpoint**: `POST /SalesReports/ExtractDepositSlipOcr`
- **Authorization**: `[Authorize(Roles = "Owner,Manager,Admin")]`
- **Payload**: Multipart form containing `depositSlipImage`.
- **Response**:
  ```json
  {
    "success": true,
    "detectedBank": "BDO",
    "detectedAmount": 45780.00,
    "detectedReference": "TRN-9482019",
    "detectedDate": "2026-08-15",
    "tempImageKey": "deposit_slip_temp_92f8a1.jpg"
  }
  ```

---

## 4. UI/UX Workflow & Touchpoints

### A. Sales Reports List Page (`/SalesReports/Index.cshtml`)
- A new column **"Deposit Slip"** is displayed in the list table.
- **Visual Statuses**:
  - `Confirmed` without slip $\rightarrow$ Orange badge: `Awaiting Deposit Slip` + quick `Upload Slip` modal button.
  - `Confirmed` with matched slip $\rightarrow$ Green badge: `Deposited ₱45,000 (Matched)` + click to preview image.
  - `Confirmed` with variance $\rightarrow$ Red badge: `Deposited ₱44,500 (-₱500 Short)` + tooltip with variance reason.
- **Quick-Upload Modal**:
  - Drag-and-drop or camera file selector.
  - "Extract with OCR" spinner and auto-fill.
  - Fields for Bank, Reference Number, Date, Amount, and Variance Reason (mandatory if amounts differ).
  - Submit button: saves slip and updates the table row in place.

### B. Daily Sales Review Page (`/SalesReports/ReviewManager.cshtml`)
- An integrated **"Bank Deposit Verification"** card appears in the control panel:
  - Shows uploaded slip preview if already attached.
  - Real-time comparison banner:
    $$\text{Handover Cash: ₱45,000.00} \quad \text{vs} \quad \text{Deposited: ₱45,000.00} \quad \rightarrow \quad \text{\textbf{BALANCED}}$$
  - If discrepancy exists: prompts for **"Deposit Variance Reason"** before saving.

### C. Treasury Index Page (`/Treasury/Index.cshtml`)
- In the **SALES INFLOWS** section:
  - **Red Alert Row**: If `SalesReport != null && !SalesReport.HasDepositSlip`, the row background is shaded with `bg-red-500/10`, bordered in red, and marked with a red tag: `● NO DEPOSIT SLIP`.
  - **Direct Upload Action**: Clicking `Upload Slip` on the row opens the modal directly inside the Treasury view.
  - **Green Matched State**: Once uploaded, the row turns standard styling with a green `✓ DEPOSITED` badge and a clickable slip photo viewer.
  - **Executive Banner**: At the top of the Treasury page:
    > *"⚠️ **Attention:** {N} Branch Sales Handover(s) totaling ₱{Amount} are awaiting bank deposit slips."*

---

## 5. Security & Access Control

1. **Storage Location**:
   - Slips are stored under `storage/deposit_slips/` outside the public web root (`wwwroot`).
2. **Authorized Streaming Endpoint**:
   - `GET /SalesReports/DepositSlip/{fileName}`
   - Protected with `[Authorize(Roles = "Owner,Manager,Admin,Auditor")]`.
   - Prevents public crawling or unauthorized viewing of company bank deposit receipts.
3. **Audit Trail**:
   - Every deposit slip upload records `DepositUploadedByUserId` and `DepositUploadedAt` for complete manager accountability.

---

## 6. Testing Strategy

1. **Data Model & Precision Unit Tests**:
   - Verify `SalesReport` deposit slip properties and computed properties (`HasDepositSlip`, `DepositVariance`, `IsDepositMatched`).
   - Verify `CashFlowEntry.SalesReportId` mapping.
2. **Treasury Inflow Red Indicator Tests**:
   - Test that confirmed sales reports without deposit slips render with the red warning indicator and `NO DEPOSIT SLIP` badge.
   - Test that confirmed sales reports with deposit slips render green and cleared.
3. **Discrepancy & Validation Tests**:
   - Verify that non-matching deposit amounts require a `DepositVarianceReason`.
   - Verify that matched amounts successfully save without error.
4. **OCR Parsing Tests**:
   - Unit test regex extraction of Philippine bank names, currency figures, and trace numbers from sample Tesseract output strings.
