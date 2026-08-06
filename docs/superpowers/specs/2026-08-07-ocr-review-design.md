# OCR Upload Review Page Responsive Design Specification

This document outlines the mobile responsiveness and Bento Grid alignment for the OCR Invoice Review page (`AuditCkDayo/Views/Audits/Review.cshtml`). Use this specification to build the layout.

---

## 1. Grid & Columns
- The primary container uses `grid grid-cols-12 gap-space-8 items-start`.
- On desktop, columns split into:
  - Left Receipt Preview: `col-span-12 lg:col-span-5`
  - Right Reconciliation Form: `col-span-12 lg:col-span-7`
- On mobile and tablet viewports, the columns stack vertically.

---

## 2. Receipt Scan Preview & Lightbox
- The receipt preview card remains sticky on desktop.
- The receipt photo is presented in full using `object-contain`, preventing any crop.
- An interactive caption is added below the image: `🔍 Click/Tap image to inspect`.
- Clicking the image opens a full-screen Lightbox overlay (`#lightboxModal`) with a dark translucent backdrop, allowing mobile users to pinch-zoom or view the document in full resolution.

---

## 3. Form Details & Bento Total Amount Card
- **Establishment Selector & Transaction Date**: Wrapped in standard inputs that resize with the viewport.
- **Submitter Info**: User icon + current submitter email displayed in a small metadata card.
- **Bento Claimed Amount Card**:
  - A prominent white container with a 4px solid navy left border.
  - Large currency figures (e.g. `₱ 142.50 PHP`) mapped to the calculated sum.
  - Value updates live in JavaScript as line items are modified.

---

## 4. Extracted Line Items Responsive Table
- The line items list displays as a standard compact table on desktop.
- On mobile/tablet screens, the table elements are transformed using CSS grid/block layout:
  - The table body and rows display as stacked cards.
  - Each item card has a 2-column grid layout for quantity and unit price inputs.
  - Small, muted, uppercase labels are added to each input cell, visible only on mobile viewports.
  - The dynamically added rows (via `addRow()`) generate matching responsive grid structures.

---

## 5. Fixed Actions Footer
- Action buttons are aligned at the bottom of the form.
- Left action: "Discard" (Outline button).
- Right action: "Submit Audit Invoice" (Solid primary button with checkmark icon).
- Layout stacks on very small viewports and spans side-by-side on tablet/desktop.
