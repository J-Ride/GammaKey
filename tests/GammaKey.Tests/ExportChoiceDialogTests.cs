using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class ExportChoiceDialogTests
{
    [Fact]
    public void KeyPreviewIsOnSoEscapeReachesTheDialog()
    {
        using var dialog = new ExportChoiceDialog(hasSelection: true);

        Assert.True(dialog.KeyPreview);
    }
}
