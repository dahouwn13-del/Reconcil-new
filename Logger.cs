namespace CIEL.Reconciliation;

public sealed class ExpediaForm : Form
{
    private readonly TextBox _expediaPath = CreatePathBox();
    private readonly TextBox _operaPath = CreatePathBox();

    public ExpediaForm()
    {
        Text = "CIEL Reconciliation — Expedia";
        Width = 1150;
        Height = 720;
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(36, 28, 36, 32),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildUploadPanel(), 0, 1);

        var info = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 14,
            Padding = new Padding(28),
            Margin = new Padding(0, 12, 0, 12)
        };
        info.Controls.Add(new Label
        {
            Text = "EXPEDIA MODULE READY FOR FILE MAPPING\n\nThe Expedia upload screen is now separate from Booking.com. Expedia exports use different columns and formats, so the parser will be completed using a real Expedia report and the corresponding Opera Arrivals: Detailed PDF.\n\nSelect the files below to confirm the interface. The Run button will be activated after the Expedia format is mapped.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12),
            ForeColor = Color.FromArgb(71, 85, 105)
        });
        root.Controls.Add(info, 0, 2);

        var back = new Button
        {
            Text = "BACK TO PLATFORM SELECTION",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 86, 122),
            Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 6, 0, 0)
        };
        back.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        back.Click += (_, _) => Close();
        root.Controls.Add(back, 0, 3);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Controls.Add(new Label
        {
            Text = "CIEL RECONCILIATION — EXPEDIA",
            Dock = DockStyle.Top,
            Height = 48,
            Font = new Font("Segoe UI Semibold", 24, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 76, 108),
            TextAlign = ContentAlignment.MiddleLeft
        });
        panel.Controls.Add(new Label
        {
            Text = "Expedia and Opera PMS reconciliation",
            Dock = DockStyle.Bottom,
            Height = 26,
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleLeft
        });
        return panel;
    }

    private Control BuildUploadPanel()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            CornerRadius = 14,
            Padding = new Padding(22, 18, 22, 18)
        };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        card.Controls.Add(table);

        var heading = new Label { Text = "Upload source files", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), TextAlign = ContentAlignment.MiddleLeft };
        table.SetColumnSpan(heading, 3);
        table.Controls.Add(heading, 0, 0);
        table.Controls.Add(CreateLabel("Expedia report"), 0, 1);
        table.Controls.Add(_expediaPath, 1, 1);
        var browseExpedia = CreateBrowseButton();
        browseExpedia.Click += (_, _) => Browse(_expediaPath, "Expedia reports (*.xls;*.xlsx;*.csv)|*.xls;*.xlsx;*.csv");
        table.Controls.Add(browseExpedia, 2, 1);
        table.Controls.Add(CreateLabel("Opera Arrivals PDF"), 0, 2);
        table.Controls.Add(_operaPath, 1, 2);
        var browseOpera = CreateBrowseButton();
        browseOpera.Click += (_, _) => Browse(_operaPath, "Opera PDF (*.pdf)|*.pdf");
        table.Controls.Add(browseOpera, 2, 2);

        var run = new Button
        {
            Text = "RUN EXPEDIA RECONCILIATION",
            Dock = DockStyle.Fill,
            Enabled = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(203, 213, 225),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
            Margin = new Padding(185, 7, 0, 0)
        };
        table.SetColumnSpan(run, 3);
        table.Controls.Add(run, 0, 3);
        return card;
    }

    private static TextBox CreatePathBox() => new() { ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Margin = new Padding(0, 5, 10, 5) };
    private static Label CreateLabel(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(51, 65, 85), Font = new Font("Segoe UI Semibold", 10) };
    private static Button CreateBrowseButton()
    {
        var b = new Button { Text = "Browse", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(241, 245, 249), ForeColor = Color.FromArgb(30, 41, 59), Cursor = Cursors.Hand, Margin = new Padding(0, 5, 0, 5) };
        b.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        return b;
    }
    private void Browse(TextBox target, string filter)
    {
        using var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog(this) == DialogResult.OK) target.Text = dlg.FileName;
    }
}
