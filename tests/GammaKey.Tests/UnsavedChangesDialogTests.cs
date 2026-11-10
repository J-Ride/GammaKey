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

    [Fact]
    public void LongNameMakesTheDialogTaller()
    {
        using var shortName = new UnsavedChangesDialog("Cave");
        using var longName = new UnsavedChangesDialog(new string('x', 40) + " " + new string('y', 40) + " " + new string('z', 40));

        Assert.True(longName.ClientSize.Height > shortName.ClientSize.Height);
    }
}
