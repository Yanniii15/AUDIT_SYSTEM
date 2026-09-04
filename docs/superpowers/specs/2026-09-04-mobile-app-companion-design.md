# AuditCkDayo Mobile Companion Application — Technical Design Specification

- **Date**: 2026-09-04
- **Status**: Validated Design
- **Target Platforms**: iOS & Android (via React Native + Expo)
- **Target Audience / Roles**: Auditor, Manager, Branch Staff
- **Design System**: Audit Emerald Clean (`audit_emerald_clean/DESIGN.md`)
- **Backend**: ASP.NET Core 9.0 Web API (`AuditCkDayo`) on Railway (`https://makbiecompanies.dev`)

---

## 1. Executive Summary & Goals

### 1.1 Context
AuditCkDayo is an operational Petty Cash Fund (PCF) auditing and branch management system running in ASP.NET Core 9.0 with MySQL. While the web interface provides complete administrative controls, on-the-ground operational workflows—delivery verification, receipt audits, daily sales logbook capture, and approval workflows—benefit significantly from a native mobile application.

### 1.2 Primary Objectives
1. **Auditor Operations**: Allow auditors to inspect date-grouped receipts, browse multi-photo receipt galleries, and perform line-item edits (sources, prices, allocations) from a phone.
2. **Manager Approval Hub**: Give managers live PCF position visibility, one-tap audit approvals/rejections (`/Audits/VerifyList`), cash surrender confirmations (`/Audits/SurrenderQueue`), and daily sales reconciliations (`/SalesReports/ReviewManager`).
3. **Branch Staff Intake**: Allow cashier and branch staff to verify deliveries at the receiving dock (`/Audits/BranchVerifyList`) and snap physical sales logbooks/reading slips directly into the daily sales pipeline (`/SalesReports/Upload`).
4. **Design Fidelity**: Faithfully replicate the `Audit Emerald Clean` design tokens, visual hierarchy, and color palettes provided in the Stitch mobile designs.

---

## 2. System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                 React Native (Expo Mobile)                  │
│       Located in /mobile monorepo subfolder                 │
│                                                             │
│  ┌────────────────┐ ┌────────────────┐ ┌────────────────┐  │
│  │  Auditor Flow  │ │  Manager Flow  │ │  Branch Staff  │  │
│  └───────┬────────┘ └───────┬────────┘ └───────┬────────┘  │
│          └──────────────────┼──────────────────┘            │
│                             ▼                               │
│              Axios Client (Bearer Token Auth)               │
│               Expo SecureStore Token Storage                │
└─────────────────────────────┬───────────────────────────────┘
                              │ HTTPS / REST API
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 ASP.NET Core 9.0 Backend                    │
│            AuditCkDayo.Controllers.Api                      │
│                                                             │
│  ┌────────────────┐ ┌────────────────┐ ┌────────────────┐  │
│  │ AuthApiController│ │AuditApiController│ │SalesApiController│
│  └────────────────┘ └────────────────┘ └────────────────┘  │
│                             │                               │
│                             ▼                               │
│              AuditDbContext (Pomelo MySQL)                  │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Technology Stack

| Layer | Component | Specification |
| :--- | :--- | :--- |
| **Mobile Runtime** | Expo / React Native | React Native 0.76+, Expo SDK 52+ |
| **Language** | TypeScript | Strict typing across API clients and components |
| **Styling** | NativeWind | Tailwind CSS utility classes using `Audit Emerald Clean` tokens |
| **Navigation** | React Navigation | `@react-navigation/bottom-tabs` & `@react-navigation/native-stack` |
| **Device Hardware** | Camera & Media | `expo-image-picker` with compression via `expo-image-manipulator` |
| **Local Storage** | Secure Tokens & Offline | `expo-secure-store` for JWTs, `expo-sqlite` for offline drafts |
| **Backend API** | ASP.NET Core 9.0 | Dedicated `AuditCkDayo.Controllers.Api` controllers |
| **Authentication** | JWT Bearer Token | `Microsoft.AspNetCore.Authentication.JwtBearer` (coexists with Cookies) |

---

## 4. Visual Identity & Design Tokens (`Audit Emerald Clean`)

All UI components adhere strictly to the token specification in `audit_emerald_clean/DESIGN.md`:

### 4.1 Color Palette
* **Primary**: `#005f37` (Deep Forest Emerald)
* **Primary Container**: `#0f7a4a` / `#a6ffc6`
* **Surface Background**: `#f1fcf2` (Soft Mint Background)
* **Surface Card**: `#ffffff` (Pure White Card Container)
* **Surface Base / Low**: `#ebf7ed` / `#F6F8F7`
* **Hairline Borders**: `#DCE5DF`
* **Text Secondary**: `#61706A`
* **Text Primary**: `#141e18`
* **Compliance / Status Tokens**:
  * Success: `#15803D` (Bg: `#DCFCE7`)
  * Warning: `#B7791F` (Bg: `#FEF3C7`)
  * Danger / No Receipt: `#DC2626` (Bg: `#FEE2E2`)
  * Info: `#2563EB` (Bg: `#DBEAFE`)
  * Draft / Muted: `#64748B` (Bg: `#F1F5F9`)

