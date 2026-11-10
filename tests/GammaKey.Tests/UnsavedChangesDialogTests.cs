using System.Windows.Forms;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class UnsavedChangesDialogTests
{
    [Fact]
    public void OkMeansSaveAndApply()
    {
        Assert.Equal(UnsavedChoice.SaveAndApply, UnsavedChangesDialog.ChoiceFrom(DialogResult.OK));
    }

    [Fact]
    public void CancelMeansDiscard()
    {
        // Escape and the close box both end ShowDialog with Cancel.
        Assert.Equal(UnsavedChoice.Discard, UnsavedChangesDialog.ChoiceFrom(DialogResult.Cancel));
    }

    [Fact]
    public void DialogNamesTheProfileInItsMessage()
    {
        using var dialog = new UnsavedChangesDialog("Cave");

        Assert.Equal("Cave has unsaved changes.", dialog.Message);
    }
}
