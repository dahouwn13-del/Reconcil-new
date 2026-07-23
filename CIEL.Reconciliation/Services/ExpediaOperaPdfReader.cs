using System.Globalization;
using System.Text.RegularExpressions;
using CIEL.Reconciliation.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CIEL.Reconciliation.Services;

public static class ExpediaOperaPdfReader
{
    private static readonly Regex DateRegex = new(@"^\d{2}-\d{2}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex ConfirmationRegex = new(@"^\d{4,10}$", RegexOptions.Compiled);

    public static List<OperaRecord> Read(string path)
    {
        var records = new List<OperaRecord>();
        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
        {
            var lines = BuildLines(page.GetWords());
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var hasExpedia = line.Words.Any(w => w.Text.Contains("Expedia", StringComparison.OrdinalIgnoreCase));
                if (!hasExpedia) continue;

                var dates = line.Words.Where(w => DateRegex.IsMatch(w.Text)).OrderBy(w => w.Left).Select(w => w.Text).ToList();
                if (dates.Count < 2) continue;

                var guest = JoinWords(line.Words.Where(w => w.Left >= 34 && w.Left < 178));
                if (string.IsNullOrWhiteSpace(guest)) continue;

                records.Add(new OperaRecord
                {
                    OperaConf = FindConfirmation(lines, index),
                    GuestName = guest,
                    Arrival = ParseOperaDate(dates[0]),
                    Departure = ParseOperaDate(dates[1]),
                    Status = JoinWords(line.Words.Where(w => w.Left >= 530 && w.Left < 580)),
                    RoomNumber = JoinWords(line.Words.Where(w => w.Left < 31 && IsDigits(w.Text))),
                    NormalizedName = NameTools.Normalize(guest)
                });
            }
        }

        if (records.Count == 0)
            throw new InvalidDataException("No Expedia reservations were detected in the Opera PDF. Select the Opera Arrivals: Detailed report filtered for Expedia travel agents.");
        return records;
    }

    private static List<PdfLine> BuildLines(IEnumerable<Word> words)
    {
        const double tolerance = 2.0;
        var result = new List<PdfLine>();
        foreach (var word in words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
        {
            var y = word.BoundingBox.Bottom;
            PdfLine? target = null;
            for (var i = result.Count - 1; i >= 0 && i >= result.Count - 4; i--)
            {
                if (Math.Abs(result[i].Y - y) <= tolerance) { target = result[i]; break; }
            }
            if (target == null) { target = new PdfLine(y); result.Add(target); }
            target.Words.Add(new PdfWord(word.Text.Trim(), word.BoundingBox.Left));
        }
        foreach (var line in result) line.Words.Sort((a, b) => a.Left.CompareTo(b.Left));
        return result.OrderByDescending(l => l.Y).ToList();
    }

    private static string FindConfirmation(IReadOnlyList<PdfLine> lines, int mainLineIndex)
    {
        var mainY = lines[mainLineIndex].Y;
        for (var i = mainLineIndex + 1; i < lines.Count && i <= mainLineIndex + 3; i++)
        {
            if (mainY - lines[i].Y > 27) break;
            var candidate = lines[i].Words.FirstOrDefault(w => w.Left >= 30 && w.Left < 80 && ConfirmationRegex.IsMatch(w.Text));
            if (candidate != null) return candidate.Text;
        }
        return "";
    }

    private static string JoinWords(IEnumerable<PdfWord> words) => string.Join(" ", words.OrderBy(w => w.Left).Select(w => w.Text)).Trim();
    private static bool IsDigits(string value) => value.All(char.IsDigit);
    private static DateTime? ParseOperaDate(string value) => DateTime.TryParseExact(value, "dd-MM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt.Date : null;
    private sealed class PdfLine { public PdfLine(double y) { Y = y; } public double Y { get; } public List<PdfWord> Words { get; } = new(); }
    private sealed record PdfWord(string Text, double Left);
}
