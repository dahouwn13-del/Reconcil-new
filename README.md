# CIEL Reconciliation v5.3

## Booking.com
Existing Version 5.2 smart matching module remains unchanged.

## Expedia
- Reads Expedia reservation-list XLSX exports.
- Reads Opera Arrivals: Detailed PDFs containing Expedia Lodging Partner, Expedia Strategic Accounts, and Expedia.com reservations.
- Matches by smart guest-name comparison and stay dates.
- Arrival +1 day remains Perfect Match with action: Check if No-Show was charged.
- Cancelled Expedia reservations are excluded.
- Exports payment type, booking amount, reservation ID, Expedia confirmation, Opera confirmation, matching explanation, and action required.


## Version 5.3.2 UI fix
- Expedia results grid now stretches across the full window in maximized mode.
- Horizontal scrolling remains available for all result columns.
- Smart Name Analysis uses a fixed width instead of consuming the remaining grid space.

## Version 5.3.3 shared grid fix
- Applied the full-width results-grid layout to both Booking.com and Expedia.
- Horizontal and vertical scrollbars are enabled in normal and maximized modes.
- Smart Name Analysis uses a fixed width in both modules so it cannot suppress horizontal scrolling.
- Reconciliation and matching logic is unchanged.

## Version 5.3.4
- Rebuilt the results viewport for both Booking.com and Expedia.
- The DataGridView now uses a plain docked host so rounded clipping cannot hide its native horizontal scrollbar.
- Fixed column widths guarantee horizontal navigation for wide reconciliation results.
- The viewport is refreshed after maximize, restore, resize, and DPI layout changes.
- No reconciliation or matching logic was changed.
