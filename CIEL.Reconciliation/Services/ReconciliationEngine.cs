using CIEL.Reconciliation.Models;
using FuzzySharp;

namespace CIEL.Reconciliation.Services;

public static class ReconciliationEngine
{
    private static readonly HashSet<string> ActiveStatuses = new(StringComparer.OrdinalIgnoreCase) { "ok", "no_show" };

    public static List<ResultRecord> Run(IReadOnlyList<BookingRecord> bookings, IReadOnlyList<OperaRecord> opera)
    {
        var active = bookings.Where(b => ActiveStatuses.Contains(b.Status)).ToList();
        var excluded = bookings.Where(b => !ActiveStatuses.Contains(b.Status)).ToList();
        var remaining = new HashSet<int>(Enumerable.Range(0, opera.Count));
        var output = new List<ResultRecord>();

        var duplicateBookingNumbers = active
            .Where(b => !string.IsNullOrWhiteSpace(b.BookingNumber))
            .GroupBy(b => b.BookingNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var b in active)
        {
            if (duplicateBookingNumbers.Contains(b.BookingNumber))
            {
                var duplicate = NewBookingResult(b);
                duplicate.Result = "Duplicate Booking";
                duplicate.Reason = "The same Booking.com reservation number appears more than once in the source file.";
                duplicate.MatchMethod = "Duplicate check";
                output.Add(duplicate);
                continue;
            }

            var split = FindSplitReservation(b, opera, remaining);
            if (split.Count > 1)
            {
                foreach (var idx in split) remaining.Remove(idx);
                var records = split.Select(i => opera[i]).OrderBy(o => o.Arrival).ToList();
                output.Add(new ResultRecord
                {
                    BookingNumber = b.BookingNumber,
                    BookingGuest = b.GuestName,
                    BookingArrival = b.Arrival,
                    BookingDeparture = b.Departure,
                    BookingStatus = b.Status,
                    OperaConf = string.Join(" / ", records.Select(o => o.OperaConf).Where(x => !string.IsNullOrWhiteSpace(x))),
                    OperaGuest = string.Join(" / ", records.Select(o => o.GuestName).Distinct()),
                    OperaArrival = records.Min(o => o.Arrival),
                    OperaDeparture = records.Max(o => o.Departure),
                    OperaStatus = string.Join(" / ", records.Select(o => o.Status).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                    OperaRoom = string.Join(" / ", records.Select(o => o.RoomNumber).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                    MatchScore = records.Max(o => Fuzz.TokenSortRatio(b.NormalizedName, o.NormalizedName)),
                    MatchMethod = "Split stay",
                    Result = "Split Reservation",
                    Reason = $"The Booking.com stay matches {records.Count} contiguous Opera reservations."
                });
                continue;
            }

            int? chosen = null;
            var bestScore = 0;
            var bestDistance = int.MaxValue;
            var method = "";

            foreach (var i in remaining)
            {
                var o = opera[i];
                var score = NameScore(b, o);
                var distance = DateDistance(b, o);
                var datesExact = b.Arrival == o.Arrival && b.Departure == o.Departure;

                // Exact dates are a strong signal, but still require a credible name match.
                if (datesExact && score >= 60)
                {
                    if (chosen is null || score > bestScore)
                    {
                        chosen = i;
                        bestScore = score;
                        bestDistance = 0;
                        method = score >= 90 ? "Exact stay and guest" : "Exact stay, similar guest";
                    }
                    continue;
                }

                // For non-exact dates, avoid matching unrelated guests solely by fuzzy name.
                if (score >= 78 && distance <= 4 &&
                    (chosen is null || score > bestScore || (score == bestScore && distance < bestDistance)))
                {
                    chosen = i;
                    bestScore = score;
                    bestDistance = distance;
                    method = b.NormalizedName == o.NormalizedName ? "Exact guest name" : "Similar guest name";
                }
            }

            var rr = NewBookingResult(b);
            rr.MatchScore = bestScore;
            rr.MatchMethod = method;
            if (chosen is int idx)
            {
                var o = opera[idx];
                remaining.Remove(idx);
                CopyOpera(rr, o);
                var arrOk = b.Arrival == o.Arrival;
                var depOk = b.Departure == o.Departure;

                if (arrOk && depOk && bestScore >= 88)
                {
                    rr.Result = "Perfect Match";
                    rr.Reason = "Guest name and stay dates match.";
                }
                else if (arrOk && depOk)
                {
                    rr.Result = "Name Mismatch";
                    rr.Reason = "Stay dates match exactly, but the guest names require review.";
                }
                else if (!arrOk || !depOk)
                {
                    rr.Result = "Date Mismatch";
                    var parts = new List<string>();
                    if (!arrOk) parts.Add("arrival date differs");
                    if (!depOk) parts.Add("departure date differs");
                    rr.Reason = char.ToUpper(parts[0][0]) + string.Join("; ", parts)[1..] + ".";
                }
                else
                {
                    rr.Result = "Manual Review";
                    rr.Reason = "A possible match was found, but it requires manual verification.";
                }
            }
            output.Add(rr);
        }

        foreach (var idx in remaining.OrderBy(i => opera[i].Arrival).ThenBy(i => opera[i].GuestName))
        {
            var o = opera[idx];
            output.Add(new ResultRecord
            {
                OperaConf = o.OperaConf,
                OperaGuest = o.GuestName,
                OperaArrival = o.Arrival,
                OperaDeparture = o.Departure,
                OperaStatus = o.Status,
                OperaRoom = o.RoomNumber,
                Result = "Missing in Booking.com",
                Reason = "Opera reservation was not matched to an active Booking.com booking."
            });
        }

        output.AddRange(excluded.Select(b => new ResultRecord
        {
            BookingNumber = b.BookingNumber,
            BookingGuest = b.GuestName,
            BookingArrival = b.Arrival,
            BookingDeparture = b.Departure,
            BookingStatus = b.Status,
            MatchMethod = "Excluded",
            Result = "Excluded / Cancelled",
            Reason = "Booking.com status is not active, so it is excluded from missing-booking totals."
        }));

        var order = new Dictionary<string, int>
        {
            ["Missing in Opera"] = 1,
            ["Duplicate Booking"] = 2,
            ["Split Reservation"] = 3,
            ["Date Mismatch"] = 4,
            ["Name Mismatch"] = 5,
            ["Manual Review"] = 6,
            ["Missing in Booking.com"] = 7,
            ["Perfect Match"] = 8,
            ["Excluded / Cancelled"] = 9
        };
        return output.OrderBy(r => order.GetValueOrDefault(r.Result, 99))
            .ThenBy(r => r.BookingArrival ?? r.OperaArrival)
            .ThenBy(r => r.BookingGuest)
            .ToList();
    }

    private static List<int> FindSplitReservation(BookingRecord b, IReadOnlyList<OperaRecord> opera, HashSet<int> remaining)
    {
        if (!b.Arrival.HasValue || !b.Departure.HasValue || string.IsNullOrWhiteSpace(b.NormalizedName)) return new();

        var candidates = remaining
            .Where(i => NameScore(b, opera[i]) >= 85 && opera[i].Arrival.HasValue && opera[i].Departure.HasValue)
            .OrderBy(i => opera[i].Arrival)
            .ToList();

        for (var start = 0; start < candidates.Count; start++)
        {
            var chain = new List<int> { candidates[start] };
            var first = opera[candidates[start]];
            if (first.Arrival != b.Arrival) continue;
            var end = first.Departure;

            for (var next = start + 1; next < candidates.Count && end < b.Departure; next++)
            {
                var candidate = opera[candidates[next]];
                if (candidate.Arrival == end)
                {
                    chain.Add(candidates[next]);
                    end = candidate.Departure;
                }
            }

            if (chain.Count > 1 && end == b.Departure) return chain;
        }
        return new();
    }

    private static int NameScore(BookingRecord b, OperaRecord o)
    {
        if (string.IsNullOrWhiteSpace(b.NormalizedName) || string.IsNullOrWhiteSpace(o.NormalizedName)) return 0;
        if (b.NormalizedName == o.NormalizedName) return 100;
        return Math.Max(Fuzz.TokenSortRatio(b.NormalizedName, o.NormalizedName), Fuzz.TokenSetRatio(b.NormalizedName, o.NormalizedName));
    }

    private static void CopyOpera(ResultRecord rr, OperaRecord o)
    {
        rr.OperaConf = o.OperaConf;
        rr.OperaGuest = o.GuestName;
        rr.OperaArrival = o.Arrival;
        rr.OperaDeparture = o.Departure;
        rr.OperaStatus = o.Status;
        rr.OperaRoom = o.RoomNumber;
    }

    private static ResultRecord NewBookingResult(BookingRecord b) => new()
    {
        BookingNumber = b.BookingNumber,
        BookingGuest = b.GuestName,
        BookingArrival = b.Arrival,
        BookingDeparture = b.Departure,
        BookingStatus = b.Status,
        Result = "Missing in Opera",
        Reason = "No sufficiently similar Opera reservation was found."
    };

    private static int DateDistance(BookingRecord b, OperaRecord o)
    {
        var a = b.Arrival.HasValue && o.Arrival.HasValue ? Math.Abs((b.Arrival.Value - o.Arrival.Value).Days) : 999;
        var d = b.Departure.HasValue && o.Departure.HasValue ? Math.Abs((b.Departure.Value - o.Departure.Value).Days) : 999;
        return a + d;
    }
}
