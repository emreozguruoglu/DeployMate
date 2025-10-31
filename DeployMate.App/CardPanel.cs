using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeployMate.App;

public sealed class CardPanel : Panel
{
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        using var path = CreateRoundRect(ClientRectangle, CornerRadius);
        Region = new Region(path);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var path = CreateRoundRect(ClientRectangle, CornerRadius);
        using var pen = new Pen(Color.FromArgb(200, 210, 218), 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}