### 4.2 Typography Hierarchy
* **Headlines & Headers**: `Manrope` (Weights: 600, 700, 800)
* **Body Text & Labels**: `Inter` (Weights: 400, 500, 600)
* **Financial Amounts & Dates**: `JetBrains Mono` (Weights: 500, 700)

---

## 5. Backend REST API Specification

A dedicated API controller namespace (`AuditCkDayo.Controllers.Api`) is introduced. Existing browser Razor routes remain 100% untouched.

### 5.1 Authentication (`AuthApiController`)
* **`POST /api/auth/login`**:
  * Payload: `{ email: string, password: string }`
  * Verifies against `Users` table via `BCrypt.Net.BCrypt.Verify`.
  * Returns: `{ token: string, user: { id: int, name: string, email: string, role: string, establishmentId: int? } }`.
* **`GET /api/auth/me`**:
  * Requires: `Bearer <token>`
  * Returns active user profile, float balances, and current role.

### 5.2 Auditor Operations (`AuditApiController`)
* **`GET /api/audits/by-date?date={yyyy-MM-dd}&buyerId={id}&establishmentId={id}`**:
  * Returns: Summary stats (total audited, approved count, pending count, total receipt photos) + list of audits with all attached `AuditItemImages` and itemized `AuditItemDetails`.
* **`GET /api/audits/{id}`**:
  * Returns complete receipt payload for editing.
* **`PUT /api/audits/{id}`**:
  * Updates line items (item name, expense source name / ID, price, quantity, branch allocation notes, receipt status).

### 5.3 Manager Operations (`ManagerApiController`)
* **`GET /api/manager/dashboard`**:
  * Returns: Opening PCF float, Remaining PCF balance, cash-in today, cash-out today, queue counts.
* **`GET /api/manager/audit-queue`**:
  * Returns list of audits awaiting manager approval (`AwaitingManagerApproval`).
* **`POST /api/manager/audits/{id}/approve`**:
  * Transitions audit status to `Approved`.
* **`POST /api/manager/audits/{id}/reject`**:
  * Payload: `{ reason: string }` $\rightarrow$ transitions status to `Rejected`.
* **`GET /api/manager/surrender-queue`**:
  * Returns pending cash surrenders with buyer name, declared amount, and variance.
* **`POST /api/manager/surrenders/{id}/confirm`**:
  * Confirms cash envelope, updates buyer float, records treasury ledger transaction.

### 5.4 Branch Staff Operations (`BranchApiController` & `SalesApiController`)
* **`GET /api/branch/deliveries`**:
  * Scoped to staff's assigned `EstablishmentId`. Returns items awaiting branch delivery verification.
* **`POST /api/branch/deliveries/{detailId}/verify`**:
  * Sets `BranchVerificationStatus = Verified`.
* **`POST /api/sales-reports/upload`**:
  * Multipart form: images (up to 5), `businessDate`, `handoverDate`, `reportSection` (Opening/Closing), `cashierName`.
  * Ingests report into daily sales pipeline.

---

## 6. Screen Specifications by Role

### 6.1 Auditor Role
1. **Audit Corrections Hub**:
   * Date Stepper (`<` `Date` `>`) + dynamic quick-jump chips for dates with active audits.
   * Daily stat cards: Total Submissions, Audited Amount (₱), Approved, and Pending.
   * Multi-photo receipt gallery: interactive thumbnail switcher (**Photo 1 of 7**, **Photo 2 of 7**, etc.) with full-screen zoom.
   * Inline Line Item Editor: Tap any line to adjust Item Name, Store Source, Quantity, Unit Price, or Department Allocation.
2. **Audit Ledger & Reports**:
   * View the itemized audit ledger and export summaries directly from the phone.

### 6.2 Manager Role
1. **Executive Dashboard**:
   * Live PCF position: Manager Opening float, Remaining PCF balance, and today's Cash-in / Cash-out delta.
   * Quick action buttons: **Release PCF** and **Record Return**.
   * High-priority operational queue summary cards.
2. **Approval Queue**:
   * Filterable list: All, High Value, Flagged, by Branch.
   * Audit inspection card: Receipt photo preview, buyer details, item count, total amount.
   * Fast **Approve** and **Reject** with confirmation modal.
3. **Daily Sales Verification**:
   * Review branch-submitted opening and closing logbooks.
   * Confirm handover cash and post directly to Treasury.

### 6.3 Branch Staff Role
1. **Verify Deliveries**:
   * Real-time list of line items routed to the user's assigned branch.
   * One-tap **Verify Delivery** button at the receiving dock.
2. **Daily Sales Intake**:
   * Step-by-step form for Opening float slips and Closing register logbook readings.
   * Native camera capture for up to 5 photos with live preview and re-take options.
   * Offline drafting: auto-saves to local SQLite if branch internet drops, syncing on reconnect.

---

## 7. Error Handling & Testing Strategy

1. **Token Expiration & Interception**:
   * Axios request interceptor automatically injects `Authorization: Bearer <token>`.
   * Response interceptor catches `401 Unauthorized`, clears `SecureStore`, and routes to Login screen.
2. **Offline Mode**:
   * Network state listener (`@react-native-community/netinfo`) displays an offline banner.
   * Drafts stored in local SQLite queue until internet is restored.
3. **Automated Testing**:
   * Backend API endpoints covered by xUnit integration tests in `AuditCkDayo.Tests`.
   * Mobile components covered by Jest and React Native Testing Library.
