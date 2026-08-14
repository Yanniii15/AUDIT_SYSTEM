# Antigravity Owner Phone Voice Accessibility Handoff

## Goal
Make the Owner side usable for a blind owner who primarily uses a phone. Prioritize native mobile screen-reader support first, then add an optional in-app Voice Assist helper.

Target devices:
- iPhone Safari with VoiceOver
- Android Chrome with TalkBack
- Mobile widths around `360px`, `390px`, `414px`, and `430px`

This is not a desktop-first accessibility pass. Treat this as phone-first owner accessibility.

## Product decision
Build two layers:

1. **Native phone accessibility foundation**
   - Proper semantic HTML, labels, focus order, ARIA, live regions, modal behavior, readable mobile cards.
   - This must work with VoiceOver/TalkBack even if custom JavaScript voice fails.

2. **Optional in-app Voice Assist**
   - Owner-only toggle.
   - Uses browser `speechSynthesis`.
   - Reads concise page summaries, selected records, validation errors, and action results.
   - Off by default.
   - Never replaces screen-reader accessibility.

## Non-negotiables
- Do not change backend business logic, authorization rules, route names, action names, model names, enum names, or database behavior.
- Do not require desktop keyboard usage as the primary owner workflow.
- Do not rely on hover interactions for owner-critical actions.
- Do not auto-enable voice. The owner must explicitly tap the Voice Assist control.
- Do not speak passwords, hidden security tokens, raw OCR JSON, or internal IDs unless the ID is already meaningful to the user.
- Do not read every table row automatically. Speak summaries first; speak selected records on demand.
- Do not remove visible labels. Voice support must be additive.
- Do not hide important owner data on mobile. Convert table rows into labeled mobile cards.
- Every owner action button must have a readable accessible name and at least a 44px tap target.

## Owner-critical pages
Focus first on these files:

1. `AuditCkDayo/Views/Shared/_Layout.cshtml`
   - Mobile header/sidebar
   - Owner navigation
   - Voice Assist toggle
   - Skip-to-content link
   - Global live region

2. `AuditCkDayo/Views/Home/Index.cshtml`
   - Owner dashboard
   - Pending audits
   - Pending sales reports
   - Historical filters
   - Cash visibility
   - Dashboard modal

3. `AuditCkDayo/Views/Audits/VerifyList.cshtml`
   - Owner audit approval queue
   - Audit inspection modal
   - Approve/reject actions

4. `AuditCkDayo/Views/Audits/SurrenderQueue.cshtml`
   - Owner cash surrender queue
   - Confirm/reject cash requests

5. `AuditCkDayo/Views/Reports/Index.cshtml`
   - Owner reports
   - Report summaries
   - Audit/liquidation/cash-flow report rows
   - Report viewer modal

6. `AuditCkDayo/Views/Treasury/Index.cshtml`
   - Owner treasury cash flow
   - Cash-in/cash-out forms
   - Daily cash summary

7. `AuditCkDayo/Views/SalesReports/Index.cshtml`
   - Owner/manager daily sales pending confirmations

8. `AuditCkDayo/Views/PcfMonitor/Index.cshtml`
   - Owner PCF visibility

9. `AuditCkDayo/Views/Notifications/Index.cshtml`
   - Owner notification center

## Existing patterns to reuse
The system already has mobile card patterns. Reuse these:

- `AuditCkDayo/Views/Audits/Surrender.cshtml`
- `AuditCkDayo/Views/Audits/SurrenderQueue.cshtml`
- `AuditCkDayo/Views/Audits/VerifyList.cshtml`
- `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml`
- `AuditCkDayo/Views/Account/Register.cshtml`
- `AuditCkDayo/Views/Establishments/Index.cshtml`
- `AuditCkDayo/Views/PcfMonitor/Index.cshtml`

Preferred desktop-table + mobile-card structure:

```html
<div class="hidden lg:block max-h-[520px] overflow-y-auto overflow-x-auto">
    <table class="w-full text-left border-collapse">
        <!-- Keep complete desktop table -->
    </table>
</div>

<div class="block lg:hidden space-y-space-4 p-space-4 max-h-[520px] overflow-y-auto">
    <!-- One accessible mobile card per record -->
</div>
```

Preferred mobile card structure:

