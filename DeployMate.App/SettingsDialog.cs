using DeployMate.Core;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeployMate.App;

public sealed class SettingsDialog : Form
{
    private readonly IConfigurationStore _config;
    private readonly TextBox _txtTimeout = new TextBox();
    private readonly NumericUpDown _numRetry = new NumericUpDown();
    private readonly TextBox _txtExclusions = new TextBox();
    private readonly NumericUpDown _numRetention = new NumericUpDown();
    private AppSettings _settings = new AppSettings();

    public SettingsDialog(IConfigurationStore config)
    {
        _config = config;
        Text = "Settings";
        Width = 520;
        Height = 360;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

        void Row(string label, Control c)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) });
            c.Dock = DockStyle.Fill; grid.Controls.Add(c);
        }

        _numRetry.Minimum = 1; _numRetry.Maximum = 10; _numRetry.Value = 3;
        _numRetention.Minimum = 1; _numRetention.Maximum = 90; _numRetention.Value = 14;
        Row("Default Timeout (hh:mm:ss)", _txtTimeout);
        Row("Default Retry Attempts", _numRetry);
        Row("Default Exclusions (comma)", _txtExclusions);
        Row("Log Retention (days)", _numRetention);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(12) };
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel);

        Controls.Add(grid);
        Controls.Add(buttons);

        Load += async (_, __) =>
        {
            _settings = await _config.LoadAppSettingsAsync(default);
            _txtTimeout.Text = _settings.DefaultTimeout.ToString();
            _numRetry.Value = _settings.DefaultRetry.MaxAttempts;
            _txtExclusions.Text = string.Join(",", _settings.DefaultExclusions);
            _numRetention.Value = _settings.LogRetentionDays;
        };

        save.Click += async (_, __) =>
        {
            if (!TimeSpan.TryParse(_txtTimeout.Text, out var ts)) { MessageBox.Show(this, "Invalid timeout"); DialogResult = DialogResult.None; return; }
            _settings.DefaultTimeout = ts;
            _settings.DefaultRetry.MaxAttempts = (int)_numRetry.Value;
            _settings.DefaultExclusions = _txtExclusions.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _settings.LogRetentionDays = (int)_numRetention.Value;
            await _config.SaveAppSettingsAsync(_settings, default);
        };
    }
}


