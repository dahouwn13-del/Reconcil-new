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
