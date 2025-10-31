using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeployMate.App;

public sealed class LogsDialog : Form
{
    private readonly ListBox _files = new ListBox();
    private readonly RichTextBox _viewer = new RichTextBox();
    private readonly Button _refreshButton = new Button();
    private readonly Button _clearButton = new Button();
    private readonly ComboBox _levelFilter = new ComboBox();
    private readonly System.Windows.Forms.Timer _refreshTimer = new System.Windows.Forms.Timer();
    private readonly string _logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeployMate", "logs");

    public LogsDialog()
    {
        InitializeComponent();
        SetupStyling();
        Load += (_, __) => RefreshList();
        _files.SelectedIndexChanged += async (_, __) => await LoadSelectedAsync();
        StartAutoRefresh();
    }

    private void InitializeComponent()
    {
        Text = "DeployMate Logs";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(800, 500);

        // Main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(8)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Top toolbar
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0)
        };

        _refreshButton.Text = "🔄 Refresh";
        _refreshButton.Size = new Size(80, 28);
        _refreshButton.Click += (_, __) => RefreshList();

        _clearButton.Text = "🗑️ Clear";
        _clearButton.Size = new Size(80, 28);
        _clearButton.Click += (_, __) => ClearLogs();

        _levelFilter.Items.AddRange(new[] { "All", "Information", "Warning", "Error", "Debug" });
        _levelFilter.SelectedIndex = 0;
        _levelFilter.Size = new Size(100, 28);
        _levelFilter.SelectedIndexChanged += (_, __) => FilterLogs();

        var titleLabel = new Label
        {
            Text = "DeployMate Application Logs",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        toolbar.Controls.AddRange(new Control[] { titleLabel, new Label { Text = " | " }, _refreshButton, _clearButton, new Label { Text = " | Level:" }, _levelFilter });

        // Log files list
        _files.Dock = DockStyle.Fill;
        _files.Font = new Font("Consolas", 8F);

        // Log viewer
        _viewer.Dock = DockStyle.Fill;
        _viewer.Font = new Font("Consolas", 9F);
        _viewer.ReadOnly = true;
        _viewer.BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
        _viewer.ForeColor = Color.FromArgb(0xD4, 0xD4, 0xD4);
        _viewer.ScrollBars = RichTextBoxScrollBars.Vertical;

        // Layout
        mainPanel.Controls.Add(toolbar, 0, 0);
        mainPanel.SetColumnSpan(toolbar, 2);
        mainPanel.Controls.Add(_files, 0, 1);
        mainPanel.Controls.Add(_viewer, 1, 1);

        Controls.Add(mainPanel);
    }

    private void SetupStyling()
    {
        BackColor = Color.FromArgb(0x2D, 0x2D, 0x30);
        ForeColor = Color.FromArgb(0xF1, 0xF1, 0xF1);

        _files.BackColor = Color.FromArgb(0x3C, 0x3C, 0x3C);
        _files.ForeColor = Color.FromArgb(0xF1, 0xF1, 0xF1);
        _files.BorderStyle = BorderStyle.FixedSingle;

        _refreshButton.BackColor = Color.FromArgb(0x0E, 0x63, 0x9C);
        _refreshButton.ForeColor = Color.White;
        _refreshButton.FlatStyle = FlatStyle.Flat;
        _refreshButton.FlatAppearance.BorderSize = 0;

        _clearButton.BackColor = Color.FromArgb(0xD8, 0x3C, 0x37);
        _clearButton.ForeColor = Color.White;
        _clearButton.FlatStyle = FlatStyle.Flat;
        _clearButton.FlatAppearance.BorderSize = 0;

        _levelFilter.BackColor = Color.FromArgb(0x3C, 0x3C, 0x3C);
        _levelFilter.ForeColor = Color.FromArgb(0xF1, 0xF1, 0xF1);
        _levelFilter.FlatStyle = FlatStyle.Flat;
    }

    private void RefreshList()
    {
        try
    {
        Directory.CreateDirectory(_logsDir);
            var logFiles = Directory.GetFiles(_logsDir, "log-*.txt")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Select(f => new FileInfo(f))
                .ToArray();

        _files.Items.Clear();
            foreach (var file in logFiles)
            {
                var displayName = $"{file.Name} ({file.LastWriteTime:yyyy-MM-dd HH:mm})";
                _files.Items.Add(new LogFileItem { DisplayName = displayName, FilePath = file.FullName });
            }

            if (_files.Items.Count > 0)
            {
                _files.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading log files: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadSelectedAsync()
    {
        if (_files.SelectedItem is LogFileItem item && File.Exists(item.FilePath))
        {
            try
            {
                using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, Encoding.UTF8);
                var content = await sr.ReadToEndAsync();
                
                _viewer.Clear();
                AppendColoredLogs(content);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading log file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void AppendColoredLogs(string content)
    {
        var lines = content.Split('\n');
        var selectedLevel = _levelFilter.SelectedItem?.ToString() ?? "All";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var color = GetLogLevelColor(line);
            var level = ExtractLogLevel(line);

            // Apply level filtering
            if (selectedLevel != "All" && !level.Contains(selectedLevel, StringComparison.OrdinalIgnoreCase))
                continue;

            _viewer.SelectionStart = _viewer.TextLength;
            _viewer.SelectionLength = 0;
            _viewer.SelectionColor = color;
            _viewer.AppendText(line + Environment.NewLine);
        }

        _viewer.ScrollToCaret();
    }

    private Color GetLogLevelColor(string line)
    {
        if (line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) || 
            line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))
            return Color.FromArgb(0xFF, 0x6B, 0x6B); // Red
        if (line.Contains("[WARN]", StringComparison.OrdinalIgnoreCase) || 
            line.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase))
            return Color.FromArgb(0xFF, 0xD9, 0x3D); // Yellow
        if (line.Contains("[INF]", StringComparison.OrdinalIgnoreCase) || 
            line.Contains("[INFO]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("[INFORMATION]", StringComparison.OrdinalIgnoreCase))
            return Color.FromArgb(0x4C, 0xAF, 0x50); // Green
        if (line.Contains("[DBG]", StringComparison.OrdinalIgnoreCase) || 
            line.Contains("[DEBUG]", StringComparison.OrdinalIgnoreCase))
            return Color.FromArgb(0x9C, 0x27, 0xB0); // Purple

        return Color.FromArgb(0xD4, 0xD4, 0xD4); // Default light gray
    }

    private string ExtractLogLevel(string line)
    {
        var patterns = new[] { "[ERR]", "[ERROR]", "[WARN]", "[WARNING]", "[INF]", "[INFO]", "[INFORMATION]", "[DBG]", "[DEBUG]" };
        foreach (var pattern in patterns)
        {
            if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return pattern;
        }
        return "UNKNOWN";
    }

    private void FilterLogs()
    {
        if (_files.SelectedItem is LogFileItem item && File.Exists(item.FilePath))
        {
            _ = LoadSelectedAsync();
        }
    }

    private void ClearLogs()
    {
        if (MessageBox.Show("Are you sure you want to clear all log files?", "Confirm Clear", 
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                var logFiles = Directory.GetFiles(_logsDir, "log-*.txt");
                foreach (var file in logFiles)
                {
                    File.Delete(file);
                }
                RefreshList();
                _viewer.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void StartAutoRefresh()
    {
        _refreshTimer.Interval = 5000; // Refresh every 5 seconds
        _refreshTimer.Tick += (_, __) => RefreshList();
        _refreshTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    private class LogFileItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }
}


