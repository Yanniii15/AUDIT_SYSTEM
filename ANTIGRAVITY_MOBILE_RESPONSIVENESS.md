# Antigravity Mobile Responsiveness Handoff

## Goal
Make the remaining desktop-first pages in `AuditCkDayo` mobile responsive without changing backend logic, routes, authorization, model binding, database behavior, or existing business rules.

The app already has good mobile patterns in parts of the system. Reuse those patterns instead of inventing a second design language.

## Non-negotiables
- Do not change controller logic unless a Razor form cannot bind correctly without a name/id fix.
- Do not change model names, action names, route names, role checks, or enum values.
- Do not remove any table columns from desktop views.
- Do not hide important data on mobile; convert it into card/detail rows.
- Do not rely on horizontal scrolling as the only mobile solution for primary workflows.
- Preserve existing Tailwind CDN setup in `AuditCkDayo/Views/Shared/_Layout.cshtml`.
- Preserve the current visual language: white cards, `bg-primary`, `surface-*` tokens, rounded-xl panels, `font-label-caps`, `font-data-mono`, Material Symbols.
- Keep all forms usable at 360px width.
- Every submit/action button must remain visible and tappable on mobile.

## Existing responsive patterns to copy
Use these files as references:

### Strong mobile card/table pattern
- `AuditCkDayo/Views/Audits/Surrender.cshtml`
- `AuditCkDayo/Views/Audits/SurrenderQueue.cshtml`
- `AuditCkDayo/Views/Audits/VerifyList.cshtml`
- `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml`
- `AuditCkDayo/Views/Account/Register.cshtml`
- `AuditCkDayo/Views/Establishments/Index.cshtml`
- `AuditCkDayo/Views/PcfMonitor/Index.cshtml`

Preferred pattern:

```html
<div class="hidden lg:block max-h-[520px] overflow-y-auto overflow-x-auto">
    <table class="w-full text-left border-collapse">
        <!-- desktop table stays complete -->
    </table>
</div>

<div class="block lg:hidden space-y-space-4 p-space-4 max-h-[520px] overflow-y-auto">
    <!-- mobile cards, one record per card -->
</div>
```

Mobile card style to reuse:

```html
<article class="bg-surface-container-lowest border border-surface-border rounded-xl p-space-4 space-y-space-3 shadow-sm">
    <div class="flex items-start justify-between gap-space-3">
        <div class="min-w-0">
            <div class="font-body-md font-semibold text-on-surface truncate">Primary label</div>
            <div class="text-[10px] font-label-caps uppercase text-on-surface-variant">Secondary label</div>
        </div>
        <span class="font-data-mono text-primary font-bold whitespace-nowrap">₱0.00</span>
    </div>
    <div class="grid grid-cols-2 gap-space-2 text-[11px]">
        <div class="bg-surface-container-low rounded-lg p-space-3 border border-surface-border">
            <div class="font-label-caps uppercase text-[9px] text-on-surface-variant">Field</div>
            <div class="font-data-mono text-on-surface">Value</div>
        </div>
    </div>
</article>
```

### Responsive form pattern
Use these as references:
- `AuditCkDayo/Views/Audits/Review.cshtml`
- `AuditCkDayo/Views/SalesReports/Review.cshtml`
- `AuditCkDayo/Views/Treasury/ReleasePcf.cshtml`
- `AuditCkDayo/Views/Treasury/Settlement.cshtml`

Preferred form classes:

```html
<form class="grid grid-cols-1 md:grid-cols-2 gap-space-4">
```

For actions:

```html
<div class="flex flex-col sm:flex-row gap-space-3 sm:items-center sm:justify-between">
```

Buttons should generally be `w-full sm:w-auto` on mobile-sensitive forms.

## Pages that already have acceptable mobile responsiveness
Do not rewrite these unless a specific bug is visible during QA:

- `AuditCkDayo/Views/Shared/_Layout.cshtml` — sidebar already uses mobile open/close behavior.
- `AuditCkDayo/Views/Audits/Review.cshtml` — mobile form/table-card pattern exists.
- `AuditCkDayo/Views/Audits/Surrender.cshtml` — mobile cards exist.
- `AuditCkDayo/Views/Audits/SurrenderQueue.cshtml` — mobile queue cards exist.
- `AuditCkDayo/Views/Audits/VerifyList.cshtml` — mobile queue cards and modal actions exist.
- `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml` — mobile queue cards and modal actions exist.
- `AuditCkDayo/Views/Account/Register.cshtml` — mobile cards exist.
- `AuditCkDayo/Views/Account/RegisterForm.cshtml` — responsive form grid exists.
- `AuditCkDayo/Views/Establishments/Index.cshtml` — mobile cards exist.
- `AuditCkDayo/Views/PcfMonitor/Index.cshtml` — mobile cards exist.
- `AuditCkDayo/Views/Notifications/Index.cshtml` — simple stacked layout already responsive.

