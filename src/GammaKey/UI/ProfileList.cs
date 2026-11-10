using GammaKey.Models;

namespace GammaKey.UI;

public sealed class ProfileList : ListBox
{
    public ProfileList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        // The form's scale pass does not touch ItemHeight, so set it in device pixels here.
        ItemHeight = RowHeightFor(DeviceDpi);
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Theme.Panel;
        ForeColor = Theme.TextPrimary;
        Margin = Padding.Empty;
    }

    // Two text lines plus a Space2 band above and below, so the summary never clips at any scale.
    public static int RowHeightFor(float dpi) => NameLineHeight(dpi) + SummaryLineHeight(dpi) + 2 * Theme.Scale(Theme.Space2, dpi);

    private static int NameLineHeight(float dpi) => (int)Math.Ceiling(Theme.Label.GetHeight(dpi));
    private static int SummaryLineHeight(float dpi) => (int)Math.Ceiling(Theme.Caption.GetHeight(dpi));

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ItemHeight = RowHeightFor(DeviceDpi);
    }

    public void Bind(IReadOnlyList<Profile> profiles, int keepIndex)
    {
        BeginUpdate();
        Items.Clear();
        foreach (var p in profiles) Items.Add(p);
        if (keepIndex >= 0 && keepIndex < Items.Count) SelectedIndex = keepIndex;
        EndUpdate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || Items[e.Index] is not Profile profile) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int S(int v) => Theme.Scale(v, DeviceDpi);
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(Theme.Panel)) g.FillRectangle(back, e.Bounds);
        var row = new Rectangle(e.Bounds.Left + S(Theme.Space2), e.Bounds.Top + 1, e.Bounds.Width - 2 * S(Theme.Space2) - 1, e.Bounds.Height - 3);
        if (selected)
        {
            using var fill = new SolidBrush(Theme.Surface);
            Theme.FillRounded(g, fill, row, Theme.Radius);
            using var border = new Pen(Theme.LineStrong);
            Theme.DrawRounded(g, border, row, Theme.Radius);
            using var bar = new SolidBrush(Theme.Accent);
            g.FillRectangle(bar, row.Left, row.Top + 3, 2, row.Height - 6);
        }

        var parts = GammaKey.UI.Format.HotkeyParts(profile.Hotkey);
        int chipsWidth = 0;
        foreach (var part in parts) chipsWidth += TextRenderer.MeasureText(g, part, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width + S(12) + S(Theme.Space1);
        int textRight = row.Right - S(Theme.Space2) - chipsWidth;

        // Both lines are centred as a block inside the row so the Space2 bands stay equal.
        int nameHeight = NameLineHeight(DeviceDpi);
        int summaryHeight = SummaryLineHeight(DeviceDpi);
        int textTop = row.Top + (row.Height - nameHeight - summaryHeight) / 2;
        var nameRect = new Rectangle(row.Left + S(Theme.Space3), textTop, textRight - row.Left - S(Theme.Space3), nameHeight);
        TextRenderer.DrawText(g, profile.Name, Theme.Label, nameRect, Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var subRect = new Rectangle(nameRect.Left, nameRect.Bottom, nameRect.Width, summaryHeight);
        TextRenderer.DrawText(g, GammaKey.UI.Format.ProfileSummary(profile), Theme.Caption, subRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        // Chips centre on the row; DrawChip centres on the line it is given.
        int x = textRight;
        foreach (var part in parts)
            x = GkHotkeyBox.DrawChip(g, part, x, row.Top, row.Height, DeviceDpi, selected ? Theme.Accent : Theme.TextMuted, selected ? Theme.AccentDim : Theme.Line) + S(Theme.Space1);
    }
}
