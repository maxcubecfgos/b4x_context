using System;
using System.Threading;
using Xunit;

namespace B4XContext.Tests
{
    /// <summary>
    /// Constructs both windows on an STA thread so XAML/resource/template errors
    /// (e.g. a missing StaticResource or a bad ControlTemplate) fail the suite instead of
    /// only surfacing when a user opens the window.
    /// </summary>
    public class UiSmokeTests
    {
        [Fact]
        public void MainWindow_and_settings_window_construct_without_xaml_errors()
        {
            Exception? caught = null;

            var t = new Thread(() =>
            {
                try
                {
                    var app = new b4x_context.App();
                    app.InitializeComponent();

                    var settings = new b4x_context.HotkeySettingsWindow(
                        "Control,Shift", "P", 4096, "http://localhost:11434/v1", "qwen3.5:latest");
                    settings.Show();
                    settings.Close();

                    var main = new b4x_context.MainWindow();
                    main.Show();
                    main.Close();
                }
                catch (Exception ex) { caught = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(60));

            Assert.Null(caught);
        }
    }
}
