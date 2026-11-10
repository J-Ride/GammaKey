namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

// ComboBox keeps its popup and keyboard handling; this paints the closed box and the rows.
public sealed class GkDropdown : ComboBox
{
    private const int WmPaint = 0x000F;

    public GkDropdown()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        // The form's scale pass does not touch ItemHeight, so set it in device pixels here.
        ItemHeight = Theme.Scale(Theme.ControlHeight - 6, DeviceDpi);
        BackColor = Theme.Surface;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Label;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var back = new SolidBrush(selected && inList ? Theme.Raised : Theme.Surface)) e.Graphics.FillRectangle(back, e.Bounds);
        if (selected && inList)
        {
            using var bar = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, 2, e.Bounds.Height);
        }
        var text = new Rectangle(e.Bounds.Left + Theme.Space2, e.Bounds.Top, e.Bounds.Width - Theme.Space2, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString(), Theme.Label, text, Enabled ? Theme.TextPrimary : Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // The closed box is painted by the system after OnDrawItem, so repaint its border and arrow
    // once WM_PAINT has finished.
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WmPaint) return;
        using var g = Graphics.FromHwnd(Handle);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        // The system drop-down button is scroll-bar wide (it grows with DPI), so cover all of it.
        int buttonWidth = SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi) + 2;
        var arrowBox = new Rectangle(Width - buttonWidth, 0, buttonWidth, Height);
        using (var back = new SolidBrush(Theme.Surface)) g.FillRectangle(back, arrowBox);
        using (var path = Theme.RoundedPath(r, Theme.Radius))
        {
            // Square corners outside the rounded border take the parent colour.
            var state = g.Save();
            g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Exclude);
            using (var outside = new SolidBrush(Parent?.BackColor ?? Theme.Background)) g.FillRectangle(outside, ClientRectangle);
            g.Restore(state);
            using var pen = new Pen(Focused ? Theme.Accent : Theme.Line);
            g.DrawPath(pen, path);
        }
        using var ap = new Pen(Enabled ? Theme.Accent : Theme.TextDim, 1.5f);
        float cx = arrowBox.Left + arrowBox.Width / 2f;
        float cy = Height / 2f - 1;
        g.DrawLines(ap, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
    }
}