## Priority pages to make mobile responsive

### Priority 1: Reports page
File:
- `AuditCkDayo/Views/Reports/Index.cshtml`

Problem:
- This is the largest desktop-first page.
- Many sections still use inline `style="max-height: 440px; overflow-y: auto; overflow-x: auto;"` and full tables.
- Mobile users should not need to sideways-scroll through every report section.

Required work:
1. Keep desktop tables intact under `hidden lg:block`.
2. Add `block lg:hidden` card layouts for each report section that displays tabular records.
3. Preserve all filters and export/action links.
4. Summary KPI grid should become `grid-cols-1 sm:grid-cols-2 lg:grid-cols-6` instead of squeezing two cards on very narrow screens if any card text wraps badly.
5. Report viewer modal should fit 360px width:
   - modal padding: `p-space-3 sm:p-space-4 lg:p-space-6`
   - section layout: `grid-cols-1 lg:grid-cols-[...]`
   - image/details should not exceed viewport height.
6. Replace inline scroll styles where practical with Tailwind classes. If inline style stays, it must not be the only mobile UX.

Mobile card sections needed for at least:
- P&L detail rows.
- Branch sales rows.
- Manager Audit / Receipt Audit Log rows.
- Buyer Liquidation rows.
- Branch Audit / Expense Allocation rows.
- Daily Cash Flow / Cash Out rows.
- Treasury detail tables inside tabs/sections.

Acceptance:
- At 390px width, Reports page has no body-level horizontal scroll.
- Filters stack cleanly with full-width inputs/buttons.
- Every report row is readable as a card.
- Receipt/audit viewer modal can be opened, read, and closed on mobile.

### Priority 2: Sales Reports index
File:
- `AuditCkDayo/Views/SalesReports/Index.cshtml`

Problem:
- Pending sales verification uses only a desktop table inside `overflow-x-auto`.

Required work:
1. Wrap the existing table in `hidden lg:block`.
2. Add `block lg:hidden` cards for pending reports.
3. Each mobile card must show:
   - branch
   - business date
   - handover date
   - cashier
   - cash to handover
   - status
   - `Confirm to Treasury` action
4. Action button must be full width on mobile.

Acceptance:
- Manager can identify and open a pending sales report at 360px width without horizontal scrolling.

### Priority 3: Treasury cash flow
File:
- `AuditCkDayo/Views/Treasury/Index.cshtml`

Current state:
- Many parts are already stacked/gridded, but forms and daily cash sections need mobile polish.

Required work:
1. Header action buttons should be `w-full sm:w-auto` on mobile.
2. Date filter button should be full width below input on mobile.
3. Cash-in and cash-out forms should use full-width buttons on mobile.
4. Cash entry rows should wrap safely: names/notes `min-w-0 truncate`, amounts `whitespace-nowrap`.
5. Avoid cramped two-column rows at 360px; use `grid-cols-1 sm:grid-cols-2` where needed.

Acceptance:
- Treasury daily cash flow can record cash in/out on 360px width without clipped labels or hidden buttons.

### Priority 4: Coverage management
File:
- `AuditCkDayo/Views/Coverage/Index.cshtml`

Problem:
- Coverage history table uses horizontal scroll only.

Required work:
1. Keep desktop table as `hidden lg:block`.
2. Add `block lg:hidden` coverage cards.
3. Each card must show:
   - covered manager
   - covering manager
   - start date
   - end date
   - active/expired status
   - delete/end action if present in desktop table

Acceptance:
- Admin can review and act on coverage records at 360px width without horizontal scroll.

### Priority 5: Treasury release and settlement forms
Files:
- `AuditCkDayo/Views/Treasury/ReleasePcf.cshtml`
- `AuditCkDayo/Views/Treasury/Settlement.cshtml`

Current state:
- Forms are mostly responsive but need final mobile polish.

Required work:
1. Main form cards should use `w-full max-w-*` safely.
2. Long select fields must not overflow; add `min-w-0` to grid children where needed.
3. Bottom action areas should become stacked on mobile:
   - back link
   - submit button full width
4. Settlement calculated result cards should stack on 360px.

Acceptance:
- User can complete release and settlement forms on 360px width with all fields visible.

### Priority 6: P&L registration
File:
- `AuditCkDayo/Views/PnlRegistration/Index.cshtml`

Current state:
- Add-category forms already use `flex-col sm:flex-row`.

