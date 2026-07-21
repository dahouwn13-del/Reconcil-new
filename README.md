# CIEL Reconciliation Suite v3.1

Native Windows desktop application for reconciling Booking.com Excel exports with Opera Arrivals: Detailed PDFs.

## Version 3.1 improvements

- Smarter matching using exact dates plus fuzzy guest-name scoring
- Split reservation detection for contiguous Opera stays
- Duplicate Booking.com reservation-number detection
- Separate name-mismatch classification
- Opera room number included in the results and Excel export
- Clickable result cards and search remain available

## Build

The GitHub Actions workflow publishes a self-contained Windows executable artifact.


## Version 3.1 name transliteration

Booking.com guest names written in Arabic/Persian or Cyrillic are transliterated internally into Latin characters before fuzzy matching against Opera. The original guest name remains unchanged in the screen and Excel export. Transliteration improves matching but should still be manually reviewed when spelling differs significantly.
