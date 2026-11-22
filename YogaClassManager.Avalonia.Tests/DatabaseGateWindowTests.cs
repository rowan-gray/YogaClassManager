using Avalonia.Headless.XUnit;
using YogaClassManager.Avalonia.Views.Bootstrap;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>The startup gate is the only window until a database is chosen, so closing it must work -
/// it's how the user quits (title-bar close, Cmd+Q, Dock > Quit). It used to cancel every close.</summary>
public class DatabaseGateWindowTests
{
    [AvaloniaFact]
    public void TheGate_CanBeClosed_BeforeADatabaseIsChosen()
    {
        var window = new DatabaseGateWindow();
        window.Show();

        window.Close();

        Assert.True(window.IsClosed);
        Assert.False(window.IsVisible);
    }
}
