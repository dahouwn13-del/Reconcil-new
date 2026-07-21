using System.Drawing.Drawing2D;

namespace CIEL.Reconciliation;

public sealed class HomeForm : Form
{
    public HomeForm()
    {
        Text = "CIEL Reconciliation";
        Width = 1000;
        Height = 650;
        MinimumSize = new Size(850, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(70, 55, 70, 55),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "CIEL RECONCILIATION",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 30, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 76, 108)
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "Select a reconciliation platform",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13),
            ForeColor = Color.FromArgb(100, 116, 139)
        }, 0, 1);

        var tiles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(35, 35, 35, 35),
            BackColor = Color.Transparent
        };
        tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
        tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));

        var booking = CreateTile("Booking.com", "Reconcile Booking.com Excel with Opera Arrivals PDF", Color.FromArgb(0, 119, 182));
        var expedia = CreateTile("Expedia", "Reconcile Expedia reports with Opera PMS", Color.FromArgb(255, 196, 0));
        booking.Click += (_, _) => OpenModule(new MainForm());
        expedia.Click += (_, _) => OpenModule(new ExpediaForm());
        foreach (Control control in booking.Controls) control.Click += (_, _) => OpenModule(new MainForm());
        foreach (Control control in expedia.Controls) control.Click += (_, _) => OpenModule(new ExpediaForm());

        tiles.Controls.Add(booking, 0, 0);
        tiles.Controls.Add(expedia, 2, 0);
        root.Controls.Add(tiles, 0, 2);

        root.Controls.Add(new Label
        {
            Text = "Booking.com  |  Expedia",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9)
        }, 0, 3);
    }

    private void OpenModule(Form form)
    {
        Hide();
        try { form.ShowDialog(this); }
        finally { Show(); Activate(); }
    }

    private static RoundedTile CreateTile(string title, string subtitle, Color accent)
    {
        var tile = new RoundedTile
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            AccentColor = accent,
            Cursor = Cursors.Hand,
            Margin = new Padding(0)
        };
        tile.Controls.Add(new Label
        {
            Text = subtitle,
            Dock = DockStyle.Bottom,
            Height = 80,
            TextAlign = ContentAlignment.TopCenter,
            Padding = new Padding(20, 4, 20, 0),
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 10),
            BackColor = Color.Transparent
        });
        tile.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = accent,
            Font = new Font("Segoe UI Semibold", 24, FontStyle.Bold),
            BackColor = Color.Transparent
        });
        return tile;
    }
}

internal sealed class RoundedTile : Panel
{
    public int CornerRadius { get; set; } = 18;
    public Color BorderColor { get; set; } = Color.FromArgb(226, 232, 240);
    public Color AccentColor { get; set; } = Color.FromArgb(0, 119, 182);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectangle(rect, CornerRadius);
        Region = new Region(path);
        using var border = new Pen(BorderColor, 1);
        e.Graphics.DrawPath(border, path);
        using var accent = new SolidBrush(AccentColor);
        e.Graphics.FillRectangle(accent, 0, 0, 8, Height);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
