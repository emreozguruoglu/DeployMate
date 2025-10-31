using DeployMate.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DeployMate.App;

public sealed class Shell : Form
{
    private readonly IDeploymentEngine _engine;
    private readonly IConfigurationStore _config;
    private readonly DeployMate.Core.ILogger _log;

    private readonly FlowLayoutPanel _targetsPanel = new FlowLayoutPanel();
    private readonly Button _addButton = new Button();
    private readonly Dictionary<TargetId, CancellationTokenSource> _running = new();
    private readonly Dictionary<TargetId, bool> _selected = new();
    private bool _darkTheme = false;
    private TargetConfig[] _targets = Array.Empty<TargetConfig>();

    public Shell(IDeploymentEngine engine, IConfigurationStore config, DeployMate.Core.ILogger log)
    {
        _engine = engine;
        _config = config;
        _log = log;

        Text = "DeployMate";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        var topBar = BuildTopBar();
        Controls.Add(topBar);

        // Enable keyboard shortcuts
        KeyPreview = true;
        KeyDown += Shell_KeyDown;

        _targetsPanel.Dock = DockStyle.Fill;
        _targetsPanel.Padding = new Padding(12, 24, 12, 12);
        _targetsPanel.AutoScroll = true;
        _targetsPanel.WrapContents = true;
        _targetsPanel.FlowDirection = FlowDirection.LeftToRight;
        Controls.Add(_targetsPanel);

        Load += async (_, __) =>
        {
            _targets = await _config.LoadTargetsAsync(CancellationToken.None);
            if (_targets.Length == 0)
            {
                // Seed one example target (disabled) for first run
                _targets = new[]
                {
                    new TargetConfig
                    {
                        Name = "Demo SFTP",
                        Environment = "TEST",
                        Protocol = Protocol.Sftp,
                        Host = "sftp.example.local",
                        Port = 22,
                        RemotePath = "/var/www/app",
                        LocalDestination = "C\\builds\\MyApp\\Release",
                        Credential = new CredentialRef { Kind = "Dpapi", Key = "demo-sftp" },
                        Disabled = true
                    }
                };
                await _config.SaveTargetsAsync(_targets, CancellationToken.None);
            }
            RenderTargets();
        };
    }

    private Control BuildTopBar()
    {
        var menu = new MenuStrip { Dock = DockStyle.Top };
        var file = new ToolStripMenuItem("File");
        var miOpen = new ToolStripMenuItem("Open") { ShortcutKeys = Keys.Control | Keys.O };
        var miSave = new ToolStripMenuItem("Save") { ShortcutKeys = Keys.Control | Keys.S };
        var miSaveAs = new ToolStripMenuItem("Save As") { ShortcutKeys = Keys.Control | Keys.Shift | Keys.S };
        file.DropDownItems.AddRange(new ToolStripItem[] { miOpen, miSave, miSaveAs });

        var actions = new ToolStripMenuItem("Actions");
        var miAdd = new ToolStripMenuItem("+ Add") { ShortcutKeys = Keys.Control | Keys.N };
        var miStartSel = new ToolStripMenuItem("Start Selected") { ShortcutKeys = Keys.Control | Keys.F6 };
        var miStartAll = new ToolStripMenuItem("Start All") { ShortcutKeys = Keys.F5 };
        var miStopSel = new ToolStripMenuItem("Stop Selected") { ShortcutKeys = Keys.Control | Keys.F7 };
        var miStopAll = new ToolStripMenuItem("Stop All") { ShortcutKeys = Keys.Control | Keys.F5 };
        actions.DropDownItems.AddRange(new ToolStripItem[] { miAdd, new ToolStripSeparator(), miStartSel, miStartAll, new ToolStripSeparator(), miStopSel, miStopAll });

        var view = new ToolStripMenuItem("View");
        var miDark = new ToolStripMenuItem("Dark") { CheckOnClick = true, ShortcutKeys = Keys.Control | Keys.D };
        view.DropDownItems.Add(miDark);

        var tools = new ToolStripMenuItem("Tools");
        var miSettings = new ToolStripMenuItem("Settings") { ShortcutKeys = Keys.Control | Keys.Oemcomma };
        var miLogs = new ToolStripMenuItem("Logs") { ShortcutKeys = Keys.Control | Keys.L };
        tools.DropDownItems.AddRange(new ToolStripItem[] { miSettings, miLogs });

        menu.Items.AddRange(new ToolStripItem[] { file, actions, view, tools });

        miStartAll.Click += (_, __) => StartAll();
        miStopAll.Click += (_, __) => StopAll();
        miStartSel.Click += (_, __) => StartSelected();
        miStopSel.Click += (_, __) => StopSelected();
        miSettings.Click += (_, __) => { using var dlg = new SettingsDialog(_config); dlg.ShowDialog(this); };
        miLogs.Click += (_, __) => { using var dlg = new LogsDialog(); dlg.ShowDialog(this); };
        miOpen.Click += async (_, __) =>
        {
            using var ofd = new OpenFileDialog { Filter = "DeployMate Config (*.deploymate.json)|*.deploymate.json|JSON (*.json)|*.json" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                await _config.ImportAsync(ofd.FileName, default);
                _targets = await _config.LoadTargetsAsync(default);
                RenderTargets();
            }
        };
        miSave.Click += async (_, __) =>
        {
            // Save current store state to default location is already automatic; this forces re-save
            await _config.SaveTargetsAsync(_targets, default);
            var settings = await _config.LoadAppSettingsAsync(default);
            await _config.SaveAppSettingsAsync(settings, default);
            MessageBox.Show(this, "Saved.");
        };
        miSaveAs.Click += async (_, __) =>
        {
            using var sfd = new SaveFileDialog { Filter = "DeployMate Config (*.deploymate.json)|*.deploymate.json|JSON (*.json)|*.json", FileName = "DeployMate.export.deploymate.json" };
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                await _config.ExportAsync(sfd.FileName, default);
                MessageBox.Show(this, "Exported.");
            }
        };

