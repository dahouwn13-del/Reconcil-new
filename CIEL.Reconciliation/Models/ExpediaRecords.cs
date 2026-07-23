namespace CIEL.Reconciliation.Models;

public sealed class ExpediaRecord
{
    public string ReservationId { get; set; } = "";
    public string ExpediaConfirmation { get; set; } = "";
    public string GuestName { get; set; } = "";
    public DateTime? Arrival { get; set; }
    public DateTime? Departure { get; set; }
    public DateTime? BookedDate { get; set; }
    public string Room { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public decimal BookingAmount { get; set; }
    public string Status { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}

public sealed class ExpediaResultRecord
{
    public string ReservationId { get; set; } = "";
    public string ExpediaConfirmation { get; set; } = "";
    public string ExpediaGuest { get; set; } = "";
    public DateTime? ExpediaArrival { get; set; }
    public DateTime? ExpediaDeparture { get; set; }
    public string ExpediaStatus { get; set; } = "";
    public string PaymentType { get; set; } = "";
    public decimal BookingAmount { get; set; }
    public string RoomDescription { get; set; } = "";
    public string OperaConf { get; set; } = "";
    public string OperaGuest { get; set; } = "";
    public DateTime? OperaArrival { get; set; }
    public DateTime? OperaDeparture { get; set; }
    public string OperaStatus { get; set; } = "";
    public string OperaRoom { get; set; } = "";
    public int MatchScore { get; set; }
    public string MatchMethod { get; set; } = "";
    public string Result { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ActionRequired { get; set; } = "None";
    public string NameAnalysis { get; set; } = "";
}
