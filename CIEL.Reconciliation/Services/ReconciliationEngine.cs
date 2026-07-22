using CIEL.Reconciliation.Models;
using CIEL.Reconciliation.Logging;

namespace CIEL.Reconciliation.Services;

public static class ReconciliationEngine
{
    private static readonly HashSet<string> ActiveStatuses = new(StringComparer.OrdinalIgnoreCase) { "ok", "no_show" };

    public static List<ResultRecord> Run(
        IReadOnlyList<BookingRecord> bookings,
        IReadOnlyList<OperaRecord> opera)
    {
        Logger.Info($"Engine started with {bookings.Count} Booking.com records and {opera.Count} Opera records.");
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

        var processed = 0;
        foreach (var booking in active)
        {
            processed++;
            Logger.Info($"Matching {processed} of {active.Count}.", reservationNumber: booking.BookingNumber, guestName: booking.GuestName);

            if (duplicateBookingNumbers.Contains(booking.BookingNumber))
            {
                var duplicate = NewBookingResult(booking);
                duplicate.Result = "Duplicate Booking";
                duplicate.Reason = "The same Booking.com reservation number appears more than once in the source file.";
                duplicate.ActionRequired = "Verify duplicate Booking.com reservation";
                duplicate.MatchMethod = "Duplicate check";
                output.Add(duplicate);
                Logger.Warning("Duplicate Booking.com number detected.", reservationNumber: booking.BookingNumber, guestName: booking.GuestName);
                continue;
            }

            var split = FindSplitReservation(booking, opera, remaining);
            if (split.Count > 1)
            {
                foreach (var matchIndex in split)
                    remaining.Remove(matchIndex);

                var records = split.Select(index => opera[index]).OrderBy(record => record.Arrival).ToList();
                var nameMatches = records.Select(record => SmartNameMatcher.Compare(booking, record)).ToList();
                var bestName = nameMatches.OrderByDescending(match => match.Score).First();

                output.Add(new ResultRecord
                {
                    BookingNumber = booking.BookingNumber,
                    BookingGuest = booking.GuestName,
                    BookingArrival = booking.Arrival,
                    BookingDeparture = booking.Departure,
                    BookingStatus = booking.Status,
                    OperaConf = string.Join(" / ", records.Select(record => record.OperaConf).Where(value => !string.IsNullOrWhiteSpace(value))),
                    OperaGuest = string.Join(" / ", records.Select(record => record.GuestName).Distinct()),
                    OperaArrival = records.Min(record => record.Arrival),
                    OperaDeparture = records.Max(record => record.Departure),
                    OperaStatus = string.Join(" / ", records.Select(record => record.Status).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()),
                    OperaRoom = string.Join(" / ", records.Select(record => record.RoomNumber).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()),
                    MatchScore = bestName.Score,
                    MatchMethod = "Smart name + split stay",
                    Result = "Split Reservation",
                    Reason = $"The Booking.com stay matches {records.Count} contiguous Opera reservations.",
                    ActionRequired = "Verify split reservation",
                    NameAnalysis = bestName.Explanation
                });

                Logger.Success($"Split stay found using {split.Count} Opera reservations.", reservationNumber: booking.BookingNumber, guestName: booking.GuestName);
                continue;
            }

            int? chosenIndex = null;
            SmartNameMatchResult? chosenNameMatch = null;
            var bestCandidateRank = int.MinValue;

            foreach (var operaIndex in remaining)
            {
                var operaRecord = opera[operaIndex];
                var nameMatch = SmartNameMatcher.Compare(booking, operaRecord);
                var datesExact = booking.Arrival == operaRecord.Arrival && booking.Departure == operaRecord.Departure;
                var nextDayArrival = IsNextDayArrival(booking, operaRecord);
                var dateDistance = DateDistance(booking, operaRecord);

                var eligible = datesExact
                    ? nameMatch.Score >= 60
                    : nextDayArrival
                        ? nameMatch.Score >= 82
                        : nameMatch.Score >= 80 && dateDistance <= 4;

                if (!eligible)
                    continue;

                var dateBonus = datesExact ? 35 : nextDayArrival ? 30 : Math.Max(0, 18 - (dateDistance * 4));
                var candidateRank = nameMatch.Score + dateBonus;
                if (candidateRank <= bestCandidateRank)
                    continue;

                chosenIndex = operaIndex;
                chosenNameMatch = nameMatch;
                bestCandidateRank = candidateRank;
            }

            var result = NewBookingResult(booking);
            if (chosenIndex is int selectedIndex && chosenNameMatch is not null)
            {
                var operaRecord = opera[selectedIndex];
                remaining.Remove(selectedIndex);
                CopyOpera(result, operaRecord);
                result.MatchScore = chosenNameMatch.Score;
                result.MatchMethod = chosenNameMatch.Method;
                result.NameAnalysis = chosenNameMatch.Explanation;

                var arrivalMatches = booking.Arrival == operaRecord.Arrival;
                var departureMatches = booking.Departure == operaRecord.Departure;
                var nextDayArrival = IsNextDayArrival(booking, operaRecord);

                if (nextDayArrival && chosenNameMatch.Score >= 82)
                {
                    result.Result = "Perfect Match";
                    result.MatchMethod = $"{chosenNameMatch.Method} + next-day arrival";
                    result.Reason = "Guest name and departure date match; the guest arrived one day after the Booking.com arrival date.";
                    result.ActionRequired = "Check if No-Show was charged";
                }
                else if (arrivalMatches && departureMatches && chosenNameMatch.Score >= 88)
                {
                    result.Result = "Perfect Match";
                    result.Reason = "Guest name and stay dates match.";
                    result.ActionRequired = "None";
                }
                else if (arrivalMatches && departureMatches)
                {
                    result.Result = "Name Mismatch";
                    result.Reason = "Stay dates match exactly, but the guest names require review.";
                    result.ActionRequired = "Verify guest name";
                }
                else
                {
                    result.Result = "Date Mismatch";
                    var differences = new List<string>();
                    if (!arrivalMatches) differences.Add("arrival date differs");
                    if (!departureMatches) differences.Add("departure date differs");
                    result.Reason = Capitalize(string.Join("; ", differences)) + ".";
                    result.ActionRequired = "Verify stay dates";
                }
            }

            output.Add(result);
            LogResult(result);
        }

        foreach (var operaIndex in remaining.OrderBy(index => opera[index].Arrival).ThenBy(index => opera[index].GuestName))
        {
            var operaRecord = opera[operaIndex];
            output.Add(new ResultRecord
            {
                OperaConf = operaRecord.OperaConf,
                OperaGuest = operaRecord.GuestName,
                OperaArrival = operaRecord.Arrival,
                OperaDeparture = operaRecord.Departure,
                OperaStatus = operaRecord.Status,
                OperaRoom = operaRecord.RoomNumber,
                Result = "Missing in Booking.com",
                Reason = "Opera reservation was not matched to an active Booking.com booking.",
                ActionRequired = "Investigate Opera reservation"
            });
        }

        output.AddRange(excluded.Select(booking => new ResultRecord
        {
            BookingNumber = booking.BookingNumber,
            BookingGuest = booking.GuestName,
            BookingArrival = booking.Arrival,
            BookingDeparture = booking.Departure,
            BookingStatus = booking.Status,
            MatchMethod = "Excluded",
            Result = "Excluded / Cancelled",
            Reason = "Booking.com status is not active, so it is excluded from missing-booking totals.",
            ActionRequired = "None"
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

        Logger.Success($"Engine completed. Output rows: {output.Count}. Unmatched Opera rows: {remaining.Count}.");
        return output.OrderBy(row => order.GetValueOrDefault(row.Result, 99))
            .ThenBy(row => row.BookingArrival ?? row.OperaArrival)
            .ThenBy(row => row.BookingGuest)
            .ToList();
    }

    private static List<int> FindSplitReservation(BookingRecord booking, IReadOnlyList<OperaRecord> opera, HashSet<int> remaining)
    {
        if (!booking.Arrival.HasValue || !booking.Departure.HasValue || string.IsNullOrWhiteSpace(booking.NormalizedName))
            return new List<int>();

        var candidates = remaining
            .Where(index => SmartNameMatcher.Compare(booking, opera[index]).Score >= 85 && opera[index].Arrival.HasValue && opera[index].Departure.HasValue)
            .OrderBy(index => opera[index].Arrival)
            .ToList();

        for (var start = 0; start < candidates.Count; start++)
        {
            var chain = new List<int> { candidates[start] };
            var first = opera[candidates[start]];
            if (first.Arrival != booking.Arrival)
                continue;

            var end = first.Departure;
            for (var next = start + 1; next < candidates.Count && end < booking.Departure; next++)
            {
                var candidate = opera[candidates[next]];
                if (candidate.Arrival == end)
                {
                    chain.Add(candidates[next]);
                    end = candidate.Departure;
                }
            }

            if (chain.Count > 1 && end == booking.Departure)
                return chain;
        }

        return new List<int>();
    }

    private static bool IsNextDayArrival(BookingRecord booking, OperaRecord opera) =>
        booking.Arrival.HasValue &&
        opera.Arrival.HasValue &&
        booking.Departure.HasValue &&
        opera.Departure.HasValue &&
        opera.Arrival.Value.Date == booking.Arrival.Value.Date.AddDays(1) &&
        opera.Departure.Value.Date == booking.Departure.Value.Date;

    private static void CopyOpera(ResultRecord result, OperaRecord opera)
    {
        result.OperaConf = opera.OperaConf;
        result.OperaGuest = opera.GuestName;
        result.OperaArrival = opera.Arrival;
        result.OperaDeparture = opera.Departure;
        result.OperaStatus = opera.Status;
        result.OperaRoom = opera.RoomNumber;
    }

    private static ResultRecord NewBookingResult(BookingRecord booking) => new()
    {
        BookingNumber = booking.BookingNumber,
        BookingGuest = booking.GuestName,
        BookingArrival = booking.Arrival,
        BookingDeparture = booking.Departure,
        BookingStatus = booking.Status,
        Result = "Missing in Opera",
        Reason = "No sufficiently similar Opera reservation was found.",
        ActionRequired = "Investigate reservation"
    };

    private static int DateDistance(BookingRecord booking, OperaRecord opera)
    {
        var arrivalDistance = booking.Arrival.HasValue && opera.Arrival.HasValue
            ? Math.Abs((booking.Arrival.Value.Date - opera.Arrival.Value.Date).Days)
            : 999;
        var departureDistance = booking.Departure.HasValue && opera.Departure.HasValue
            ? Math.Abs((booking.Departure.Value.Date - opera.Departure.Value.Date).Days)
            : 999;
        return arrivalDistance + departureDistance;
    }

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static void LogResult(ResultRecord result)
    {
        if (result.Result == "Perfect Match" && result.ActionRequired == "None")
        {
            Logger.Success($"Matched. Name score {result.MatchScore}% via {result.MatchMethod}.", reservationNumber: result.BookingNumber, guestName: result.BookingGuest);
            return;
        }

        if (result.Result == "Perfect Match")
        {
            Logger.Warning($"Matched with action required: {result.ActionRequired}. Name score {result.MatchScore}%.", reservationNumber: result.BookingNumber, guestName: result.BookingGuest);
            return;
        }

        if (result.Result == "Missing in Opera")
        {
            Logger.Warning("Missing in Opera.", reservationNumber: result.BookingNumber, guestName: result.BookingGuest);
            return;
        }

        Logger.Warning($"{result.Result}: {result.Reason} Action: {result.ActionRequired}.", reservationNumber: result.BookingNumber, guestName: result.BookingGuest);
    }
}
