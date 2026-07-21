# CIEL Reconciliation

Version 5.0 adds a reusable in-app logging framework for Booking.com reconciliation.

## Work Log
- Opens inside the Booking.com screen.
- Updates live while files are read and reservations are matched.
- Uses INFO, SUCCESS, WARNING and ERROR levels.
- Shows matching progress and percentage.
- Includes reservation number and guest name where available.
- Supports Copy log and Clear log.

The application starts with two choices: Booking.com and Expedia. Expedia remains a separate module for future Expedia-specific file mapping.