        _addButton.Text = "+ Add";
        _addButton.Width = 90;
        _addButton.Height = 28;
        _addButton.Visible = false; // replaced by ToolStrip button
        _addButton.Click += async (_, __) =>
        {
            using var dlg = new TargetDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
            {
                var list = new List<TargetConfig>(_targets) { dlg.Result };
                _targets = list.ToArray();
                await _config.SaveTargetsAsync(_targets, CancellationToken.None);
                RenderTargets();
            }
        };
        miAdd.Click += async (_, __) =>
        {
            using var dlg = new TargetDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
            {
                var list = new List<TargetConfig>(_targets) { dlg.Result };
                _targets = list.ToArray();
                await _config.SaveTargetsAsync(_targets, CancellationToken.None);
                RenderTargets();
            }
        };
        miDark.CheckedChanged += (_, __) => { _darkTheme = miDark.Checked; ApplyTheme(); };

        Controls.Add(menu);
        return menu;
    }

    private void ApplyTheme()
    {
        var bg = _darkTheme ? Color.FromArgb(0x12, 0x14, 0x17) : Color.White;
        var fg = _darkTheme ? Color.FromArgb(0xED, 0xEF, 0xF2) : Color.FromArgb(0x0A, 0x0A, 0x0A);
        BackColor = bg;
        ForeColor = fg;
        foreach (Control c in Controls)
        {
            c.BackColor = _darkTheme ? Color.FromArgb(0x1A, 0x1D, 0x21) : Color.FromArgb(0xF7, 0xF9, 0xFC);
            c.ForeColor = fg;
        }
        foreach (Control card in _targetsPanel.Controls)
        {
            if (card is Panel p)
            {
                p.BackColor = _darkTheme ? Color.FromArgb(0x1E, 0x22, 0x27) : Color.White;
                p.ForeColor = fg;
            }
        }
        Invalidate(true);
    }

    private void RenderTargets()
    {
        _targetsPanel.Controls.Clear();
        foreach (var t in _targets)
        {
            _targetsPanel.Controls.Add(CreateTargetCard(t));
        }
    }

    private Control CreateTargetCard(TargetConfig target)
    {
        var card = new CardPanel
        {
            Width = 380,
            Height = 180,
            Margin = new Padding(12),
            BackColor = _darkTheme ? Color.FromArgb(0x1E, 0x22, 0x27) : Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8)
        };
        var select = new CheckBox { Width = 16, Height = 16, Location = new Point(12, 12), Checked = _selected.TryGetValue(target.Id, out var sel) && sel };
        select.CheckedChanged += (_, __) => { _selected[target.Id] = select.Checked; };
        var name = new Label { Text = target.Name + (target.Disabled ? " (Disabled)" : string.Empty), AutoSize = true, Location = new Point(32, 12) };
        var env = new Label { Text = target.Environment, AutoSize = true, Location = new Point(12, 34) };
        var src = new Label { Text = $"{target.Protocol}://{target.Host}:{target.Port}{target.RemotePath}", AutoSize = true, Location = new Point(12, 56) };
        var dest = new Label { Text = $"Local: {target.LocalDestination}", AutoSize = true, Location = new Point(12, 76) };
        var progress = new ProgressBar { Width = 350, Height = 14, Location = new Point(12, 100) };
        var status = new Label { Text = "Idle", AutoSize = true, Location = new Point(12, 120) };

        // Allow starting disabled targets in forced Dry Run for quick testing
        bool forceDryRun = target.Disabled;
        var start = new Button { Text = "Start", Width = 65, Height = 28, Location = new Point(12, 140), Enabled = true };
        var cancel = new Button { Text = "Cancel", Width = 65, Height = 28, Location = new Point(82, 140), Enabled = false };
        var logs = new Button { Text = "Logs", Width = 60, Height = 28, Location = new Point(152, 140) };
        var edit = new Button { Text = "Edit", Width = 55, Height = 28, Location = new Point(217, 140) };
        var remove = new Button { Text = "Remove", Width = 65, Height = 28, Location = new Point(277, 140) };

        // Apply professional button styling
        ApplyButtonStyling(start, Color.FromArgb(0x4C, 0xAF, 0x50)); // Green for start
        ApplyButtonStyling(cancel, Color.FromArgb(0xFF, 0x98, 0x00)); // Orange for cancel
        ApplyButtonStyling(logs, Color.FromArgb(0x21, 0x96, 0xF3)); // Blue for logs
        ApplyButtonStyling(edit, Color.FromArgb(0x9C, 0x27, 0xB0)); // Purple for edit
        ApplyButtonStyling(remove, Color.FromArgb(0xF4, 0x43, 0x36)); // Red for remove
        start.Click += async (_, __) =>
        {
            start.Enabled = false;
            cancel.Enabled = true;
            var cts = new CancellationTokenSource();
            _running[target.Id] = cts;
            var uiProgress = new Progress<DeployProgress>(p =>
            {
                status.Text = p.Status + ": " + p.Message;
                progress.Value = Math.Max(0, Math.Min(100, (int)p.Percent));
            });
            try
            {
                bool dryRun = forceDryRun || target.DefaultDryRun;
                await _engine.RunAsync(target, dryRun, uiProgress, cts.Token);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Deployment failed for {Target}", target.Name);
                MessageBox.Show(this, ex.Message, "Deployment Failed");
                status.Text = "Failed: " + ex.Message;
            }
            finally
            {
                _running.Remove(target.Id);
                start.Enabled = true;
                cancel.Enabled = false;
            }
        };
        cancel.Click += (_, __) =>
        {
            if (_running.TryGetValue(target.Id, out var cts))
            {
                cts.Cancel();
            }
        };

        edit.Click += async (_, __) =>
        {
            using var dlg = new TargetDialog(target);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
            {
                var list = new List<TargetConfig>(_targets);
                int idx = list.FindIndex(x => x.Id.Equals(target.Id));
                if (idx >= 0) list[idx] = dlg.Result;
                _targets = list.ToArray();
                await _config.SaveTargetsAsync(_targets, CancellationToken.None);
                RenderTargets();
            }
        };
        remove.Click += async (_, __) =>
        {
            if (MessageBox.Show(this, $"Remove target '{target.Name}'?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var list = new List<TargetConfig>(_targets);
                list.RemoveAll(x => x.Id.Equals(target.Id));
                _targets = list.ToArray();
                await _config.SaveTargetsAsync(_targets, CancellationToken.None);
                RenderTargets();
            }
        };
        logs.Click += (_, __) =>
        {
            using var logsDlg = new TargetLogsDialog(target);
            logsDlg.ShowDialog(this);
        };
        card.DoubleClick += (_, __) => edit.PerformClick();

        card.Controls.Add(select);
        card.Controls.Add(name);
        card.Controls.Add(env);
        card.Controls.Add(src);
        card.Controls.Add(dest);
        card.Controls.Add(progress);
        card.Controls.Add(status);
        if (forceDryRun)
        {
            var tip = new ToolTip();
            tip.SetToolTip(start, "Target is disabled; running as Dry Run only.");
        }
        card.Controls.Add(start);
        card.Controls.Add(cancel);
        card.Controls.Add(logs);
        card.Controls.Add(edit);
        card.Controls.Add(remove);
        return card;
    }

    private void StartAll()
    {
        foreach (Control c in _targetsPanel.Controls)
        {
            if (c is Panel card)
            {
                foreach (Control child in card.Controls)
                {
                    if (child is Button b && b.Text == "Start" && b.Enabled)
                    {
                        b.PerformClick();
                    }
                }
            }
        }
    }

    private void StopAll()
    {
        foreach (var kv in _running)
        {
            kv.Value.Cancel();
        }
    }

    private void StartSelected()
    {
        foreach (Control c in _targetsPanel.Controls)
        {
            if (c is Panel card)
            {
                bool selected = false;
                Button? startBtn = null;
                foreach (Control child in card.Controls)
                {
                    if (child is CheckBox cb) selected = cb.Checked;
                    if (child is Button b && b.Text == "Start") startBtn = b;
                }
                if (selected && startBtn != null && startBtn.Enabled) startBtn.PerformClick();
            }
        }
    }

    private void StopSelected()
    {
        foreach (var sel in _selected)
        {
            if (sel.Value && _running.TryGetValue(sel.Key, out var cts)) cts.Cancel();
        }
    }

    private void ApplyButtonStyling(Button button, Color backgroundColor)
    {
        button.BackColor = backgroundColor;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;
        
        // Add hover effects
        button.MouseEnter += (_, __) => 
        {
            var hoverColor = Color.FromArgb(
                Math.Min(255, Math.Max(0, backgroundColor.R + 20)),
                Math.Min(255, Math.Max(0, backgroundColor.G + 20)),
                Math.Min(255, Math.Max(0, backgroundColor.B + 20))
            );
            button.BackColor = hoverColor;
        };
        button.MouseLeave += (_, __) => button.BackColor = backgroundColor;
    }

    private async void Shell_KeyDown(object? sender, KeyEventArgs e)
    {
        // Handle additional keyboard shortcuts that might not be automatically handled
        switch (e.KeyCode)
        {
            case Keys.F5:
                if (e.Control)
                {
                    StopAll();
                }
                else
                {
                    StartAll();
                }
                e.Handled = true;
                break;
            case Keys.F6:
                if (e.Control)
                {
                    StartSelected();
                    e.Handled = true;
                }
                break;
            case Keys.F7:
                if (e.Control)
                {
                    StopSelected();
                    e.Handled = true;
                }
                break;
            case Keys.D:
                if (e.Control)
                {
                    // Toggle dark theme
                    _darkTheme = !_darkTheme;
                    ApplyTheme();
                    e.Handled = true;
                }
                break;
            case Keys.N:
                if (e.Control)
                {
                    // Add new target
                    using var dlg = new TargetDialog();
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
                    {
                        var list = new List<TargetConfig>(_targets) { dlg.Result };
                        _targets = list.ToArray();
                        await _config.SaveTargetsAsync(_targets, CancellationToken.None);
                        RenderTargets();
                    }
                    e.Handled = true;
                }
                break;
            case Keys.Oemcomma:
                if (e.Control)
                {
                    // Open settings
                    using var settingsDlg = new SettingsDialog(_config);
                    settingsDlg.ShowDialog(this);
                    e.Handled = true;
                }
                break;
            case Keys.L:
                if (e.Control)
                {
                    // Open logs
                    using var logsDlg = new LogsDialog();
                    logsDlg.ShowDialog(this);
                    e.Handled = true;
                }
                break;
        }
    }
}


