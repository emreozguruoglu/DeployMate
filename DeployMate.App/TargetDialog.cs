using DeployMate.Core;
using System;
using System.Drawing;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace DeployMate.App;

public sealed class TargetDialog : Form
{
    private readonly TextBox _txtName = new TextBox();
    private readonly ComboBox _cmbEnv = new ComboBox();
    private readonly ComboBox _cmbProtocol = new ComboBox();
    private readonly TextBox _txtHost = new TextBox();
    private readonly NumericUpDown _numPort = new NumericUpDown();
    private readonly TextBox _txtRemote = new TextBox();
    private readonly TextBox _txtLocal = new TextBox();
    private readonly TextBox _txtCredKey = new TextBox();
    private readonly CheckBox _chkDryRun = new CheckBox();
    private readonly CheckBox _chkDisabled = new CheckBox();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TargetConfig? Result { get; private set; }

    public TargetDialog(TargetConfig? existing = null)
    {
        Text = existing == null ? "Add Target" : "Edit Target";
        Width = 560;
        Height = 440;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 10, AutoSize = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        void AddRow(string label, Control ctrl)
        {
            var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) };
            ctrl.Dock = DockStyle.Fill;
            grid.Controls.Add(lbl);
            grid.Controls.Add(ctrl);
        }

        _cmbEnv.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbEnv.Items.AddRange(new[] { "DEV", "TEST", "PREPROD", "PROD" });
        _cmbProtocol.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbProtocol.Items.AddRange(Enum.GetNames(typeof(Protocol)));
        _numPort.Minimum = 1; _numPort.Maximum = 65535; _numPort.Value = 22;
        _chkDryRun.Text = "Dry Run by default";
        _chkDisabled.Text = "Disabled";

        AddRow("Name", _txtName);
        AddRow("Environment", _cmbEnv);
        AddRow("Protocol", _cmbProtocol);
        AddRow("Host", _txtHost);
        AddRow("Port", _numPort);
        AddRow("Remote Path", _txtRemote);
        AddRow("Local Destination", _txtLocal);
        AddRow("Credential Key", _txtCredKey);
        AddRow("Options", new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Controls = { _chkDryRun, _chkDisabled }, AutoSize = true });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(12) };
        var btnOk = new Button { Text = "Save", Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(btnOk);
        buttons.Controls.Add(btnCancel);

        Controls.Add(grid);
        Controls.Add(buttons);

        if (existing != null)
        {
            _txtName.Text = existing.Name;
            _cmbEnv.SelectedItem = existing.Environment;
            _cmbProtocol.SelectedItem = existing.Protocol.ToString();
            _txtHost.Text = existing.Host;
            _numPort.Value = existing.Port;
            _txtRemote.Text = existing.RemotePath;
            _txtLocal.Text = existing.LocalDestination;
            _txtCredKey.Text = existing.Credential.Key;
            _chkDryRun.Checked = existing.DefaultDryRun;
            _chkDisabled.Checked = existing.Disabled;
        }

        btnOk.Click += (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text)) { MessageBox.Show(this, "Name is required"); DialogResult = DialogResult.None; return; }
            if (_cmbEnv.SelectedItem == null) { MessageBox.Show(this, "Environment is required"); DialogResult = DialogResult.None; return; }
            if (_cmbProtocol.SelectedItem == null) { MessageBox.Show(this, "Protocol is required"); DialogResult = DialogResult.None; return; }

            var cfg = existing ?? new TargetConfig();
            cfg = new TargetConfig
            {
                Id = existing?.Id ?? TargetId.New(),
                Name = _txtName.Text.Trim(),
                Environment = _cmbEnv.SelectedItem!.ToString()!,
                Protocol = Enum.Parse<Protocol>(_cmbProtocol.SelectedItem!.ToString()!),
                Host = _txtHost.Text.Trim(),
                Port = (int)_numPort.Value,
                RemotePath = _txtRemote.Text.Trim(),
                LocalDestination = _txtLocal.Text.Trim(),
                Credential = new CredentialRef { Kind = "Dpapi", Key = _txtCredKey.Text.Trim() },
                Transfer = existing?.Transfer ?? new TransferOptions(),
                PreDeploy = existing?.PreDeploy ?? new HookSet(),
                PostDeploy = existing?.PostDeploy ?? new HookSet(),
                DefaultDryRun = _chkDryRun.Checked,
                Disabled = _chkDisabled.Checked
            };
            Result = cfg;
        };
    }
}


