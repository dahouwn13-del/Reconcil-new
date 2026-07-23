using CIEL.Reconciliation.Logging;
using CIEL.Reconciliation.Models;

namespace CIEL.Reconciliation.Services;

public static class ExpediaReconciliationEngine
{
    public static List<ExpediaResultRecord> Run(IReadOnlyList<ExpediaRecord> expedia, IReadOnlyList<OperaRecord> opera)
    {
        var active = expedia.Where(x => !x.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)).ToList();
        var cancelled = expedia.Where(x => x.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)).ToList();
        var remaining = new HashSet<int>(Enumerable.Range(0, opera.Count));
        var output = new List<ExpediaResultRecord>();

        Logger.Info($"Expedia engine started with {active.Count} active and {cancelled.Count} cancelled reservations.");
        foreach (var booking in active)
        {
            int? chosen = null;
            SmartNameMatchResult? chosenName = null;
            var bestRank = int.MinValue;
            foreach (var index in remaining)
            {
                var candidate = opera[index];
                var name = SmartNameMatcher.Compare(booking.GuestName, candidate.GuestName, booking.NormalizedName, candidate.NormalizedName);
                var exactDates = booking.Arrival == candidate.Arrival && booking.Departure == candidate.Departure;
                var nextDay = booking.Arrival.HasValue && candidate.Arrival == booking.Arrival.Value.AddDays(1) && booking.Departure == candidate.Departure;
                var distance = DateDistance(booking, candidate);
                var eligible = exactDates ? name.Score >= 60 : nextDay ? name.Score >= 82 : name.Score >= 82 && distance <= 4;
                if (!eligible) continue;
                var rank = name.Score + (exactDates ? 35 : nextDay ? 30 : Math.Max(0, 18 - distance * 4));
                if (rank > bestRank) { chosen = index; chosenName = name; bestRank = rank; }
            }

            var result = NewResult(booking);
            if (chosen is int selected && chosenName is not null)
            {
                var op = opera[selected];
                remaining.Remove(selected);
                CopyOpera(result, op);
                result.MatchScore = chosenName.Score;
                result.MatchMethod = chosenName.Method;
                result.NameAnalysis = chosenName.Explanation;
                var exact = booking.Arrival == op.Arrival && booking.Departure == op.Departure;
                var nextDay = booking.Arrival.HasValue && op.Arrival == booking.Arrival.Value.AddDays(1) && booking.Departure == op.Departure;
                if (nextDay)
                {
                    result.Result = "Perfect Match";
                    result.Reason = "Guest and departure date match; Opera arrival is one day later than Expedia.";
                    result.ActionRequired = "Check if No-Show was charged";
                }
                else if (exact && chosenName.Score >= 88)
                {
                    result.Result = "Perfect Match";
                    result.Reason = "Guest name and stay dates match.";
                }
                else if (exact)
                {
                    result.Result = "Manual Review";
                    result.Reason = "Stay dates match, but the guest name requires review.";
                    result.ActionRequired = "Verify guest name";
                }
                else
                {
                    result.Result = "Date Mismatch";
                    result.Reason = "A likely Opera reservation was found, but one or both stay dates differ.";
                    result.ActionRequired = "Verify stay dates";
                }
            }
            output.Add(result);
        }

        foreach (var index in remaining)
        {
            var op = opera[index];
            output.Add(new ExpediaResultRecord
            {
                OperaConf = op.OperaConf, OperaGuest = op.GuestName, OperaArrival = op.Arrival,
                OperaDeparture = op.Departure, OperaStatus = op.Status, OperaRoom = op.RoomNumber,
                Result = "Missing in Expedia", Reason = "Opera reservation was not matched to an active Expedia reservation.",
                ActionRequired = "Investigate Opera reservation"
            });
        }

        output.AddRange(cancelled.Select(x => {
            var r = NewResult(x); r.Result = "Excluded / Cancelled"; r.MatchMethod = "Excluded";
            r.Reason = "Expedia status is cancelled and is excluded from missing-reservation totals."; return r;
        }));

        var order = new Dictionary<string,int> { ["Missing in Opera"] = 1, ["Date Mismatch"] = 2, ["Manual Review"] = 3, ["Missing in Expedia"] = 4, ["Perfect Match"] = 5, ["Excluded / Cancelled"] = 6 };
        return output.OrderBy(x => order.GetValueOrDefault(x.Result, 99)).ThenBy(x => x.ExpediaArrival ?? x.OperaArrival).ThenBy(x => x.ExpediaGuest).ToList();
    }

    private static ExpediaResultRecord NewResult(ExpediaRecord x) => new()
    {
        ReservationId = x.ReservationId, ExpediaConfirmation = x.ExpediaConfirmation, ExpediaGuest = x.GuestName,
        ExpediaArrival = x.Arrival, ExpediaDeparture = x.Departure, ExpediaStatus = x.Status,
        PaymentType = x.PaymentType, BookingAmount = x.BookingAmount, RoomDescription = x.Room,
        Result = "Missing in Opera", Reason = "No matching Opera reservation was found.", ActionRequired = "Investigate reservation"
    };

    private static void CopyOpera(ExpediaResultRecord r, OperaRecord o)
    {
        r.OperaConf = o.OperaConf; r.OperaGuest = o.GuestName; r.OperaArrival = o.Arrival;
        r.OperaDeparture = o.Departure; r.OperaStatus = o.Status; r.OperaRoom = o.RoomNumber;
    }

    private static int DateDistance(ExpediaRecord b, OperaRecord o)
    {
        var total = 0;
        if (b.Arrival.HasValue && o.Arrival.HasValue) total += Math.Abs((b.Arrival.Value - o.Arrival.Value).Days); else total += 10;
        if (b.Departure.HasValue && o.Departure.HasValue) total += Math.Abs((b.Departure.Value - o.Departure.Value).Days); else total += 10;
        return total;
    }
}