```html
<article class="bg-surface-container-lowest border border-surface-border rounded-xl p-space-4 space-y-space-3 shadow-sm"
         aria-label="Surrender request from Beth Buyer for ₱500, pending">
    <div class="flex items-start justify-between gap-space-3">
        <div class="min-w-0">
            <div class="font-body-md font-semibold text-on-surface truncate">Beth Buyer</div>
            <div class="text-[10px] font-label-caps uppercase text-on-surface-variant">Buyer</div>
        </div>
        <span class="font-data-mono text-primary font-bold whitespace-nowrap">₱500.00</span>
    </div>

    <dl class="grid grid-cols-1 sm:grid-cols-2 gap-space-2 text-[11px]">
        <div class="bg-surface-container-low rounded-lg p-space-3 border border-surface-border">
            <dt class="font-label-caps uppercase text-[9px] text-on-surface-variant">Status</dt>
            <dd class="font-data-mono text-on-surface">Pending</dd>
        </div>
    </dl>

    <a class="w-full inline-flex min-h-[44px] items-center justify-center rounded-lg bg-primary text-on-primary font-label-caps uppercase"
       aria-label="Inspect surrender request from Beth Buyer for ₱500">
        Inspect Request
    </a>
</article>
```

Use `<dl>`, `<dt>`, and `<dd>` for label/value data on mobile cards where practical.

## Layer 1: native phone screen-reader accessibility

### Layout requirements
File: `AuditCkDayo/Views/Shared/_Layout.cshtml`

Add or verify:

1. Skip link at the top of `<body>`:

```html
<a href="#mainContent" class="sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-[999] focus:bg-primary focus:text-on-primary focus:px-space-4 focus:py-space-3 focus:rounded-lg">
    Skip to main content
</a>
```

2. Main content target:

```html
<main id="mainContent" class="relative pt-16 min-h-screen bg-surface-container-low" tabindex="-1">
```

3. Global screen-reader live region:

```html
<div id="globalA11yStatus" class="sr-only" role="status" aria-live="polite" aria-atomic="true"></div>
```

4. Active owner navigation should include `aria-current="page"` on the current page link.

5. Icon-only buttons must have labels:
- Sidebar menu: `aria-label="Open navigation menu"`
- Sidebar close: `aria-label="Close navigation menu"`
- Notification bell: `aria-label="Open notifications"`
- Logout: `aria-label="Log out"`
- Modal close buttons: already partly present; verify all owner modals.

6. When mobile sidebar opens:
- announce “Navigation menu opened” through `globalA11yStatus`
- move focus to the close button
- when closed, return focus to the menu button

### Forms
For owner-side forms:
- Every input/select/textarea needs a real `<label>` or `aria-label`.
- Validation summaries need `role="alert"` or `aria-live="assertive"`.
- Field validation spans should be associated with fields using `aria-describedby` where practical.
- On failed submit, focus should go to the validation summary or first invalid field.

### Modals
Owner-critical modals:
- `Home/Index.cshtml` dashboard audit viewer
- `Audits/VerifyList.cshtml` audit viewer
- `Reports/Index.cshtml` report/audit viewer

Required modal behavior:
- `role="dialog"`
- `aria-modal="true"`
- `aria-labelledby="...Title"`
- focus moves to modal title or close button when opened
- Escape closes the modal
- focus returns to the original opener after close
- background content should not be read as the active context if possible

### Mobile cards
For owner-critical rows:
- Add useful `aria-label` to clickable cards.
- Use real buttons/links for actions, not clickable `<tr>` only.
- If an entire card is clickable, also include a visible button inside the card.
- Do not make important actions depend on clicking a non-semantic div.

## Layer 2: optional in-app Voice Assist

### Files
Create:
- `AuditCkDayo/wwwroot/js/voice-assist.js`

Modify:
- `AuditCkDayo/Views/Shared/_Layout.cshtml`
- owner-critical views listed above

### Voice Assist control
Add an owner-visible control in layout.

Desktop location:
- top header near notifications.

Mobile location:
- inside mobile sidebar footer or under the mobile header actions.

Button text:

```text
Voice Assist Off
Voice Assist On
Stop Voice
Replay Summary
```

Recommended markup:

```html
@if (User.IsInRole("Owner"))
{
    <button type="button"
            id="voiceAssistToggle"
            class="inline-flex min-h-[44px] items-center justify-center gap-space-2 rounded-lg border border-surface-border bg-surface-container px-space-4 py-space-2 text-[11px] font-label-caps uppercase text-primary"
            aria-pressed="false"
            aria-label="Turn voice assist on">
        <span class="material-symbols-outlined text-[18px]" aria-hidden="true">record_voice_over</span>
        <span id="voiceAssistToggleText">Voice Assist Off</span>
    </button>
}
```

Also provide a stop/replay option after voice is enabled. It can be the same button behavior or a second small button.

