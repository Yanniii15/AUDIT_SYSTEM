# 📱 AuditCkDayo Mobile Application — Design Specification

> **Version**: 1.0  
> **Date**: August 27, 2026  
> **Status**: Draft  
> **Parent System**: AuditCkDayo (PCF Auditing & Management System)  
> **Target Platforms**: Android & iOS  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Goals & Objectives](#2-goals--objectives)
3. [Technology Stack](#3-technology-stack)
4. [System Architecture](#4-system-architecture)
5. [API Layer Design](#5-api-layer-design)
6. [User Roles & Permissions](#6-user-roles--permissions)
7. [Screen Specifications by Role](#7-screen-specifications-by-role)
8. [Shared Data Models](#8-shared-data-models)
9. [Camera & OCR Integration](#9-camera--ocr-integration)
10. [Offline Capability Strategy](#10-offline-capability-strategy)
11. [Push Notifications](#11-push-notifications)
12. [Security & Authentication](#12-security--authentication)
13. [UI/UX Design Guidelines](#13-uiux-design-guidelines)
14. [Voice BI Integration](#14-voice-bi-integration)
15. [Phased Roadmap](#15-phased-roadmap)
16. [Testing Strategy](#16-testing-strategy)
17. [Appendix](#17-appendix)

---

## 1. Executive Summary

This document defines the design specification for the **AuditCkDayo Mobile Application** — a native Android/iOS companion app for the existing AuditCkDayo web-based PCF Auditing & Management System.

The mobile app will empower field-based users (Buyers, Branch Staff, Managers, and Owners) to perform their daily operational tasks — receipt submission, branch verification, audit approvals, daily sales encoding, treasury monitoring, and AI voice queries — directly from their phones with a native, camera-optimized, offline-capable experience.

### Why Mobile?

| Current Pain Point | Mobile Solution |
|--------------------|-----------------|
| Receipt photos uploaded via browser file picker | Native camera capture with real-time preview |
| Approvals require opening a laptop/desktop | One-tap approve/reject with biometric confirmation |
| Sales encoding done at closing on desktop | Encode on the spot from the cashier station |
| Owner checks P&L from desktop only | Voice query from anywhere, anytime |
| No real-time alerts | Native push notifications for pending actions |
| Branch verification delayed | Instant verification at the delivery point |

---

## 2. Goals & Objectives

### Primary Goals

1. **Mobilize receipt submission** — Buyers capture and submit receipts instantly with the phone camera
2. **Accelerate audit workflows** — Managers approve/reject audits in real-time from anywhere
3. **Enable field verification** — Branch Staff verify deliveries on the spot
4. **Streamline sales encoding** — Encode daily sales logbooks at the branch counter
5. **Deliver real-time treasury visibility** — Owners monitor cash flow on the go

### Secondary Goals

6. **Offline-first daily sales** — Draft sales reports without internet, sync on reconnect
7. **Push notifications** — Alert users of pending actions immediately
8. **Biometric security** — Fingerprint/Face ID for quick, secure approvals
9. **Voice BI on mobile** — Natural voice queries for P&L insights
10. **Reduce training time** — Intuitive mobile-native UX matching existing workflows

### Non-Goals (Out of Scope for v1)

- Full system administration (User CRUD, Establishment setup) — remains on web
- Database migrations or system diagnostics — remains on web
- P&L Registration / cost category management — remains on web
- Manager Coverage configuration — remains on web (complex date-range UI)

---

## 3. Technology Stack

### Recommended: .NET MAUI

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Framework** | .NET MAUI (Multi-platform App UI) | Same .NET 9 ecosystem as backend; share models, enums, validation logic |
| **Language** | C# 13 | No context switching; same language as AuditCkDayo backend |
| **UI Toolkit** | .NET MAUI Controls + Community Toolkit | Native look & feel on both platforms |
| **HTTP Client** | `HttpClient` + `System.Text.Json` | Built-in .NET; strongly-typed API responses |
| **Local Storage** | SQLite (via `sqlite-net-pcl`) | Offline drafts, cached data, local queue |
| **Camera** | MAUI MediaPicker + CameraView | Native camera integration for receipt capture |
| **Push Notifications** | Firebase Cloud Messaging (FCM) | Android + iOS push via single provider |
| **Auth Storage** | `SecureStorage` (MAUI Essentials) | Encrypted token storage on device |
| **Biometrics** | MAUI Essentials `Authenticator` | Fingerprint / Face ID for approval confirmation |
| **Audio Recording** | MAUI `AudioRecorder` plugin | Voice BI audio capture |
| **State Management** | MVVM (CommunityToolkit.Mvvm) | Clean data binding, testable ViewModels |
| **Navigation** | Shell Navigation | Tab-based + hierarchical navigation |

### Project Structure

```
AuditCkDayo.Mobile/
├── AuditCkDayo.Mobile.sln
├── AuditCkDayo.Mobile/                    # Main MAUI App
│   ├── App.xaml / App.xaml.cs
│   ├── AppShell.xaml                      # Tab navigation shell
│   ├── MauiProgram.cs                     # DI container setup
│   ├── Models/                            # Shared domain models (DTOs)
│   │   ├── AuditItemDto.cs
│   │   ├── SalesReportDto.cs
│   │   ├── TreasuryDto.cs
│   │   ├── UserDto.cs
│   │   └── ...
│   ├── ViewModels/                        # MVVM ViewModels
│   │   ├── LoginViewModel.cs
│   │   ├── DashboardViewModel.cs
│   │   ├── AuditSubmitViewModel.cs
│   │   ├── AuditReviewViewModel.cs
│   │   ├── SalesReportViewModel.cs
│   │   ├── TreasuryViewModel.cs
│   │   ├── VoiceQueryViewModel.cs
│   │   └── ...
│   ├── Views/                             # XAML Pages
│   │   ├── LoginPage.xaml
│   │   ├── DashboardPage.xaml
│   │   ├── AuditSubmitPage.xaml
│   │   ├── AuditReviewPage.xaml
│   │   ├── SalesReportPage.xaml
│   │   ├── TreasuryPage.xaml
│   │   ├── VoiceBiPage.xaml
│   │   └── ...
│   ├── Services/                          # API clients & local services
│   │   ├── ApiClient.cs                   # Base HTTP client with auth headers
│   │   ├── AuthService.cs                 # Login, token refresh, biometrics
│   │   ├── AuditApiService.cs             # Audit CRUD endpoints
│   │   ├── SalesApiService.cs             # Sales report endpoints
│   │   ├── TreasuryApiService.cs          # Treasury endpoints
│   │   ├── VoiceApiService.cs             # Voice BI endpoint
│   │   ├── NotificationService.cs         # FCM registration & handling
│   │   ├── OfflineQueueService.cs         # Offline sync queue
│   │   └── CameraService.cs              # Photo capture & compression
│   ├── Converters/                        # XAML value converters
│   ├── Resources/                         # Fonts, images, styles
│   ├── Platforms/                          # Platform-specific code
│   │   ├── Android/
│   │   └── iOS/
│   └── Helpers/                           # Utility classes
├── AuditCkDayo.Shared/                    # Shared library (models, enums, validation)
│   ├── Enums/
│   │   ├── AuditStatus.cs
│   │   ├── UserRole.cs
│   │   ├── LedgerTransactionType.cs
│   │   └── ...
│   ├── Models/                            # DTOs shared between mobile & API
│   └── Validators/                        # Input validation rules
└── AuditCkDayo.Mobile.Tests/              # Unit tests for ViewModels & Services
```

---

## 4. System Architecture

### High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        MOBILE APP (.NET MAUI)                    │
│                                                                  │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌─────────────────┐  │
│  │  Views   │──│ViewModels│──│ Services │──│OfflineQueueSvc  │  │
│  │ (XAML)   │  │ (MVVM)   │  │(API Calls│  │(SQLite Queue)   │  │
│  └─────────┘  └──────────┘  └────┬─────┘  └────────┬────────┘  │
│                                  │                  │            │
│                            HTTPS │            Auto-Sync          │
│                                  │            on Reconnect       │
└──────────────────────────────────┼──────────────────┼────────────┘
                                   │                  │
                              ┌────▼──────────────────▼────┐
                              │     API GATEWAY (HTTPS)     │
                              │   JWT Bearer Auth + CORS    │
                              └────────────┬───────────────┘
                                           │
┌──────────────────────────────────────────┼───────────────────────┐
│                  BACKEND (ASP.NET Core 9)│                       │
│                                          │                       │
│  ┌──────────────┐    ┌──────────────────▼────────────────────┐  │
│  │ MVC Controllers│   │   API Controllers (NEW)               │  │
│  │ (Razor Views)  │   │   /api/v1/auth/*                      │  │
│  │ (Web App)      │   │   /api/v1/audits/*                    │  │
│  └───────┬────────┘   │   /api/v1/sales-reports/*             │  │
│          │            │   /api/v1/treasury/*                   │  │
│          │            │   /api/v1/notifications/*              │  │
│          │            │   /api/v1/voice/*                      │  │
│          │            │   /api/v1/pcf-monitor/*                │  │
│          │            └───────────────┬───────────────────────┘  │
│          │                            │                          │
│          ▼                            ▼                          │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   SERVICE LAYER (Shared)                 │    │
│  │  SharedPcfFundService │ CoverageService │ VoiceBiService │    │
│  │  FallbackOcrService   │ Other Services...                │    │
│  └───────────────────────────┬─────────────────────────────┘    │
│                              │                                   │
│                    ┌─────────▼──────────┐                       │
│                    │  AuditDbContext     │                       │
│                    │  (EF Core 9)       │                       │
│                    └─────────┬──────────┘                       │
│                              │                                   │
└──────────────────────────────┼───────────────────────────────────┘
                               │
                    ┌──────────▼──────────┐
                    │   MySQL / MariaDB   │
                    │   (Production DB)   │
                    └─────────────────────┘
```

### Key Architectural Decisions

1. **API Layer Added to Existing Backend** — No separate API project. New `ApiControllers` are added alongside existing MVC controllers in the same `AuditCkDayo` project, sharing the same service layer and DbContext.
2. **JWT Bearer Authentication** — Mobile uses JWT tokens (instead of cookies used by web). Both auth schemes coexist in the same backend.
3. **Shared Library** — `AuditCkDayo.Shared` contains DTOs, enums, and validation rules used by both the API controllers and the mobile app — single source of truth.
4. **Offline Queue** — Mobile caches pending submissions in local SQLite. An `OfflineQueueService` auto-syncs when connectivity is restored.

---

## 5. API Layer Design

### New API Controllers Required on Backend

The following REST API controllers must be added to the existing `AuditCkDayo` project. They will wrap the same service layer already used by MVC controllers.

#### 5.1 Authentication API

```
POST   /api/v1/auth/login              # Returns JWT + refresh token
POST   /api/v1/auth/refresh            # Refresh expired JWT
POST   /api/v1/auth/logout             # Invalidate refresh token
GET    /api/v1/auth/me                 # Current user profile & role
```

**Request: Login**
```json
{
  "username": "string",
  "password": "string",
  "deviceId": "string",
  "fcmToken": "string"
}
```

**Response: Login**
```json
{
  "accessToken": "string (JWT)",
  "refreshToken": "string",
  "expiresIn": 3600,
  "user": {
    "id": 1,
    "username": "string",
    "fullName": "string",
    "role": "Buyer|Manager|Owner|BranchStaff|Admin",
    "establishmentId": 1,
    "establishmentName": "CKR Main",
    "pcfBalance": 5000.00,
    "isTreasury": false
  }
}
```

#### 5.2 Audits API

```
GET    /api/v1/audits                  # List audits (filtered by role, status, date range)
GET    /api/v1/audits/{id}             # Audit detail with line items & images
POST   /api/v1/audits                  # Submit new audit (multipart: images + JSON body)
PUT    /api/v1/audits/{id}/verify      # Branch staff verification
PUT    /api/v1/audits/{id}/approve     # Manager/Owner approval
PUT    /api/v1/audits/{id}/reject      # Manager/Owner rejection (with reason)
PUT    /api/v1/audits/{id}/cancel      # Buyer cancellation
POST   /api/v1/audits/{id}/ocr        # Trigger OCR on uploaded receipt images
GET    /api/v1/audits/summary          # Dashboard summary counts by status
```

**Request: Submit Audit** (`multipart/form-data`)
```
images[]:            File[]           # Up to 5 receipt images
storeName:           string           # Store/supplier name
receiptDate:         date             # Date on receipt
remarks:             string           # Optional notes
items[0].itemName:   string           # Line item description
items[0].quantity:   int
items[0].unitPrice:  decimal
items[0].total:      decimal
items[0].destinationEstablishmentId: int
items[0].pnlCategoryId: int
...
```

#### 5.3 Sales Reports API

```
GET    /api/v1/sales-reports                    # List sales reports (by branch, date range)
GET    /api/v1/sales-reports/{id}               # Detail with lines & breakdown
POST   /api/v1/sales-reports                    # Submit daily sales report (multipart)
PUT    /api/v1/sales-reports/{id}/confirm       # Manager/Owner confirmation → treasury posting
PUT    /api/v1/sales-reports/{id}/reject        # Rejection with reason
GET    /api/v1/sales-reports/summary            # Dashboard summary
```

**Request: Submit Sales Report** (`multipart/form-data`)
```
reportDate:          date
establishmentId:     int
images[]:            File[]           # Logbook page photos
grossSales:          decimal
foodSales:           decimal
beerSales:           decimal
beverageSales:       decimal
otherSales:          decimal
cashSales:           decimal
gcashAmount:         decimal
bankTransferAmount:  decimal
cardAmount:          decimal
creditAmount:        decimal
runawayAmount:       decimal
seniorDiscount:      decimal
pwdDiscount:         decimal
loyaltyDiscount:     decimal
giftVoucherDiscount: decimal
employeeDiscount:    decimal
pcfUsage:            decimal
cashShortage:        decimal
cashOverage:         decimal
denominations[]:     { denomination: int, count: int }[]
remarks:             string
```

#### 5.4 Treasury API

```
GET    /api/v1/treasury/daily/{date}            # Daily cash flow summary
GET    /api/v1/treasury/entries                  # Cash flow entries (filtered)
POST   /api/v1/treasury/cash-in                  # Manual cash-in entry
POST   /api/v1/treasury/cash-out                 # Manual cash-out entry
GET    /api/v1/treasury/pcf-releases             # PCF release history
POST   /api/v1/treasury/pcf-releases             # Issue PCF release to buyer/branch
GET    /api/v1/treasury/balance                  # Current treasury balance
```

#### 5.5 PCF Monitor API

```
GET    /api/v1/pcf-monitor                      # All PCF balances (by user or branch)
GET    /api/v1/pcf-monitor/{userId}/ledger       # Ledger history for a user
GET    /api/v1/pcf-monitor/my-balance            # Current user's own PCF balance & float
```

#### 5.6 Surrender API

```
GET    /api/v1/surrenders                        # List surrender requests
POST   /api/v1/surrenders                        # Submit cash surrender
PUT    /api/v1/surrenders/{id}/approve           # Approve with confirmed amount
PUT    /api/v1/surrenders/{id}/reject            # Reject surrender
```

#### 5.7 Notifications API

```
GET    /api/v1/notifications                     # List notifications (paginated)
PUT    /api/v1/notifications/{id}/read           # Mark as read
PUT    /api/v1/notifications/read-all            # Mark all as read
GET    /api/v1/notifications/unread-count        # Badge count
POST   /api/v1/notifications/register-device     # Register FCM token
```

#### 5.8 Voice BI API

```
POST   /api/v1/voice/query                      # Upload audio → transcribe → answer
```

**Request** (`multipart/form-data`)
```
audio:     File     # Audio recording (WAV/M4A)
language:  string   # "en" or "tl" (Tagalog)
```

**Response**
```json
{
  "transcription": "What are my total sales this month?",
  "answer": "Your total sales across all branches this month is ₱1,247,350.00...",
  "data": { ... }
}
```

#### 5.9 Reports API

```
GET    /api/v1/reports/pnl                       # P&L summary (date range, branch filter)
GET    /api/v1/reports/audit-log                  # Audit activity log
GET    /api/v1/reports/pcf-summary                # PCF utilization report
GET    /api/v1/reports/sales-summary              # Sales performance report
```

---

## 6. User Roles & Permissions

### Mobile Feature Access Matrix

| Feature | Owner | Admin | Manager | Buyer | BranchStaff |
|---------|:-----:|:-----:|:-------:|:-----:|:-----------:|
| **Dashboard** | ✅ All branches | ✅ All branches | ✅ Own branch | ✅ Own audits | ✅ Own branch |
| **Submit Audit** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Verify Delivery** | ❌ | ❌ | ❌ | ❌ | ✅ |
| **Approve/Reject Audit** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Submit Sales Report** | ❌ | ❌ | ❌ | ❌ | ✅ |
| **Confirm Sales Report** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Treasury Operations** | ✅ | ❌ | ✅ (if treasury) | ❌ | ❌ |
| **PCF Monitor** | ✅ All | ✅ All | ✅ Own team | ✅ Own balance | ✅ Own balance |
| **Cash Surrender** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **Approve Surrender** | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Voice BI** | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Reports / P&L** | ✅ | ✅ | ✅ (own branch) | ❌ | ❌ |
| **Notifications** | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## 7. Screen Specifications by Role

### 7.1 Common Screens (All Roles)

#### Login Screen
- Username + Password fields
- "Remember Me" toggle (stores token securely)
- Biometric login option (after first successful login)
- App logo and branding

#### Dashboard Screen
- Role-specific summary cards (pending actions, balances, alerts)
- Quick action buttons (Submit Audit, Verify Delivery, etc.)
- Recent activity feed
- Notification bell with unread badge count

#### Notification Center
- Scrollable notification list with read/unread indicators
- Tap to navigate to related item (audit, sales report, etc.)
- "Mark all as read" action
- Pull-to-refresh

#### Profile / Settings
- Current user info (name, role, branch)
- Current PCF balance display
- Biometric toggle on/off
- Notification preferences
- App version info
- Logout button

---

### 7.2 Buyer Screens

#### Audit Submission Screen
```
┌─────────────────────────────────┐
│ 📸 New Receipt Audit            │
├─────────────────────────────────┤
│                                 │
│  ┌───────┐ ┌───────┐ ┌─────┐  │
│  │ Photo │ │ Photo │ │  +  │  │
│  │   1   │ │   2   │ │ Add │  │
│  └───────┘ └───────┘ └─────┘  │
│                                 │
│  Store Name: [____________]     │
│  Receipt Date: [__/__/____]     │
│                                 │
│  ── Line Items ──────────────   │
│  Item: [__________]             │
│  Qty: [___] × ₱[_____] = ₱___  │
│  Branch: [▼ CKR Main     ]     │
│  Category: [▼ COGS        ]     │
│  [+ Add Another Item]          │
│                                 │
│  ── OCR Extract ─────────────   │
│  [🔍 Auto-Extract from Photo]  │
│                                 │
│  Remarks: [________________]    │
│                                 │
│  Your PCF Balance: ₱5,000.00   │
│  This Audit Total: ₱1,250.00   │
│  Remaining After:  ₱3,750.00   │
│                                 │
│  [       Submit Audit       ]   │
│                                 │
└─────────────────────────────────┘
```

- Camera opens natively; captured images shown as thumbnails
- OCR auto-extract button populates line items from receipt
- Real-time PCF balance display with projected remaining
- Validation: total cannot exceed current PCF balance

#### My Audits Screen
- List view with status badges (color-coded)
- Filter tabs: All | Pending | Approved | Rejected
- Tap to view audit detail with timeline
- Pull-to-refresh

#### Cash Surrender Screen
- Current PCF balance prominently displayed
- Amount to surrender input (cannot exceed balance)
- Physical cash count confirmation fields
- Submit button

---

### 7.3 Branch Staff Screens

#### Delivery Verification Screen
```
┌─────────────────────────────────┐
│ ✅ Verify Delivery              │
├─────────────────────────────────┤
│                                 │
│  Audit #1042                    │
│  Submitted by: Maria (Buyer)    │
│  Date: Aug 26, 2026             │
│                                 │
│  📸 Receipt Photos              │
│  [Photo 1] [Photo 2]           │
│                                 │
│  ── Items to Verify ──────────  │
│  ☑ 10x Chicken Wings    ₱1,500 │
│  ☑ 5x Cooking Oil 1L    ₱750   │
│  ☐ 2x Paper Towels      ₱200   │
│                                 │
│  Total: ₱2,450.00              │
│                                 │
│  [✅ Confirm Delivery]          │
│                                 │
└─────────────────────────────────┘
```

- Shows receipt images (pinch-to-zoom)
- Checklist of items to physically verify
- Simple one-tap confirmation

#### Daily Sales Report Screen
```
┌─────────────────────────────────┐
│ 📊 Daily Sales Report           │
├─────────────────────────────────┤
│                                 │
│  Date: [Aug 26, 2026]          │
│  Branch: CKR Main              │
│                                 │
│  📸 Logbook Photos              │
│  [Photo 1] [+ Add]             │
│                                 │
│  ── Sales Breakdown ──────────  │
│  Gross Sales:     ₱[________]  │
│  Food Sales:      ₱[________]  │
│  Beer Sales:      ₱[________]  │
│  Beverage Sales:  ₱[________]  │
│  Other Sales:     ₱[________]  │
│                                 │
│  ── Payments ─────────────────  │
│  Cash:            ₱[________]  │
│  GCash:           ₱[________]  │
│  Bank Transfer:   ₱[________]  │
│  Card:            ₱[________]  │
│  Credit:          ₱[________]  │
│  Run-away:        ₱[________]  │
│                                 │
│  ── Discounts ────────────────  │
│  Senior:          ₱[________]  │
│  PWD:             ₱[________]  │
│  Loyalty:         ₱[________]  │
│  Gift Voucher:    ₱[________]  │
│  Employee:        ₱[________]  │
│                                 │
│  ── Cash Denominations ───────  │
│  ₱1000 × [__] = ₱_______      │
│  ₱500  × [__] = ₱_______      │
│  ₱200  × [__] = ₱_______      │
│  ₱100  × [__] = ₱_______      │
│  ₱50   × [__] = ₱_______      │
│  ₱20   × [__] = ₱_______      │
│  ₱10   × [__] = ₱_______      │
│  ₱5    × [__] = ₱_______      │
│  ₱1    × [__] = ₱_______      │
│  Coins  × [__] = ₱_______     │
│                                 │
│  PCF Usage:       ₱[________]  │
│  Shortage:        ₱[________]  │
│  Overage:         ₱[________]  │
│                                 │
│  ══════════════════════════════ │
│  Net Cash on Hand: ₱12,450.00  │
│  ══════════════════════════════ │
│                                 │
│  [💾 Save Draft]  [📤 Submit]  │
│                                 │
└─────────────────────────────────┘
```

- Auto-calculates totals and validates cross-balances
- Save Draft stores locally (offline-capable)
- Submit sends to backend for manager review

---

### 7.4 Manager Screens

#### Audit Review Screen
```
┌─────────────────────────────────┐
│ 📋 Audit Review                 │
├─────────────────────────────────┤
│                                 │
│  Audit #1042 • AwaitingApproval │
│  By: Maria Santos (Buyer)       │
│  Store: Puregold Taft           │
│  Date: Aug 26, 2026             │
│                                 │
│  📸 Receipt Photos              │
│  ┌────────────────────────────┐ │
│  │    [Swipeable Gallery]     │ │
│  └────────────────────────────┘ │
│                                 │
│  ✅ Branch Verified: Aug 26     │
│     by: Juan (Branch Staff)     │
│                                 │
│  ── Line Items ──────────────   │
│  10x Chicken Wings              │
│  ₱150.00 ea → ₱1,500.00        │
│  Branch: CKR Main │ Cat: COGS  │
│  ─────────────────────────────  │
│  5x Cooking Oil 1L              │
│  ₱150.00 ea → ₱750.00          │
│  Branch: CKR Main │ Cat: COGS  │
│  ─────────────────────────────  │
│                                 │
│  TOTAL: ₱2,250.00              │
│                                 │
│  ┌─────────┐    ┌────────────┐ │
│  │❌ Reject │    │ ✅ Approve │ │
│  └─────────┘    └────────────┘ │
│                                 │
│  Rejection Reason (if reject):  │
│  [________________________]     │
│                                 │
│  🔐 Confirm with Fingerprint   │
│                                 │
└─────────────────────────────────┘
```

- Swipeable image gallery with pinch-to-zoom
- Full audit trail timeline
- Biometric confirmation for approve/reject actions

#### Pending Actions Dashboard (Manager)
- Tab: Pending Audits | Pending Sales | Pending Surrenders
- Count badges on each tab
- Swipe-to-approve/reject (optional quick action)

#### Sales Report Review Screen
- View submitted daily sales with logbook photos
- Verify totals and cash denomination counts
- One-tap confirm → auto-posts to treasury
- Reject with reason

---

### 7.5 Owner Screens

#### Executive Dashboard
```
┌─────────────────────────────────┐
│ 🏠 Dashboard                    │
├─────────────────────────────────┤
│                                 │
│  Good morning, Boss!            │
│                                 │
│  ── Today's Overview ─────────  │
│  ┌──────────┐  ┌──────────┐   │
│  │ ₱48,250  │  │ ₱12,400  │   │
│  │Total Sales│  │Treasury  │   │
│  └──────────┘  └──────────┘   │
│  ┌──────────┐  ┌──────────┐   │
│  │    5     │  │    3     │   │
│  │ Pending  │  │ Pending  │   │
│  │ Audits   │  │ Sales    │   │
│  └──────────┘  └──────────┘   │
│                                 │
│  ── Branch Performance ───────  │
│  CKR Main    ₱18,500  📈 +12% │
│  Branch 2    ₱15,200  📉 -3%  │
│  Branch 4    ₱14,550  📈 +8%  │
│                                 │
│  ── Quick Actions ────────────  │
│  [🎙️ Voice Query]              │
│  [📊 View P&L Report]          │
│  [💰 Treasury Summary]         │
│                                 │
└─────────────────────────────────┘
```

#### Voice BI Screen
```
┌─────────────────────────────────┐
│ 🎙️ Ask About Your Business     │
├─────────────────────────────────┤
│                                 │
│         ┌──────────┐           │
│         │          │           │
│         │   🎙️    │           │
│         │          │           │
│         └──────────┘           │
│      Hold to Ask a Question     │
│                                 │
│  ── Recent Questions ─────────  │
│                                 │
│  🗣️ "Total sales this week?"   │
│  📊 Your total sales across     │
│     all branches this week is   │
│     ₱327,450.00, up 8% from    │
│     last week...                │
│                                 │
│  🗣️ "How much COGS for Main?"  │
│  📊 CKR Main's cost of goods   │
│     sold this month is          │
│     ₱145,200.00...              │
│                                 │
└─────────────────────────────────┘
```

- Hold-to-record microphone button
- Speech-to-text shown in real time
- AI response displayed and read aloud via TTS

---

## 8. Shared Data Models

### DTOs (Data Transfer Objects) — Shared Between API & Mobile

```csharp
// AuditCkDayo.Shared/Models/AuditItemDto.cs
public class AuditItemDto
{
    public int Id { get; set; }
    public string StoreName { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string Status { get; set; }            // "AwaitingBranchVerification", etc.
    public decimal TotalAmount { get; set; }
    public string SubmittedByName { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? Remarks { get; set; }
    public List<AuditItemDetailDto> Details { get; set; }
    public List<string> ImageUrls { get; set; }
}

public class AuditItemDetailDto
{
    public int Id { get; set; }
    public string ItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public string DestinationEstablishment { get; set; }
    public string PnlCategoryName { get; set; }
}

// AuditCkDayo.Shared/Models/SalesReportDto.cs
public class SalesReportDto
{
    public int Id { get; set; }
    public DateTime ReportDate { get; set; }
    public string EstablishmentName { get; set; }
    public string Status { get; set; }
    public decimal GrossSales { get; set; }
    public decimal FoodSales { get; set; }
    public decimal BeerSales { get; set; }
    public decimal BeverageSales { get; set; }
    public decimal OtherSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal GCashAmount { get; set; }
    public decimal BankTransferAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunawayAmount { get; set; }
    public decimal CashShortage { get; set; }
    public decimal CashOverage { get; set; }
    public decimal PcfUsage { get; set; }
    public decimal NetCashOnHand { get; set; }
    public List<CashBreakdownLineDto> Denominations { get; set; }
    public List<string> ImageUrls { get; set; }
}

// AuditCkDayo.Shared/Models/DashboardDto.cs
public class DashboardDto
{
    public decimal TotalSalesToday { get; set; }
    public decimal TreasuryBalance { get; set; }
    public int PendingAudits { get; set; }
    public int PendingSalesReports { get; set; }
    public int PendingSurrenders { get; set; }
    public decimal MyPcfBalance { get; set; }
    public List<BranchSummaryDto> BranchSummaries { get; set; }
}
```

### Shared Enums

```csharp
// AuditCkDayo.Shared/Enums/AuditStatus.cs
public enum AuditStatus
{
    AwaitingBranchVerification,
    AwaitingManagerApproval,
    Approved,
    Rejected,
    Cancelled
}

// AuditCkDayo.Shared/Enums/UserRole.cs
public enum UserRole
{
    Owner,
    Admin,
    Manager,
    Buyer,
    BranchStaff
}

// AuditCkDayo.Shared/Enums/LedgerTransactionType.cs
public enum LedgerTransactionType
{
    VaultFunding,
    ManagerFunding,
    ExpenseDeduction,
    ReversalRefund,
    CashSurrender,
    ManualAdjustment
}
```

---

## 9. Camera & OCR Integration

### Receipt Capture Flow

```
┌──────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Camera   │────>│ ImageSharp   │────>│  Upload to   │────>│  OCR Extract │
│  Capture  │     │ Compress     │     │  API Server  │     │  (Server)    │
│  (Native) │     │ (Client-side)│     │              │     │              │
└──────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                                                                    │
                                                                    ▼
                                                           ┌──────────────┐
                                                           │ Auto-populate│
                                                           │ Line Items   │
                                                           └──────────────┘
```

### Client-Side Image Processing

```csharp
// Before upload, compress on-device to reduce bandwidth
public class CameraService
{
    public async Task<byte[]> CaptureAndCompressAsync()
    {
        var photo = await MediaPicker.CapturePhotoAsync();
        using var stream = await photo.OpenReadAsync();
        using var image = await Image.LoadAsync(stream);

        // Downscale to max 1800px (matches backend OCR preprocessing)
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(1800, 1800)
        }));

        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 82 });
        return output.ToArray();
    }
}
```

---

## 10. Offline Capability Strategy

### Offline-First Features

| Feature | Offline Behavior |
|---------|-----------------|
| **Daily Sales Report** | Full draft saved to local SQLite; auto-syncs on reconnect |
| **Audit Submission** | Queued locally with images; uploaded when connectivity returns |
| **Cash Surrender** | Queued locally; submitted on reconnect |
| **Dashboard Data** | Last-fetched data cached and displayed with "Last updated" timestamp |
| **Notifications** | Cached list; badge count may be stale |

### Sync Architecture

```csharp
public class OfflineQueueService
{
    private readonly SQLiteConnection _db;
    private readonly IConnectivity _connectivity;

    // Queue a pending action
    public async Task EnqueueAsync(QueuedAction action)
    {
        action.QueuedAt = DateTime.UtcNow;
        action.Status = QueueStatus.Pending;
        _db.Insert(action);

        if (_connectivity.NetworkAccess == NetworkAccess.Internet)
            await ProcessQueueAsync();
    }

    // Process all pending items when online
    public async Task ProcessQueueAsync()
    {
        var pending = _db.Table<QueuedAction>()
            .Where(a => a.Status == QueueStatus.Pending)
            .OrderBy(a => a.QueuedAt)
            .ToList();

        foreach (var action in pending)
        {
            try
            {
                await ExecuteActionAsync(action);
                action.Status = QueueStatus.Completed;
                _db.Update(action);
            }
            catch (HttpRequestException)
            {
                // Will retry next time
                break;
            }
        }
    }
}
```

### Connectivity Monitoring

```csharp
// Auto-sync when network comes back
Connectivity.ConnectivityChanged += async (s, e) =>
{
    if (e.NetworkAccess == NetworkAccess.Internet)
    {
        await _offlineQueue.ProcessQueueAsync();
        await _dashboardVm.RefreshAsync();
    }
};
```

---

## 11. Push Notifications

### Notification Events

| Event | Recipients | Priority |
|-------|-----------|----------|
| New audit submitted | Manager, Owner | High |
| Audit approved | Buyer (submitter) | Normal |
| Audit rejected | Buyer (submitter) | High |
| Delivery awaiting verification | Branch Staff | High |
| Sales report submitted | Manager, Owner | Normal |
| Sales report confirmed | Branch Staff (submitter) | Normal |
| Cash surrender submitted | Manager, Owner | Normal |
| PCF balance low (< 20% of float) | Buyer, Manager | High |
| PCF release issued | Buyer/Branch (recipient) | Normal |

### Implementation

```csharp
// Backend: Send push notification after audit action
public class NotificationPushService
{
    private readonly FirebaseMessaging _fcm;

    public async Task SendAuditApprovedAsync(AuditItem audit)
    {
        var deviceTokens = await GetDeviceTokensForUser(audit.SubmittedById);

        var message = new MulticastMessage
        {
            Tokens = deviceTokens,
            Notification = new Notification
            {
                Title = "Audit Approved ✅",
                Body = $"Your receipt from {audit.StoreName} (₱{audit.TotalAmount:N2}) has been approved."
            },
            Data = new Dictionary<string, string>
            {
                ["type"] = "audit_approved",
                ["auditId"] = audit.Id.ToString()
            }
        };

        await _fcm.SendEachForMulticastAsync(message);
    }
}
```

---

## 12. Security & Authentication

### Authentication Flow

```
┌──────────┐                    ┌──────────────┐
│  Mobile   │  POST /auth/login │   Backend    │
│   App     │──────────────────>│   API        │
│           │                   │              │
│           │  JWT + Refresh    │  Validate    │
│           │<──────────────────│  BCrypt pwd  │
│           │                   │  Issue JWT   │
│           │                   └──────────────┘
│           │
│  Store tokens in
│  SecureStorage (encrypted)
│           │
│  All subsequent requests:
│  Authorization: Bearer <JWT>
└──────────┘
```

### Security Measures

| Measure | Implementation |
|---------|---------------|
| **Password Hashing** | BCrypt (existing backend) |
| **Token Storage** | MAUI `SecureStorage` (Keychain on iOS, KeyStore on Android) |
| **Token Expiry** | JWT access token: 1 hour; Refresh token: 30 days |
| **Biometric Gate** | Required for approve/reject actions (configurable) |
| **Certificate Pinning** | Pin SSL certificate for API domain |
| **Request Signing** | HMAC signature on financial transactions (optional v2) |
| **Session Invalidation** | Server-side refresh token revocation on logout |
| **Auto-Logout** | After 15 minutes of inactivity |

### Dual Auth Scheme on Backend

```csharp
// Program.cs — Support both Cookie (web) and JWT (mobile)
builder.Services.AddAuthentication()
    .AddCookie("CookieAuth", options => { /* existing config */ })
    .AddJwtBearer("BearerAuth", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// API controllers use [Authorize(AuthenticationSchemes = "BearerAuth")]
// MVC controllers continue using [Authorize(AuthenticationSchemes = "CookieAuth")]
```

---

## 13. UI/UX Design Guidelines

### Design Principles

1. **Filipino-first language support** — UI labels in English, but peso (₱) formatting, Philippine date formats
2. **Fat finger-friendly** — Minimum 48dp touch targets, generous spacing
3. **One-handed operation** — Critical actions reachable with thumb
4. **Glanceable dashboards** — Key numbers visible without scrolling
5. **Consistent status colors** — 🟢 Approved, 🔴 Rejected, 🟡 Pending, 🔵 Verified

### Color Palette

```
Primary:       #1E40AF (Blue 800)       — Headers, primary buttons
Secondary:     #059669 (Emerald 600)    — Approved states, success
Warning:       #D97706 (Amber 600)      — Pending states
Danger:        #DC2626 (Red 600)        — Rejected, errors
Background:    #F8FAFC (Slate 50)       — App background
Surface:       #FFFFFF                  — Cards and surfaces
Text Primary:  #1E293B (Slate 800)      — Main text
Text Secondary:#64748B (Slate 500)      — Secondary text
```

### Typography

```
Headings:    Inter Bold, 20sp / 18sp / 16sp
Body:        Inter Regular, 14sp
Caption:     Inter Regular, 12sp
Amounts:     Inter SemiBold, 16sp (monospace alignment for currency)
```

### Navigation Structure

```
┌──────────────────────────────────────┐
│  AppShell (Tab Navigation)           │
├──────────────────────────────────────┤
│                                      │
│  🏠 Home    📋 Tasks    💰 PCF    👤 │
│                                      │
│  Home     = Role-specific dashboard  │
│  Tasks    = Pending actions queue    │
│  PCF      = Balance & ledger history │
│  Profile  = Settings & logout        │
│                                      │
│  Owner gets extra tab:               │
│  🏠 Home  📋 Tasks  📊 Reports  🎙️  │
│                                      │
└──────────────────────────────────────┘
```

---

## 14. Voice BI Integration

### Mobile Voice Flow

```
1. User holds microphone button
2. Native audio recording starts (M4A format)
3. User releases button
4. Audio file compressed and sent to POST /api/v1/voice/query
5. Backend:
   a. Sends audio to Groq Whisper → transcription text
   b. Fetches real-time P&L data via VoiceBiService
   c. Sends transcription + P&L JSON to Gemini 3.7 Flash
   d. Returns { transcription, answer, data }
6. Mobile displays answer text
7. Mobile speaks answer aloud via platform TTS
```

### Accessibility

- VoiceOver / TalkBack compatible
- Answer text also displayed for reading
- Configurable TTS speed and language
- Works with Bluetooth headsets

---

## 15. Phased Roadmap

### Phase 1: Foundation (Weeks 1–3)
> **Goal**: API layer + Authentication + Dashboard

- [ ] Create `AuditCkDayo.Shared` project (DTOs, enums)
- [ ] Add JWT authentication to backend (`BearerAuth` scheme)
- [ ] Build `ApiAuthController` (login, refresh, logout, me)
- [ ] Build `ApiDashboardController` (role-specific summary)
- [ ] Build `ApiNotificationsController`
- [ ] Set up .NET MAUI project structure
- [ ] Implement Login screen with SecureStorage
- [ ] Implement Dashboard screen per role
- [ ] Implement Notification Center
- [ ] Implement biometric login toggle

### Phase 2: Core Audit Workflow (Weeks 4–6)
> **Goal**: Full audit lifecycle on mobile

- [ ] Build `ApiAuditsController` (CRUD + status transitions)
- [ ] Implement camera capture + image compression
- [ ] Build Audit Submission screen (Buyer)
- [ ] Build OCR auto-extract integration
- [ ] Build Delivery Verification screen (Branch Staff)
- [ ] Build Audit Review screen (Manager/Owner)
- [ ] Build My Audits list with filters
- [ ] Biometric confirmation for approve/reject

### Phase 3: Sales & Treasury (Weeks 7–9)
> **Goal**: Daily sales encoding + treasury visibility

- [ ] Build `ApiSalesReportsController`
- [ ] Build `ApiTreasuryController`
- [ ] Implement Daily Sales Report form (Branch Staff)
- [ ] Implement Sales Report Review screen (Manager/Owner)
- [ ] Implement Treasury dashboard (Owner/Manager)
- [ ] Save Draft / offline sales report capability
- [ ] Auto-post confirmed sales to treasury

### Phase 4: PCF & Surrenders (Weeks 10–11)
> **Goal**: PCF monitoring + cash surrender workflow

- [ ] Build `ApiPcfMonitorController`
- [ ] Build `ApiSurrenderController`
- [ ] Implement PCF Balance screen with ledger history
- [ ] Implement Cash Surrender submission
- [ ] Implement Surrender approval screen
- [ ] PCF low-balance warning notifications

### Phase 5: Voice BI & Reports (Weeks 12–13)
> **Goal**: AI voice queries + executive reporting

- [ ] Build `ApiVoiceController`
- [ ] Build `ApiReportsController`
- [ ] Implement Voice BI screen with hold-to-record
- [ ] Implement TTS response playback
- [ ] Implement P&L Report viewer
- [ ] Implement Sales Summary report

### Phase 6: Push Notifications & Offline Sync (Weeks 14–15)
> **Goal**: Real-time alerts + offline resilience

- [ ] Integrate Firebase Cloud Messaging (Android + iOS)
- [ ] Backend push notification service
- [ ] Device token registration API
- [ ] Implement OfflineQueueService with SQLite
- [ ] Implement connectivity monitoring + auto-sync
- [ ] Offline draft indicators in UI

### Phase 7: Polish & Release (Weeks 16–17)
> **Goal**: Testing, optimization, app store preparation

- [ ] End-to-end testing on physical devices
- [ ] Performance optimization (image loading, list virtualization)
- [ ] App icon, splash screen, branding
- [ ] Android Play Store listing preparation
- [ ] iOS App Store listing preparation (if applicable)
- [ ] Beta testing with actual branch staff
- [ ] Bug fixes from beta feedback
- [ ] Production release

---

## 16. Testing Strategy

### Mobile App Tests

| Layer | Tool | What to Test |
|-------|------|-------------|
| **ViewModels** | xUnit + Moq | State management, command execution, validation |
| **Services** | xUnit + MockHttp | API calls, error handling, retry logic |
| **Offline Queue** | xUnit + SQLite | Queue/dequeue, sync order, conflict resolution |
| **UI** | MAUI UI Tests / Appium | Navigation flows, form submissions, camera mock |
| **Integration** | Device Farm | End-to-end on real Android/iOS devices |

### API Tests (Backend)

| Type | Tool | Coverage |
|------|------|----------|
| **Unit** | xUnit (existing) | Service layer logic (already 5,300+ lines) |
| **API Integration** | xUnit + WebApplicationFactory | All API endpoints, auth, response shapes |
| **Contract** | Shared DTO validation | Ensure mobile & API agree on data shapes |

---

## 17. Appendix

### A. Minimum Device Requirements

| Platform | Minimum Version |
|----------|----------------|
| Android | 8.0 (API 26) Oreo |
| iOS | 15.0 |

### B. Estimated App Size

| Component | Size |
|-----------|------|
| Base MAUI app | ~15 MB |
| Dependencies (ImageSharp, SQLite) | ~5 MB |
| Resources (fonts, icons) | ~2 MB |
| **Total estimated** | **~22 MB** |

### C. Backend Changes Summary

| Change | Effort | Risk |
|--------|--------|------|
| Add JWT auth scheme | Low | Low — coexists with cookies |
| Create API controllers | Medium | Low — wraps existing services |
| Add `AuditCkDayo.Shared` project | Low | Low — extract existing DTOs |
| FCM push notification service | Medium | Low — new addition |
| Device token storage (DB table) | Low | Low — new migration |

### D. Dependencies to Add

**Backend (NuGet)**:
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `FirebaseAdmin` (for FCM push)

**Mobile (NuGet)**:
- `CommunityToolkit.Mvvm`
- `CommunityToolkit.Maui`
- `sqlite-net-pcl`
- `SixLabors.ImageSharp`
- `Plugin.Firebase.CloudMessaging`
- `Plugin.Maui.Audio`

---

> **Document Status**: Draft — Ready for review and feedback  
> **Next Steps**: Review and approve this spec, then begin Phase 1 implementation
