namespace CIEL.Reconciliation.Logging;

public static class Logger
{
    private static readonly object Sync = new();
    private static readonly List<LogEntry> EntriesInternal = new();

    public static event Action<LogEntry>? EntryWritten;

    public static IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (Sync)
                return EntriesInternal.ToList();
        }
    }

    public static void Clear()
    {
        lock (Sync)
            EntriesInternal.Clear();
    }

    public static void Info(string message, string module = "Booking.com", string? reservationNumber = null, string? guestName = null) =>
        Write(LogLevel.Info, message, module, reservationNumber, guestName);

    public static void Success(string message, string module = "Booking.com", string? reservationNumber = null, string? guestName = null) =>
        Write(LogLevel.Success, message, module, reservationNumber, guestName);

    public static void Warning(string message, string module = "Booking.com", string? reservationNumber = null, string? guestName = null) =>
        Write(LogLevel.Warning, message, module, reservationNumber, guestName);

    public static void Error(string message, string module = "Booking.com", string? reservationNumber = null, string? guestName = null) =>
        Write(LogLevel.Error, message, module, reservationNumber, guestName);

    private static void Write(LogLevel level, string message, string module, string? reservationNumber, string? guestName)
    {
        var entry = new LogEntry(DateTime.Now, level, message, module, reservationNumber, guestName);
        lock (Sync)
            EntriesInternal.Add(entry);

        EntryWritten?.Invoke(entry);
    }
}