### Browser speech constraints
Mobile browsers usually block speech until a user gesture happens.

Therefore:
- Do not expect auto-speech before the owner taps the button.
- On first tap, enable Voice Assist and read the current page summary.
- On later page loads, attempt to read if browser allows it; fail silently if blocked.
- Always let the owner tap “Replay Summary”.

### Storage
Persist preference on the phone:

```js
localStorage.setItem("auditckdayo.voiceAssist", "on");
localStorage.setItem("auditckdayo.voiceAssist", "off");
```

### Voice helper rules
`voice-assist.js` should:
- detect support with `"speechSynthesis" in window`
- use `window.speechSynthesis.cancel()` before speaking new text
- keep messages concise
- ignore empty summaries
- never read password fields
- never read anti-forgery tokens
- expose small helpers on `window.auditVoiceAssist`, for example:

```js
window.auditVoiceAssist = {
    speak(text),
    stop(),
    readPageSummary(),
    announce(text)
};
```

### Page summary source
Each owner-critical page should expose a concise summary.

Preferred markup:

```html
<div id="pageVoiceSummary" class="sr-only">
    Dashboard. Pending audit approvals: 4. Pending cash surrender requests: 2. Current monitored PCF: ₱12,500.
</div>
```

Alternative:

```html
<section data-voice-summary="Reports. Approved audit amount ₱8,400. Pending surrender amount ₱500. Current cash balance ₱12,500.">
```

Use the hidden element for complex Razor-generated summaries. It is easier to inspect and also helps screen readers.

## Page-specific voice summary requirements

### Home dashboard
File: `AuditCkDayo/Views/Home/Index.cshtml`

Summary should include:
- page name
- current user role
- pending audit approvals count
- pending cash surrender requests count
- pending daily sales reports count if visible
- current monitored PCF/cash total if visible

Example:

```text
Dashboard. Owner view. Pending audit approvals: 4. Pending cash surrender requests: 2. Pending sales reports: 1. Current monitored PCF: ₱12,500.
```

### Audit approvals
File: `AuditCkDayo/Views/Audits/VerifyList.cshtml`

Summary should include:
- number of pending audits
- first pending audit buyer, branch, amount, status

Example:

```text
Audit approvals. 6 audits pending. First audit: Beth Buyer, CKR Main, ₱500, awaiting manager approval.
```

When owner opens an audit modal, speak:

```text
Audit viewer opened. Buyer Beth Buyer. Branch CKR Main. Amount ₱500. Status awaiting manager approval.
```

### Cash surrender requests
File: `AuditCkDayo/Views/Audits/SurrenderQueue.cshtml`

Summary should include:
- pending request count
- first request buyer and amount

Example:

```text
Cash surrender approvals. 3 pending. First request: Charlie Buyer, ₱500.
```

When selecting a request, speak:

```text
Selected surrender request. Charlie Buyer. Declared amount ₱500. Requested August 13.
```

### Reports
File: `AuditCkDayo/Views/Reports/Index.cshtml`

Summary should include:
- selected date range if visible
- current cash balance
- total audit amount
- approved audit amount
- pending surrender amount
- confirmed surrender amount

Example:

```text
Reports. Current cash balance ₱12,500. Approved audit amount ₱8,400. Pending surrender amount ₱500. Confirmed surrender amount ₱2,000.
```

### Treasury
File: `AuditCkDayo/Views/Treasury/Index.cshtml`

Summary should include:
- selected cash flow date
- starting balance
- total cash in
- total cash out
- closing balance

Example:

```text
Treasury cash flow for August 13. Starting balance ₱10,000. Cash in ₱5,000. Cash out ₱2,000. Closing balance ₱13,000.
```

### Sales Reports
File: `AuditCkDayo/Views/SalesReports/Index.cshtml`

Summary should include:
- pending sales report count
- first pending branch/date/cash amount

Example:

```text
Pending sales verification. 2 reports pending. First report: CKR Main, August 13, cash to handover ₱4,500.
```

### PCF Monitor
File: `AuditCkDayo/Views/PcfMonitor/Index.cshtml`

Summary should include:
- scope label
- total starting PCF
- total current PCF
- total used PCF

Example:

```text
PCF Monitor. Owner scope. Starting PCF ₱20,000. Current PCF ₱12,500. Used PCF ₱7,500.
```

### Notifications
File: `AuditCkDayo/Views/Notifications/Index.cshtml`

Summary should include:
- unread notification count
- newest notification title and message if present

Example:

```text
Notifications. 3 unread. Newest: Surrender request pending. Beth Buyer submitted ₱500 for confirmation.
```

