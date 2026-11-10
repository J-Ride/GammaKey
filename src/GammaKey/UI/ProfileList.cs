using GammaKey.Models;

namespace GammaKey.UI;

public sealed class ProfileList : ListBox
{
    public const int RowHeight = 44;

    public ProfileList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = RowHeight;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Theme.Panel;
        ForeColor = Theme.TextPrimary;
        Margin = Padding.Empty;
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
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(Theme.Panel)) g.FillRectangle(back, e.Bounds);
        var row = new Rectangle(e.Bounds.Left + Theme.Space2, e.Bounds.Top + 1, e.Bounds.Width - 2 * Theme.Space2 - 1, e.Bounds.Height - 3);
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
        foreach (var part in parts) chipsWidth += TextRenderer.MeasureText(g, part, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width + 12 + Theme.Space1;
        int textRight = row.Right - Theme.Space2 - chipsWidth;

        var nameRect = new Rectangle(row.Left + Theme.Space3, row.Top + 5, textRight - row.Left - Theme.Space3, 18);
        TextRenderer.DrawText(g, profile.Name, Theme.Label, nameRect, Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var subRect = new Rectangle(nameRect.Left, nameRect.Bottom, nameRect.Width, 14);
        TextRenderer.DrawText(g, GammaKey.UI.Format.ProfileSummary(profile), Theme.Caption, subRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        // Chips sit on the row's vertical centre; DrawChip assumes a ControlHeight-tall line, so offset.
        int x = textRight;
        var state = g.Save();
        g.TranslateTransform(0, row.Top + (row.Height - Theme.ControlHeight) / 2f);
        foreach (var part in parts)
            x = GkHotkeyBox.DrawChip(g, part, x, selected ? Theme.Accent : Theme.TextMuted, selected ? Theme.AccentDim : Theme.Line) + Theme.Space1;
        g.Restore(state);
    }
}
