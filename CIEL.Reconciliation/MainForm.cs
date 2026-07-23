using System.Drawing.Drawing2D;
using CIEL.Reconciliation.Models;
using CIEL.Reconciliation.Services;
using CIEL.Reconciliation.Logging;

namespace CIEL.Reconciliation;

public sealed class MainForm : Form
{
    private readonly TextBox _bookingPath = CreatePathBox();
    private readonly TextBox _operaPath = CreatePathBox();
    private readonly Button _run = CreatePrimaryButton("RUN RECONCILIATION");
    private readonly Button _export = CreateSecondaryButton("EXPORT TO EXCEL");
    private readonly Button _toggleLog = CreateSecondaryButton("WORK LOG");
    private LogForm? _logForm;
    private TableLayoutPanel? _root;
    private readonly ToolStripStatusLabel _statusSummary = new("Ready");
    private readonly ToolStripStatusLabel _statusSpacer = new() { Spring = true };
    private readonly ToolStripStatusLabel _versionLabel = new("Version 5.3.4");
    private readonly Label _status = new()
    {
        Text = "Select the Booking.com Excel file and Opera Arrivals: Detailed PDF.",
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(80, 91, 105)
    };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 4, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly TextBox _search = new() { BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Search guest, booking number or Opera confirmation...", Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        ScrollBars = ScrollBars.Both,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        GridColor = Color.FromArgb(226, 232, 240)
    };
    private readonly FlowLayoutPanel _cards = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = false,
        FlowDirection = FlowDirection.LeftToRight,
        BackColor = Color.Transparent
    };

    private List<ResultRecord> _results = new();
    private int _bookingCount;
    private int _operaCount;
    private string _activeFilter = "All";

    public MainForm()
    {
        Text = "CIEL Reconciliation — Booking.com";
        Width = 1450;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        AllowDrop = true;

        ConfigureGrid();
        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 24),
            RowCount = 6,
            ColumnCount = 1,
            BackColor = BackColor,
            AutoSize = false,
            AutoScroll = false,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(_root);

        _root.Controls.Add(BuildHeader(), 0, 0);
        _root.Controls.Add(BuildUploadPanel(), 0, 1);
        _root.Controls.Add(_cards, 0, 2);
        _root.Controls.Add(BuildStatusPanel(), 0, 3);
        _root.Controls.Add(BuildSearchPanel(), 0, 4);
        _root.Controls.Add(BuildGridPanel(), 0, 5);
        Controls.Add(BuildStatusStrip());

        _run.Click += async (_, _) => await RunAsync();
        _export.Click += async (_, _) => await ExportAsync();
        _toggleLog.Click += (_, _) => ShowWorkLog();
        _search.TextChanged += (_, _) => ApplyFilter();
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        _export.Enabled = false;
        AddEmptyCards();

        // Re-apply the grid viewport after maximize/restore and DPI-driven resize.
        Shown += (_, _) => EnsureGridViewport();
        ResizeEnd += (_, _) => EnsureGridViewport();
        SizeChanged += (_, _) =>
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(EnsureGridViewport));
        };
    }

    private static TextBox CreatePathBox() => new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.White,
        Margin = new Padding(0, 5, 10, 5)
    };

    private static Button CreatePrimaryButton(string text) => new()
    {
        Text = text,
        Height = 48,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(0, 104, 146),
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0)
    };

    private static Button CreateSecondaryButton(string text) => new()
    {
        Text = text,
        Height = 48,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.White,
        ForeColor = Color.FromArgb(0, 86, 122),
        Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0)
    };

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        panel.Controls.Add(new Label
        {
            Text = "CIEL RECONCILIATION — BOOKING.COM",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI Semibold", 24, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 76, 108),
            TextAlign = ContentAlignment.MiddleLeft
        });
        panel.Controls.Add(new Label
        {
            Text = "Booking.com and Opera PMS reconciliation",
            Dock = DockStyle.Bottom,
            Height = 24,
            Font = new Font("Segoe UI", 10),
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
            Padding = new Padding(22, 18, 22, 18),
            Margin = new Padding(0, 4, 0, 12)
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, BackColor = Color.Transparent };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        card.Controls.Add(table);

        var heading = new Label
        {
            Text = "Upload source files",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            TextAlign = ContentAlignment.MiddleLeft
        };
        table.SetColumnSpan(heading, 3);
        table.Controls.Add(heading, 0, 0);

        table.Controls.Add(CreateFieldLabel("Booking.com Excel"), 0, 1);
        table.Controls.Add(_bookingPath, 1, 1);
        var browseBooking = CreateBrowseButton();
        browseBooking.Click += (_, _) => BrowseBooking();
        table.Controls.Add(browseBooking, 2, 1);

        table.Controls.Add(CreateFieldLabel("Opera Arrivals PDF"), 0, 2);
        table.Controls.Add(_operaPath, 1, 2);
        var browseOpera = CreateBrowseButton();
        browseOpera.Click += (_, _) => BrowseOpera();
        table.Controls.Add(browseOpera, 2, 2);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(185, 6, 0, 4),
            Margin = new Padding(0)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        actions.Controls.Add(_run, 0, 0);
        actions.Controls.Add(_toggleLog, 2, 0);
        actions.Controls.Add(_export, 4, 0);
        table.SetColumnSpan(actions, 3);
        table.Controls.Add(actions, 0, 3);

        return card;
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(51, 65, 85),
        Font = new Font("Segoe UI Semibold", 10)
    };

    private static Button CreateBrowseButton()
    {
        var button = new Button
        {
            Text = "Browse",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(30, 41, 59),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 5, 0, 5)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        return button;
    }

    private Control BuildStatusPanel()
    {
        var panel = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.White, CornerRadius = 10, Padding = new Padding(16, 6, 16, 6), Margin = new Padding(0, 6, 0, 6) };
        panel.Controls.Add(_status);
        panel.Controls.Add(_progress);
        return panel;
    }

    private Control BuildSearchPanel()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 4, 0, 4) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.Controls.Add(new Label { Text = "Search", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 10), ForeColor = Color.FromArgb(51, 65, 85) }, 0, 0);
        table.Controls.Add(_search, 1, 0);
        var clear = CreateBrowseButton();
        clear.Text = "CLEAR FILTER";
        clear.Margin = new Padding(10, 2, 0, 2);
        clear.Click += (_, _) => { _activeFilter = "All"; _search.Clear(); UpdateCards(_bookingCount, _operaCount, _results); ApplyFilter(); };
        table.Controls.Add(clear, 2, 0);
        return table;
    }

    private Control BuildGridPanel()
    {
        // Keep the DataGridView in a non-rounded host. A rounded Region can clip the
        // native horizontal scrollbar when the form is maximized or DPI scaling changes.
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = Padding.Empty,
            Margin = new Padding(0, 4, 0, 0),
            AutoScroll = false,
            MinimumSize = new Size(0, 150)
        };
        _grid.Margin = Padding.Empty;
        panel.Controls.Add(_grid);
        return panel;
    }


    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip
        {
            SizingGrip = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Bottom
        };
        strip.Items.Add(_statusSummary);
        strip.Items.Add(_statusSpacer);
        strip.Items.Add(_versionLabel);
        return strip;
    }

    private void ShowWorkLog()
    {
        if (_logForm == null || _logForm.IsDisposed)
        {
            _logForm = new LogForm();
            _logForm.FormClosed += (_, _) => _logForm = null;
        }

        if (!_logForm.Visible)
            _logForm.Show(this);

        if (_logForm.WindowState == FormWindowState.Minimized)
            _logForm.WindowState = FormWindowState.Normal;

        _logForm.BringToFront();
        _logForm.Activate();
    }

    private void ConfigureGrid()
    {
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 86, 122);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        _grid.ColumnHeadersHeight = 38;
        _grid.RowTemplate.Height = 32;
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 238, 247);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.DataBindingComplete += (_, _) => FormatGrid();
    }

    private void EnsureGridViewport()
    {
        if (_grid.IsDisposed || !_grid.IsHandleCreated) return;

        _grid.Dock = DockStyle.Fill;
        _grid.ScrollBars = ScrollBars.Both;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.PerformLayout();
        _grid.Invalidate();
    }

    private void BrowseBooking()
    {
        using var dlg = new OpenFileDialog { Filter = "Booking.com Excel (*.xls;*.xlsx)|*.xls;*.xlsx" };
        if (dlg.ShowDialog(this) == DialogResult.OK) _bookingPath.Text = dlg.FileName;
    }

    private void BrowseOpera()
    {
        using var dlg = new OpenFileDialog { Filter = "Opera PDF (*.pdf)|*.pdf" };
        if (dlg.ShowDialog(this) == DialogResult.OK) _operaPath.Text = dlg.FileName;
    }

    private async Task RunAsync()
    {
        if (!File.Exists(_bookingPath.Text) || !File.Exists(_operaPath.Text))
        {
            MessageBox.Show(this, "Please select both source files.", "Files required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Logger.Clear();
        _logForm?.ResetForNewRun();
        Logger.Info("Booking.com reconciliation started.");
        Logger.Info($"Booking.com file: {Path.GetFileName(_bookingPath.Text)}");
        Logger.Info($"Opera file: {Path.GetFileName(_operaPath.Text)}");

        SetBusy(true, "Reading files and reconciling bookings...");
        try
        {
            var data = await Task.Run(() =>
            {
                Logger.Info("Reading Booking.com Excel file...");
                var bookings = BookingExcelReader.Read(_bookingPath.Text);
                Logger.Success($"Loaded {bookings.Count} Booking.com records.");

                Logger.Info("Reading Opera Arrivals PDF...");
                var opera = OperaPdfReader.Read(_operaPath.Text);
                Logger.Success($"Loaded {opera.Count} Opera records.");

                Logger.Info("Starting matching engine...");
                var results = ReconciliationEngine.Run(bookings, opera);
                return (bookings, opera, results);
            });

            _bookingCount = data.bookings.Count;
            _operaCount = data.opera.Count;
            _results = data.results;
            _activeFilter = "All";
            _search.Clear();
            UpdateCards(_bookingCount, _operaCount, _results);
            ApplyFilter();
            _export.Enabled = true;
            _status.Text = $"Completed successfully — {_bookingCount} Booking.com records and {_operaCount} Opera records processed.";
            _statusSummary.Text = $"Ready | Booking.com: {_bookingCount:N0} | Opera: {_operaCount:N0} | Results: {_results.Count:N0} | Last run: {DateTime.Now:HH:mm}";
            Logger.Success($"{_results.Count} result rows created.");
            Logger.Success($"Summary: {_results.Count(r => r.Result == "Perfect Match")} perfect, {_results.Count(r => r.Result == "Missing in Opera")} missing in Opera, {_results.Count(r => r.Result == "Date Mismatch")} date mismatches, {_results.Count(r => r.Result == "Split Reservation")} split stays.");
        }
        catch (Exception ex)
        {
            _status.Text = "Reconciliation failed.";
            _statusSummary.Text = $"Error | Last run: {DateTime.Now:HH:mm}";
            Logger.Error(ex.Message);
            Logger.Error(ex.StackTrace ?? "No technical stack trace was available.");
            MessageBox.Show(this, ex.Message, "Reconciliation error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExportAsync()
    {
        if (_results.Count == 0) return;
        using var save = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"Booking_Reconciliation_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx"
        };
        if (save.ShowDialog(this) != DialogResult.OK) return;

        SetBusy(true, "Creating reconciliation workbook...");
        try
        {
            await Task.Run(() => ExcelExporter.Save(save.FileName, _results, _bookingCount, _operaCount));
            _status.Text = $"Report saved: {save.FileName}";
            _statusSummary.Text = $"Report exported | {DateTime.Now:HH:mm}";
            var open = MessageBox.Show(this, "Reconciliation report created successfully. Open it now?", "Export completed", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (open == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(save.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _run.Enabled = !busy;
        _export.Enabled = !busy && _results.Count > 0;
        _progress.Visible = busy;
        UseWaitCursor = busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _status.Text = message;
            _statusSummary.Text = message;
        }
        else if (!busy && _results.Count == 0)
        {
            _statusSummary.Text = "Ready";
        }
    }

    private void AddEmptyCards() => UpdateCards(0, 0, Array.Empty<ResultRecord>());

    private void UpdateCards(int bookingCount, int operaCount, IReadOnlyList<ResultRecord> rows)
    {
        _cards.SuspendLayout();
        _cards.Controls.Clear();
        AddCard("All", "Booking.com", bookingCount, Color.FromArgb(0, 119, 182));
        AddCard("All", "Opera PMS", operaCount, Color.FromArgb(100, 72, 170));
        AddCard("Perfect Match", "Perfect Match", rows.Count(r => r.Result == "Perfect Match"), Color.FromArgb(22, 163, 74));
        AddCard("Date Mismatch", "Date Mismatch", rows.Count(r => r.Result == "Date Mismatch"), Color.FromArgb(234, 88, 12));
        AddCard("Split Reservation", "Split Stays", rows.Count(r => r.Result == "Split Reservation"), Color.FromArgb(2, 132, 199));
        AddCard("Missing in Opera", "Missing in Opera", rows.Count(r => r.Result == "Missing in Opera"), Color.FromArgb(220, 38, 38));
        AddCard("Missing in Booking.com", "Missing in Booking", rows.Count(r => r.Result == "Missing in Booking.com"), Color.FromArgb(153, 27, 27));
        AddCard("Manual Review", "Manual Review", rows.Count(r => r.Result == "Manual Review"), Color.FromArgb(202, 138, 4));
        _cards.ResumeLayout();
    }

    private void AddCard(string filter, string label, int value, Color accent)
    {
        var selected = _activeFilter == filter || (_activeFilter == "All" && filter == "All" && label == "Booking.com");
        var panel = new RoundedPanel
        {
            Width = 180,
            Height = 94,
            Margin = new Padding(0, 7, 12, 7),
            BackColor = Color.White,
            CornerRadius = 12,
            Cursor = Cursors.Hand,
            Tag = filter,
            BorderColor = selected ? accent : Color.FromArgb(226, 232, 240),
            BorderWidth = selected ? 2 : 1
        };
        panel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 6, BackColor = accent });
        panel.Controls.Add(new Label { Text = value.ToString("N0"), Font = new Font("Segoe UI Semibold", 20, FontStyle.Bold), AutoSize = false, Height = 48, Dock = DockStyle.Top, Padding = new Padding(15, 8, 5, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = accent, BackColor = Color.Transparent });
        panel.Controls.Add(new Label { Text = label, AutoSize = false, Height = 34, Dock = DockStyle.Bottom, Padding = new Padding(15, 0, 4, 7), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(71, 85, 105), BackColor = Color.Transparent });
        panel.Click += CardClicked;
        foreach (Control control in panel.Controls) control.Click += CardClicked;
        _cards.Controls.Add(panel);
    }

    private void CardClicked(object? sender, EventArgs e)
    {
        Control? control = sender as Control;
        while (control != null && control.Tag is not string) control = control.Parent;
        if (control?.Tag is not string filter) return;
        _activeFilter = filter;
        UpdateCards(_bookingCount, _operaCount, _results);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<ResultRecord> query = _results;
        if (_activeFilter != "All") query = query.Where(r => r.Result == _activeFilter);

        var term = _search.Text.Trim();
        if (term.Length > 0)
        {
            query = query.Where(r =>
                Contains(r.BookingNumber, term) || Contains(r.BookingGuest, term) ||
                Contains(r.OperaConf, term) || Contains(r.OperaGuest, term) ||
                Contains(r.Result, term) || Contains(r.Reason, term) || Contains(r.ActionRequired, term) || Contains(r.NameAnalysis, term));
        }
        _grid.DataSource = query.ToList();
    }

    private static bool Contains(string? value, string term) => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private void FormatGrid()
    {
        void SetColumnWidth(string columnName, int width)
        {
            if (!_grid.Columns.Contains(columnName)) return;
            var column = _grid.Columns[columnName]!;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = Math.Min(width, 70);
        }

        var friendly = new Dictionary<string, string>
        {
            [nameof(ResultRecord.BookingNumber)] = "Booking.com No.",
            [nameof(ResultRecord.BookingGuest)] = "Booking.com Guest",
            [nameof(ResultRecord.BookingArrival)] = "Booking Arrival",
            [nameof(ResultRecord.BookingDeparture)] = "Booking Departure",
            [nameof(ResultRecord.BookingStatus)] = "Booking Status",
            [nameof(ResultRecord.OperaConf)] = "Opera Conf.",
            [nameof(ResultRecord.OperaGuest)] = "Opera Guest",
            [nameof(ResultRecord.OperaArrival)] = "Opera Arrival",
            [nameof(ResultRecord.OperaDeparture)] = "Opera Departure",
            [nameof(ResultRecord.OperaStatus)] = "Opera Status",
            [nameof(ResultRecord.OperaRoom)] = "Opera Room",
            [nameof(ResultRecord.MatchScore)] = "Score",
            [nameof(ResultRecord.MatchMethod)] = "Match Method",
            [nameof(ResultRecord.Result)] = "Result",
            [nameof(ResultRecord.Reason)] = "Reason",
            [nameof(ResultRecord.ActionRequired)] = "Action Required",
            [nameof(ResultRecord.NameAnalysis)] = "Smart Name Analysis"
        };
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            if (friendly.TryGetValue(column.Name, out var text)) column.HeaderText = text;
            if (column.ValueType == typeof(DateTime?) || column.ValueType == typeof(DateTime)) column.DefaultCellStyle.Format = "dd/MM/yyyy";
        }
        SetColumnWidth(nameof(ResultRecord.BookingNumber), 135);
        SetColumnWidth(nameof(ResultRecord.BookingGuest), 190);
        SetColumnWidth(nameof(ResultRecord.BookingArrival), 125);
        SetColumnWidth(nameof(ResultRecord.BookingDeparture), 135);
        SetColumnWidth(nameof(ResultRecord.BookingStatus), 120);
        SetColumnWidth(nameof(ResultRecord.OperaConf), 105);
        SetColumnWidth(nameof(ResultRecord.OperaGuest), 190);
        SetColumnWidth(nameof(ResultRecord.OperaArrival), 120);
        SetColumnWidth(nameof(ResultRecord.OperaDeparture), 130);
        SetColumnWidth(nameof(ResultRecord.OperaStatus), 110);
        SetColumnWidth(nameof(ResultRecord.OperaRoom), 100);
        SetColumnWidth(nameof(ResultRecord.MatchScore), 75);
        SetColumnWidth(nameof(ResultRecord.MatchMethod), 150);
        SetColumnWidth(nameof(ResultRecord.Result), 145);
        if (_grid.Columns.Contains(nameof(ResultRecord.Reason))) _grid.Columns[nameof(ResultRecord.Reason)]!.Width = 280;
        if (_grid.Columns.Contains(nameof(ResultRecord.ActionRequired))) _grid.Columns[nameof(ResultRecord.ActionRequired)]!.Width = 220;
        if (_grid.Columns.Contains(nameof(ResultRecord.NameAnalysis)))
        {
            var nameAnalysisColumn = _grid.Columns[nameof(ResultRecord.NameAnalysis)]!;
            nameAnalysisColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            nameAnalysisColumn.Width = 340;
            nameAnalysisColumn.MinimumWidth = 220;
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files == null) return;
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".xls" or ".xlsx") _bookingPath.Text = file;
            else if (ext == ".pdf") _operaPath.Text = file;
        }
    }
}

internal sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 12;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int BorderWidth { get; set; } = 1;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectangle(rect, CornerRadius);
        Region = new Region(path);
        if (BorderColor != Color.Transparent && BorderWidth > 0)
        {
            using var pen = new Pen(BorderColor, BorderWidth);
            e.Graphics.DrawPath(pen, path);
        }
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
