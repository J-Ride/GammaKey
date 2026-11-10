namespace GammaKey.UI;

public enum UnsavedChoice { SaveAndApply, Discard }

// Two choices and no Cancel: either one completes the action that raised the dialog, so the
// user is never left in a dead end. Escape and the close box count as Discard changes.
public sealed class UnsavedChangesDialog : Form
{
    private const int ContentWidth = 388;
    private readonly Label _message = new() { AutoSize = true, UseMnemonic = false, Font = Theme.Label, ForeColor = Theme.TextPrimary, Margin = new Padding(0, 0, 0, Theme.Space4), MaximumSize = new Size(ContentWidth, 0) };

    public UnsavedChangesDialog(string profileName)
    {
        // Setting AutoScaleMode or ClientSize on a live form runs the DPI scale pass at once, on
        // an empty form. Suspend first so the pass runs once, over the whole tree, at ResumeLayout.
        SuspendLayout();
        Text = "Unsaved changes";
        WindowChrome.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        // The window wraps its content, so a long wrapped name and a move to a monitor at
        // another scale both keep the buttons in view.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _message.Text = $"{profileName} has unsaved changes.";

        var stack = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), Margin = Padding.Empty, BackColor = Theme.Background };
        Controls.Add(stack);
        stack.Controls.Add(_message);

        var buttons = new FlowLayoutPanel { AutoSize = false, Width = ContentWidth, Height = Theme.ControlHeight, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, BackColor = Theme.Background };
        var save = new GkButton("Save and Apply") { IsPrimary = true, Margin = Padding.Empty, DialogResult = DialogResult.OK };
        var discard = new GkButton("Discard changes") { Margin = new Padding(0, 0, Theme.Space2, 0), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(discard);
        stack.Controls.Add(buttons);

        AcceptButton = save;
        CancelButton = discard;
        Theme.Apply(this);
        ResumeLayout(true);
    }

    public string Message => _message.Text;

    public static UnsavedChoice ChoiceFrom(DialogResult result) => result == DialogResult.OK ? UnsavedChoice.SaveAndApply : UnsavedChoice.Discard;

    // A null owner (the main window is hidden in the tray) centres the dialog on the screen.
    public static UnsavedChoice Ask(IWin32Window? owner, string profileName)
    {
        using var dialog = new UnsavedChangesDialog(profileName);
        if (owner is null) dialog.StartPosition = FormStartPosition.CenterScreen;
        return ChoiceFrom(owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner));
    }
}