Required work:
1. Check category lists/cards on mobile.
2. Ensure create inputs and buttons are full-width at 360px.
3. If category tables/lists overflow, convert them to cards or stacked rows.

Acceptance:
- Admin can add and view categories at 360px width.

### Priority 7: System diagnostics
File:
- `AuditCkDayo/Views/System/Diagnostics.cshtml`

Required work:
1. Ensure diagnostic panels stack vertically on mobile.
2. Long technical strings should wrap with `break-words` or `overflow-x-auto` only for code-like blocks.
3. Buttons/links full width on mobile if they appear in rows.

Acceptance:
- Diagnostics page does not force body-level horizontal scroll at 360px.

## Implementation strategy

### Step 1: Audit viewport issues first
Run the app and check these widths:
- 360px wide — small Android baseline.
- 390px wide — common iPhone width.
- 768px wide — tablet portrait.
- 1024px wide — tablet landscape / desktop transition.

Look specifically for:
- body-level horizontal scrolling
- clipped action buttons
- tables that require sideways scrolling for primary data
- modals taller/wider than viewport
- sticky panels that stay sticky on mobile when they should stack
- tiny tap targets under 44px height

### Step 2: Convert desktop tables to table + mobile-card pairs
For each page with a desktop table:

```html
<div class="hidden lg:block max-h-[520px] overflow-y-auto overflow-x-auto">
    <!-- existing table -->
</div>

<div class="block lg:hidden space-y-space-4 p-space-4 max-h-[520px] overflow-y-auto">
    @if (!Model.Any())
    {
        <div class="text-center text-on-surface-variant font-body-md py-space-6">No records found.</div>
    }
    else
    {
        @foreach (var item in Model)
        {
            <article class="bg-surface-container-lowest border border-surface-border rounded-xl p-space-4 space-y-space-3 shadow-sm">
                <!-- same data as table, stacked -->
            </article>
        }
    }
</div>
```

Rules:
- Desktop table must remain unchanged unless styling is broken.
- Mobile cards must include all critical fields and actions.
- Use `truncate` only for secondary labels; financial amounts and dates must remain readable.
- Use `break-words` for notes/descriptions.

### Step 3: Fix forms
Rules:
- Use `grid grid-cols-1 md:grid-cols-2` for most forms.
- Use `grid-cols-1 sm:grid-cols-2` only where fields are short.
- Use `w-full` on inputs/selects/textareas/buttons.
- Use `flex flex-col sm:flex-row` for action rows.
- Add `min-w-0` to flex/grid children that contain long text.

### Step 4: Fix modals
Rules:
- Outer modal padding must shrink on mobile.
- Use `max-h-[92vh] overflow-y-auto` for content panels.
- Put action buttons inside modal on mobile if desktop action panel is outside viewport.
- Close buttons must be visible at top right.

## QA checklist

For each changed page, verify at 360px, 390px, 768px, and desktop:

- [ ] No body-level horizontal scroll.
- [ ] Navigation opens/closes correctly.
- [ ] Header text wraps without overlapping buttons.
- [ ] Forms can be completed without hidden fields.
- [ ] Submit/action buttons are visible, full-width where needed, and tappable.
- [ ] Tables are readable as cards on mobile.
- [ ] Desktop tables still appear at `lg` and above.
- [ ] Empty states still render.
- [ ] Validation messages still render near the relevant field.
- [ ] Modals open, scroll, and close on mobile.
- [ ] Existing role-based actions remain visible only to the intended role.

## Suggested manual role checks
Use seeded/demo users or existing local accounts for each role:

### Manager / Owner
Check:
- Dashboard
- Audit Approvals
- Cash Surrender Requests
- PCF Monitor
- Reports
- Treasury
- Sales Reports

### BranchStaff
Check:
- Dashboard
- Verify Deliveries
- Daily Sales upload/review
- Cash Surrender

### Buyer
Check:
- New Audit upload/review/batch
- Cash Surrender
- Dashboard

### Admin
Check:
- Users Directory
- Establishments
- Coverage
- P&L Registration
- System Settings

## Test/build commands
Run after Razor edits:

```bash
dotnet build AuditCkDayo/AuditCkDayo.csproj
```

If tests are available and not blocked by live API limits:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj
```

If the full suite hits the live Gemini API test or rate limits, at minimum run a build and manually verify the changed pages in browser viewport emulation.

## Deliverable expected from Antigravity
- Updated `.cshtml` files only unless a tiny shared CSS/helper change is clearly justified.
- A short summary listing each changed page and what was made responsive.
- Screenshots or viewport notes for 360px/390px and desktop.
- Confirmation that no backend behavior was changed.