## Mobile interaction requirements

### Tap targets
All owner-critical controls should be at least 44px tall:

```html
class="min-h-[44px]"
```

Apply to:
- approve/reject buttons
- confirm/reject surrender buttons
- inspect/open buttons
- filter/apply buttons
- export/download buttons
- Voice Assist controls
- notification controls

### No hover-only behavior
If desktop uses hover to reveal actions, mobile must show those actions directly.

### Clickable rows
Clickable `<tr>` or `<article onclick="...">` is not enough. Add a real button/link inside each mobile card:

```html
<button type="button" class="w-full min-h-[44px] ...">
    Inspect Request
</button>
```

### Focus and announcements
When a user performs an action:
- success message should use `role="status"`
- errors should use `role="alert"`
- Voice Assist should speak the message if enabled

## Suggested `voice-assist.js` behavior

Implement small, boring JavaScript. No dependencies.

Pseudo-structure:

```js
(function () {
    const storageKey = "auditckdayo.voiceAssist";
    const toggle = document.getElementById("voiceAssistToggle");
    const toggleText = document.getElementById("voiceAssistToggleText");
    const summary = document.getElementById("pageVoiceSummary");
    const status = document.getElementById("globalA11yStatus");

    function isSupported() {
        return "speechSynthesis" in window && "SpeechSynthesisUtterance" in window;
    }

    function isEnabled() {
        return localStorage.getItem(storageKey) === "on";
    }

    function setEnabled(enabled) {
        localStorage.setItem(storageKey, enabled ? "on" : "off");
        updateButton();
    }

    function speak(text) {
        if (!isSupported() || !isEnabled() || !text || !text.trim()) return;
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text.trim());
        utterance.rate = 0.95;
        utterance.pitch = 1;
        window.speechSynthesis.speak(utterance);
    }

    function stop() {
        if (isSupported()) window.speechSynthesis.cancel();
    }

    function readPageSummary() {
        speak(summary?.textContent || document.title);
    }

    function announce(text) {
        if (status) status.textContent = text;
        speak(text);
    }

    function updateButton() {
        if (!toggle || !toggleText) return;
        const enabled = isEnabled();
        toggle.setAttribute("aria-pressed", enabled ? "true" : "false");
        toggle.setAttribute("aria-label", enabled ? "Turn voice assist off" : "Turn voice assist on");
        toggleText.textContent = enabled ? "Voice Assist On" : "Voice Assist Off";
    }

    toggle?.addEventListener("click", function () {
        const next = !isEnabled();
        setEnabled(next);
        if (next) readPageSummary();
        else stop();
    });

    window.auditVoiceAssist = { speak, stop, readPageSummary, announce };
    updateButton();
})();
```

Add final code carefully; this pseudo-code is the intended shape, not a command to paste blindly if IDs differ.

## QA checklist for Antigravity

Test on mobile viewport widths:
- `360px`
- `390px`
- `414px`
- `430px`

Test with browser/device accessibility where possible:
- iPhone VoiceOver if available
- Android TalkBack if available
- Chrome DevTools accessibility tree if physical devices are unavailable

For each owner-critical page:

- [ ] Page has one clear `<h1>`.
- [ ] Owner can reach main content quickly.
- [ ] Sidebar opens/closes on phone and announces state.
- [ ] Voice Assist button is visible to Owner on phone.
- [ ] Voice Assist is hidden from non-owner roles or harmless if not rendered.
- [ ] Tapping Voice Assist reads the current page summary.
- [ ] Tapping Voice Assist off stops speech.
- [ ] Buttons have accessible names.
- [ ] Icon-only buttons have `aria-label`.
- [ ] Mobile cards have labeled fields.
- [ ] No owner-critical action is hover-only.
- [ ] Validation errors are announced.
- [ ] Success messages are announced.
- [ ] Modals announce title/content when opened.
- [ ] Modal close returns to the opener.
- [ ] No body-level horizontal scroll at 360px.
- [ ] Forms remain usable with screen reader swipe navigation.

## Build verification
Run after Razor/JS changes:

```bash
dotnet build AuditCkDayo/AuditCkDayo.csproj
```

If tests are run and the full suite hits the live Gemini API/rate limit, report that separately. Do not treat a live API rate limit as a mobile accessibility failure.

## Expected Antigravity deliverable
- Updated Razor views and JS/CSS files required for phone-first owner accessibility.
- Short summary of changed files.
- Notes for each owner-critical page at 360px/390px.
- Confirmation that backend logic was not changed.
- Confirmation that Voice Assist is owner-only and off by default.
