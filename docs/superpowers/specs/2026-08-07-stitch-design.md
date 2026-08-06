# PCF Auditing Suite (AuditCkDayo) - Stitch Design Specification

This document provides layout patterns, color schemes, responsive navigation drawers, and bento grids for the Petty Cash Fund (PCF) Auditing Suite. Use this specification to generate visual designs, wireframes, or prototypes on Stitch.

---

## 1. Visual Theme & Color Palette

The suite uses an editorial, utilitarian design system built around deep navy, slate gray, and muted slate-blue accents.

```css
:root {
  --primary: #041632;                    /* Deep Navy */
  --primary-fixed-dim: #b7c7eb;          /* Soft Ice Blue */
  --secondary: #505F76;                  /* Slate Gray */
  --background: #F9F9F9;                 /* Light Gray Page BG */
  --surface-lowest: #FFFFFF;             /* Pure White Containers */
  --surface-low: #F3F3F4;                /* Off-white Containers */
  --surface-border: #E2E8F0;             /* Cool Gray Borders */
  --text-primary: #1A1C1C;               /* Near Black Body */
  --text-muted: #44474D;                 /* Medium Gray Subtext */
  
  /* Status Colors */
  --audit-pending: #3B5B92;              /* Muted Blue (Calm Pending State) */
  --audit-success: #10B981;              /* Emerald Green (Approved) */
  --audit-error: #EF4444;                /* Crimson Red (Rejected) */
}
```

Typography:
- **Display Headings**: Hanken Grotesk (Bold, tight tracking)
- **Body & Labels**: Inter (Clean sans-serif)
- **Financial Counters**: Monospace/Data Fonts

---

## 2. Layout Structure & Responsive Shell

The global shell adapts between desktop layouts and touch-friendly mobile drawers.

### A. Desktop View (≥1024px)
- **Sidebar**: Persistent `aside` panel fixed to the left (`w-72`).
- **Workspace**: Main container starts with `pl-72` offset.
- **Top Header**: Fixed to the top with offset `left-72`.

### B. Mobile/Tablet View (<1024px)
- **Sidebar**: Off-canvas drawer shifted using CSS translation (`-translate-x-full`). It slides in on command.
- **Backdrop Overlay**: Dynamic semi-transparent background (`bg-black/50`) showing behind the open mobile drawer to capture clicks and close the menu.
- **Workspace**: Full width (`pl-0`), header spans `left-0`.
- **Top Header Menu Button**: Burger menu icon on the far left.

---

## 3. Bento Dashboard Grid

The landing page features an asymmetric Bento snapshot:

1. **Filtered Total Audited Card** (Col-span: 12 on mobile, 4 on desktop)
   - Large Monospace balance display: `₱0.00`
   - Utilitarian progress bar tracking weekly limit usage.
2. **Pending Verification Counter** (Col-span: 12 on mobile, 4 on desktop)
   - Accent colored number (`--audit-pending`).
   - Split segmented sub-bars showing queue items.
3. **Current PCF Balance** (Col-span: 12 on mobile, 4 on desktop, primary navy background)
   - Contrast text highlighting current user's available funds.
   - Sync/Refresh action pill.

---

## 4. Split-Screen Verification Queue

Both branch staff and manager approval workflows use a side-by-side split container (`grid-cols-12`).

- **Queue Table** (`col-span-12 lg:col-span-8`):
  - Row indicators: Buyer avatar, establishment badge, transaction date, amount.
  - Active selection highlights the row with a soft background (`bg-primary/5`).
- **Detail Inspector Card** (`col-span-12 lg:col-span-4`):
  - Sticky vertical layout holding a cropped image preview, line-item table breakdown, custom reviewer notes, and approve/reject forms.
  - **View Full Audit** Button: Centered secondary button opening the overlays described below.

---

## 5. Overlay Audit Modal (Full Audit Viewer)

A modal layer triggered from the inspector to display non-cropped documents alongside metadata.

- **Layout Grid**: 12-column layout mapping to:
  - **Left Image Panel** (`lg:col-span-8`):
    - Dark gray background with centered receipt photo.
    - Large receipt image uses CSS `object-contain` to show the full receipt without cropping.
    - Fallback section: Displays an warning box if the image link fails, showing an "Open Receipt Image" direct button.
  - **Right Sidebar Details** (`lg:col-span-4`):
    - Vertically scrollable parameters: Buyer email, Establishment, Transaction Date, Large Currency Display, Purpose description, and Reviewer Notes.
    - **Line Items Table**: Lists individual receipt details (Description, Qty, Unit Price, Total Price).
    - **Close Control**: Touch-friendly Escape button.
