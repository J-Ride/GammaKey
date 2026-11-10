namespace GammaKey.UI;

public enum UnsavedChoice { SaveAndApply, Discard }

// Two choices and no Cancel: either one completes the action that raised the dialog, so the
// user is never left in a dead end. Escape and the close box count as Discard changes.
public sealed class UnsavedChangesDialog : Form
{
    private readonly Label _message = new() { AutoSize = true, UseMnemonic = false, Font = Theme.Label, ForeColor = Theme.TextPrimary, Margin = new Padding(0, 0, 0, Theme.Space4), MaximumSize = new Size(380, 0) };

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
        ClientSize = new Size(420, 120);
        _message.Text = $"{profileName} has unsaved changes.";

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), BackColor = Theme.Background };
        Controls.Add(stack);
        stack.Controls.Add(_message);

        var buttons = new FlowLayoutPanel { AutoSize = false, Height = Theme.ControlHeight, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, BackColor = Theme.Background };
        var save = new Button { Text = "Save and Apply", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, DialogResult = DialogResult.OK };
        var discard = new Button { Text = "Discard changes", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(discard);
        stack.Controls.Add(buttons);
        // The button row stretches to the stack width; a TopDown FlowLayoutPanel does not do that itself.
        stack.Layout += (_, _) => buttons.Width = stack.ClientSize.Width - stack.Padding.Horizontal;

        AcceptButton = save;
        CancelButton = discard;
        Theme.Apply(this);
        Theme.StylePrimary(save);
        ResumeLayout(true);
        // The height follows the wrapped message so the buttons stay reachable for a long name.
        ClientSize = new Size(ClientSize.Width, buttons.Bottom + stack.Padding.Bottom);
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
