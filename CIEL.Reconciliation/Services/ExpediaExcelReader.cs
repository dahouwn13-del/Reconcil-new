using ClosedXML.Excel;
using CIEL.Reconciliation.Models;
using System.Globalization;

namespace CIEL.Reconciliation.Services;

public static class ExpediaExcelReader
{
    public static List<ExpediaRecord> Read(string path)
    {
        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheets.First();
        var used = worksheet.RangeUsed() ?? throw new InvalidDataException("The Expedia workbook is empty.");
        var headers = used.FirstRow().Cells().ToDictionary(
            cell => cell.GetString().Trim(),
            cell => cell.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);

        string[] required = { "Guest", "Check-in", "Check-out", "Reservation ID", "Confirmation #", "Status" };
        var missing = required.Where(header => !headers.ContainsKey(header)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"The Expedia workbook is missing required columns: {string.Join(", ", missing)}.");

        var records = new List<ExpediaRecord>();
        foreach (var row in used.RowsUsed().Skip(1))
        {
            var guest = GetText(row, headers, "Guest");
            var reservationId = GetText(row, headers, "Reservation ID");
            if (string.IsNullOrWhiteSpace(guest) && string.IsNullOrWhiteSpace(reservationId))
                continue;

            records.Add(new ExpediaRecord
            {
                GuestName = guest,
                ReservationId = reservationId,
                ExpediaConfirmation = GetText(row, headers, "Confirmation #"),
                Arrival = GetDate(row, headers, "Check-in"),
                Departure = GetDate(row, headers, "Check-out"),
                BookedDate = GetDate(row, headers, "Booked"),
                Room = GetText(row, headers, "Room"),
                PaymentType = GetText(row, headers, "Payment type"),
                BookingAmount = GetDecimal(row, headers, "Booking amount"),
                Status = GetText(row, headers, "Status"),
                NormalizedName = NameTools.Normalize(guest)
            });
        }

        if (records.Count == 0)
            throw new InvalidDataException("No Expedia reservations were found in the workbook.");

        return records;
    }

    private static string GetText(IXLRangeRow row, IReadOnlyDictionary<string, int> headers, string name)
    {
        if (!headers.TryGetValue(name, out var column)) return "";
        return row.Cell(column).GetFormattedString().Trim();
    }

    private static DateTime? GetDate(IXLRangeRow row, IReadOnlyDictionary<string, int> headers, string name)
    {
        if (!headers.TryGetValue(name, out var column)) return null;
        var cell = row.Cell(column);
        if (cell.TryGetValue<DateTime>(out var date)) return date.Date;
        var text = cell.GetFormattedString().Trim();
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            return date.Date;
        return null;
    }

    private static decimal GetDecimal(IXLRangeRow row, IReadOnlyDictionary<string, int> headers, string name)
    {
        if (!headers.TryGetValue(name, out var column)) return 0m;
        var cell = row.Cell(column);
        if (cell.TryGetValue<decimal>(out var amount)) return amount;
        var text = cell.GetFormattedString().Trim();
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out amount) ? amount : 0m;
    }
}
