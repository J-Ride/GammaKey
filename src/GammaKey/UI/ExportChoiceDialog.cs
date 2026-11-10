namespace GammaKey.UI;

public enum ExportScope { Selected, All }

public sealed class ExportChoiceDialog : Form
{
    private ExportScope? _choice;

    public ExportChoiceDialog(bool hasSelection)
    {
        // Setting AutoScaleMode or ClientSize on a live form runs the DPI scale pass at once, on
        // an empty form. Suspend first so the pass runs once, over the whole tree, at ResumeLayout.
        SuspendLayout();
        Text = "Export";
        KeyPreview = true;
        WindowChrome.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        // The window wraps its content, so the buttons stay in view at any DPI scale.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var stack = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), Margin = Padding.Empty, BackColor = Theme.Background };
        Controls.Add(stack);
        stack.Controls.Add(new Label { Text = "What do you want to export?", AutoSize = true, Font = Theme.Label, ForeColor = Theme.TextPrimary, Margin = new Padding(0, 0, 0, Theme.Space4) });

        var buttons = new FlowLayoutPanel { AutoSize = false, Width = 388, Height = Theme.ControlHeight, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, BackColor = Theme.Background };
        var selected = new GkButton("This profile only") { IsPrimary = hasSelection, Margin = Padding.Empty, Enabled = hasSelection };
        var all = new GkButton("All profiles") { IsPrimary = !hasSelection, Margin = new Padding(0, 0, Theme.Space2, 0) };
        selected.Click += (_, _) => { _choice = ExportScope.Selected; DialogResult = DialogResult.OK; };
        all.Click += (_, _) => { _choice = ExportScope.All; DialogResult = DialogResult.OK; };
        buttons.Controls.Add(selected);
        buttons.Controls.Add(all);
        stack.Controls.Add(buttons);

        // The single export is the default when a profile is selected; otherwise All is.
        AcceptButton = hasSelection ? selected : all;
        Theme.Apply(this);
        ResumeLayout(true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // Null when the user pressed Escape or the close box: nothing is exported.
    public static ExportScope? Ask(IWin32Window owner, bool hasSelection)
    {
        using var dialog = new ExportChoiceDialog(hasSelection);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._choice : null;
    }
}
