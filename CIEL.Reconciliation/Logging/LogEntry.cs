namespace CIEL.Reconciliation.Logging;

public sealed record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string Module = "Booking.com",
    string? ReservationNumber = null,
    string? GuestName = null);
