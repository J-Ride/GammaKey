using System.Windows.Forms;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkButtonTests
{
    [Fact]
    public void ShortTextStillGetsTheMinimumWidth()
    {
        using var button = new GkButton("OK");

        Assert.True(button.Width >= Theme.Scale(72, button.DeviceDpi));
    }

    [Fact]
    public void LongerTextMakesAWiderButton()
    {
        using var shortOne = new GkButton("Close");
        using var longOne = new GkButton("Open profiles folder");

        Assert.True(longOne.Width > shortOne.Width);
    }

    [Fact]
    public void HeightIsTheLogicalControlHeightForTheFormsScalePass()
    {
        using var button = new GkButton("Apply");

        Assert.Equal(Theme.ControlHeight, button.Height);
    }

    [Fact]
    public void ClickCarriesItsDialogResultToTheForm()
    {
        using var form = new Form();
        var button = new GkButton("Save") { DialogResult = DialogResult.OK };
        form.Controls.Add(button);

        button.PerformClick();

        Assert.Equal(DialogResult.OK, form.DialogResult);
    }
}
