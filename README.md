# 🪙 PCF Auditing Suite (AuditCkDayo)

An elegant, secure, and responsive Petty Cash Fund (PCF) auditing and management application. Designed with an editorial, utilitarian theme built around deep navy, slate gray, and muted slate-blue accents, the system streamlines receipt ingestion, delivery verification, and multi-tier approval workflows.

---

## 🚀 System Architecture & Workflow

The system facilitates structured, auditable expense tracking for teams managing decentralized cash floats:

```
[Buyer] ──(Uploads Receipt Images)──> [Staged Session Draft]
                                             │
                                       (Gemini OCR)
                                             │
                                             ▼
                                    [Branch Staff] 
                           (Verifies physical items at site)
                                             │
                                             ▼
                                    [Manager / Owner] 
                              (Approve/Reject & Ledger updates)
```

1. **Buyer (Ingestion)**: Captures/uploads up to 5 receipt images per transaction, reviews the unified Gemini/Azure OCR extraction draft, assigns it to an establishment, and submits.
2. **Branch Staff (Verification)**: Inspects physical goods at the establishment against the invoice details and either verifies or flags the item.
3. **Manager / Owner (Approval)**: Conducts final review via a split-screen dashboard, decides approval status, and triggers ledger adjustment transactions.

---

## 🎨 Visual Identity & Key UX Features

- **Bento Dashboard**: An asymmetrical landing page highlighting remaining float limits, pending approval counts, and available petty cash balances with progress bars and data-mono styling.
- **Full-Screen Audit Viewer**: Focused full-page modal allowing inspectors to zoom/pan the secure receipt image alongside transaction line items without leaving the queue.
- **Responsive Navigation Drawer**: Adapts dynamically from a persistent desktop sidebar to an off-canvas slide-out drawer on mobile devices (<1024px).
- **Secure File Access**: All uploaded receipt media is saved outside the public web root (`wwwroot`) and served exclusively via authorization-checked controllers to safeguard financial data.

---

## 🛠️ Tech Stack & Dependencies

- **Framework**: `.NET 9.0` (C# ASP.NET Core MVC)
- **Database**: Entity Framework Core with `Pomelo.EntityFrameworkCore.MySql` (MySQL via XAMPP)
- **OCR Engine**: `GoogleGeminiOcrService` (Google Gemini AI Model) with fallback to `AzureOcrService` (Azure Document Intelligence)
- **Security**: Blowfish Password Hashing via `BCrypt.Net-Next`
- **Testing**: xUnit with isolated SQLite and InMemory database contexts

---

## 📦 Database Schema Quick-View

The database tracks historical mutations through seven interconnected tables:

- **`Users`**: Holds role, float balances (`PcfBalance`, `DailyStartingFloat`), and manager hierarchies.
- **`Establishments`**: List of store/branch sites.
- **`AuditItems`**: Primary record of expense audits, status (`AwaitingBranchVerification`, `AwaitingManagerApproval`, `Approved`, `Rejected`), and verification timestamps.
- **`AuditItemDetails`**: Line items extracted from the receipt.
- **`AuditItemImages`**: Supports long/segmented receipts with custom display ordering.
- **`PettyCashLedgers`**: Permanent transaction record capturing funding directions, adjust type, and resulting balances.
- **`Notifications`**: Real-time system event alerts for approval queues.

---

## ⚡ Quick Start

### 1. Prerequisites
- Install [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download)
- Install [XAMPP](https://www.apache.org/xampp/) (MySQL server) and start the MySQL service.

### 2. Configuration Setup
Create a local connection string in `AuditCkDayo/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=audit_ckr_dayo;user=root;password="
  },
  "GoogleGemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY"
  }
}
```
*Alternatively, load the Gemini API Key into environment variables as `GoogleGemini__ApiKey` or via .NET User Secrets.*

### 3. Database Migration & Seeding
On startup, the system automatically applies migrations and seeds default test roles:
- **Owner**: `alice@test.com` (Password: `Password123!`)
- **Manager**: `bob@test.com` (Password: `Password123!`)
- **Buyer**: `charlie@test.com` (Password: `Password123!`)
- **Branch Staff**: `staff@test.com` (Password: `Password123!`)

### 4. Run the Web Application
```bash
cd AuditCkDayo
dotnet run
```
Open [http://localhost:5000](http://localhost:5000) or check the SSL ports in your console.

### 5. Running the Test Suite
Ensure the test project passes:
```bash
dotnet test
```
