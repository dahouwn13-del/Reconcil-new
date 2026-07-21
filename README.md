# CIEL Reconciliation v4.0

Windows desktop application with a platform selection screen for:

- Booking.com reconciliation
- Expedia reconciliation

## Booking.com
The existing Booking.com and Opera PMS reconciliation remains fully functional, including smart matching, split-stay detection, Excel export, filters, and multilingual name normalization.

## Expedia
A separate Expedia interface is included because Expedia reports use a different structure. The Expedia parser will be activated after mapping a real Expedia report against its corresponding Opera Arrivals: Detailed PDF.

## Build
The GitHub Actions workflow publishes a self-contained Windows x64 executable and uploads it as the `CIEL-Reconciliation-Windows` artifact.


## Version 4.1 - In-app Work Log
- Added a SHOW WORK LOG / HIDE WORK LOG button inside the Booking.com screen.
- The live log displays file-reading stages, each booking being matched, warnings, review items, errors, and a final summary.
- Added Copy Log and Clear Log controls.
- Preserved the previous duplicate-variable compile fix.
