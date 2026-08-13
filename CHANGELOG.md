# Changelog

## Unreleased

- Added Reports page audit packet generation for date-range review: manager audit log, all-buyer liquidation summaries, branch allocation details, and treasury cash-out details.
- Updated Reports data model/controller logic to build buyer liquidation reports for all scoped buyers unless a Buyer filter is selected.
- Updated branch allocation reporting to prefer split line-item branch assignments and fall back to the receipt branch for older non-split records.
- Added report regression coverage for all-buyer liquidation, buyer filtering, branch allocation fallback, cash-out details, and report section rendering.
