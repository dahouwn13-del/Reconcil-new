using ClosedXML.Excel;
using CIEL.Reconciliation.Models;

namespace CIEL.Reconciliation.Services;

public static class ExpediaExcelExporter
{
    public static void Save(string path, IReadOnlyList<ExpediaResultRecord> rows, int expediaCount, int operaCount)
    {
        using var wb = new XLWorkbook();
        var summary = wb.AddWorksheet("Summary");
        summary.Cell("A1").Value = "CIEL Expedia Reconciliation";
        summary.Range("A1:D1").Merge().Style.Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#0B4F6C"));
        summary.Cell("A3").Value = "Expedia Records"; summary.Cell("B3").Value = expediaCount;
        summary.Cell("A4").Value = "Opera Records"; summary.Cell("B4").Value = operaCount;
        summary.Cell("A6").Value = "Result"; summary.Cell("B6").Value = "Count";
        var groups = rows.GroupBy(x => x.Result).OrderBy(x => x.Key).ToList();
        for (var i = 0; i < groups.Count; i++) { summary.Cell(7+i,1).Value = groups[i].Key; summary.Cell(7+i,2).Value = groups[i].Count(); }
        summary.RangeUsed()!.CreateTable(); summary.Columns().AdjustToContents();

        AddSheet(wb, "All Results", rows);
        foreach (var name in new[] { "Perfect Match", "Missing in Opera", "Missing in Expedia", "Date Mismatch", "Manual Review", "Excluded - Cancelled" })
        {
            var filter = name == "Excluded - Cancelled" ? "Excluded / Cancelled" : name;
            AddSheet(wb, name, rows.Where(x => x.Result == filter).ToList());
        }
        wb.SaveAs(path);
    }

    private static void AddSheet(XLWorkbook wb, string name, IReadOnlyList<ExpediaResultRecord> rows)
    {
        var ws = wb.AddWorksheet(name);
        var headers = new[] { "Reservation ID", "Expedia Confirmation", "Expedia Guest", "Expedia Arrival", "Expedia Departure", "Expedia Status", "Payment Type", "Booking Amount", "Room Description", "Opera Conf No.", "Opera Guest", "Opera Arrival", "Opera Departure", "Opera Status", "Opera Room", "Match Score", "Match Method", "Result", "Reason", "Action Required", "Name Analysis" };
        for (var c=0;c<headers.Length;c++) ws.Cell(1,c+1).Value=headers[c];
        for (var i=0;i<rows.Count;i++)
        {
            var x=rows[i]; var r=i+2;
            ws.Cell(r,1).Value=x.ReservationId; ws.Cell(r,2).Value=x.ExpediaConfirmation; ws.Cell(r,3).Value=x.ExpediaGuest;
            if(x.ExpediaArrival.HasValue) ws.Cell(r,4).Value=x.ExpediaArrival.Value; if(x.ExpediaDeparture.HasValue) ws.Cell(r,5).Value=x.ExpediaDeparture.Value;
            ws.Cell(r,6).Value=x.ExpediaStatus; ws.Cell(r,7).Value=x.PaymentType; ws.Cell(r,8).Value=x.BookingAmount; ws.Cell(r,9).Value=x.RoomDescription;
            ws.Cell(r,10).Value=x.OperaConf; ws.Cell(r,11).Value=x.OperaGuest; if(x.OperaArrival.HasValue) ws.Cell(r,12).Value=x.OperaArrival.Value; if(x.OperaDeparture.HasValue) ws.Cell(r,13).Value=x.OperaDeparture.Value;
            ws.Cell(r,14).Value=x.OperaStatus; ws.Cell(r,15).Value=x.OperaRoom; ws.Cell(r,16).Value=x.MatchScore; ws.Cell(r,17).Value=x.MatchMethod; ws.Cell(r,18).Value=x.Result; ws.Cell(r,19).Value=x.Reason; ws.Cell(r,20).Value=x.ActionRequired; ws.Cell(r,21).Value=x.NameAnalysis;
        }
        var used=ws.RangeUsed(); if(used is null) return; used.CreateTable();
        ws.Row(1).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#0077B6"));
        ws.Columns(4,5).Style.DateFormat.Format="dd-mmm-yyyy"; ws.Columns(12,13).Style.DateFormat.Format="dd-mmm-yyyy"; ws.Column(8).Style.NumberFormat.Format="#,##0.00";
        ws.SheetView.FreezeRows(1); ws.Columns().AdjustToContents(5,45); ws.Column(9).Width=45; ws.Column(19).Width=45; ws.Column(20).Width=34; ws.Column(21).Width=55;
    }
}
