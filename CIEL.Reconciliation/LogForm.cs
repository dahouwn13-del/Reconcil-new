using CIEL.Reconciliation.Logging;
using System.Text.RegularExpressions;

namespace CIEL.Reconciliation;

public sealed class LogForm : Form
{
    private readonly RichTextBox _workLog = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(15, 23, 42),
        ForeColor = Color.FromArgb(226, 232, 240),
        Font = new Font("Consolas", 9),
        DetectUrls = false
    };

    private readonly Label _logStatus = new()
    {
        Text = "Ready",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(51, 65, 85),
        Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold)
    };

    private readonly ProgressBar _logProgress = new()
    {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 100,
        Value = 0,
        Style = ProgressBarStyle.Continuous
    };

    public LogForm()
    {
        Text = "CIEL Reconciliation — Work Log";
        Width = 980;
        Height = 620;
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);
        ShowInTaskbar = true;

        Controls.Add(BuildLayout());
        Logger.EntryWritten += OnLogEntryWritten;
        FormClosed += (_, _) => Logger.EntryWritten -= OnLogEntryWritten;

        LoadExistingEntries();
    }

    public void ResetForNewRun()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ResetForNewRun));
            return;
        }

        _workLog.Clear();
        _logProgress.Value = 0;
        _logStatus.Text = "Starting...";
    }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(8, 5, 8, 5)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        header.Controls.Add(new Label
        {
            Text = "WORK LOG — LIVE ENGINE ACTIVITY",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85)
        }, 0, 0);

        var copy = CreateButton("COPY LOG");
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_workLog.Text))
                Clipboard.SetText(_workLog.Text);
        };
        header.Controls.Add(copy, 1, 0);

        var clear = CreateButton("CLEAR LOG");
        clear.Click += (_, _) =>
        {
            Logger.Clear();
            _workLog.Clear();
            _logProgress.Value = 0;
            _logStatus.Text = "Ready";
        };
        header.Controls.Add(clear, 2, 0);

        var progressRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8, 8, 8, 7),
            BackColor = Color.White
        };
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressRow.Controls.Add(_logStatus, 0, 0);
        progressRow.Controls.Add(_logProgress, 1, 0);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(progressRow, 0, 1);
        layout.Controls.Add(_workLog, 0, 2);
        return layout;
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 86, 122),
            Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 4, 0)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        return button;
    }

    private void LoadExistingEntries()
    {
        foreach (var entry in Logger.Entries)
            AppendEntry(entry);
    }

    private void OnLogEntryWritten(LogEntry entry)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<LogEntry>(OnLogEntryWritten), entry);
            return;
        }

        AppendEntry(entry);
    }

    private void AppendEntry(LogEntry entry)
    {
        var levelText = entry.Level.ToString().ToUpperInvariant().PadRight(7);
        var details = string.Empty;
        if (!string.IsNullOrWhiteSpace(entry.ReservationNumber)) details += $" | #{entry.ReservationNumber}";
        if (!string.IsNullOrWhiteSpace(entry.GuestName)) details += $" | {entry.GuestName}";
        var line = $"{entry.Timestamp:HH:mm:ss}  {levelText}  {entry.Message}{details}{Environment.NewLine}";

        _workLog.SelectionStart = _workLog.TextLength;
        _workLog.SelectionLength = 0;
        _workLog.SelectionColor = entry.Level switch
        {
            LogLevel.Success => Color.FromArgb(74, 222, 128),
            LogLevel.Warning => Color.FromArgb(251, 191, 36),
            LogLevel.Error => Color.FromArgb(248, 113, 113),
            _ => Color.FromArgb(147, 197, 253)
        };
        _workLog.AppendText(line);
        _workLog.SelectionColor = _workLog.ForeColor;
        _workLog.SelectionStart = _workLog.TextLength;
        _workLog.ScrollToCaret();

        var match = Regex.Match(entry.Message, @"Matching\s+(\d+)\s+of\s+(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var current) && int.TryParse(match.Groups[2].Value, out var total) && total > 0)
        {
            _logProgress.Value = Math.Clamp((int)Math.Round(current * 100d / total), 0, 100);
            _logStatus.Text = $"Matching {current} of {total} ({_logProgress.Value}%)";
        }
        else if (entry.Level == LogLevel.Error)
        {
            _logStatus.Text = "Stopped with an error";
        }
        else if (entry.Message.StartsWith("Engine completed", StringComparison.OrdinalIgnoreCase))
        {
            _logProgress.Value = 100;
            _logStatus.Text = "Completed (100%)";
        }
        else if (entry.Message.Contains("Reading", StringComparison.OrdinalIgnoreCase) || entry.Message.Contains("Starting", StringComparison.OrdinalIgnoreCase))
        {
            _logStatus.Text = entry.Message;
        }
    }
}
