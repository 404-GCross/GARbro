using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using GameRes;

namespace GARbro.GUI.Linux;

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Default;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InitializeFormats();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var initialPath = desktop.Args is { Length: > 0 } ? desktop.Args[0] : null;
            desktop.MainWindow = new MainWindow(initialPath);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InitializeFormats()
    {
        var catalog = FormatCatalog.Instance;
        var formatsDat = Path.Combine(catalog.DataDirectory, "Formats.dat");
        if (File.Exists(formatsDat))
        {
            try
            {
                using var file = File.OpenRead(formatsDat);
                catalog.DeserializeScheme(file);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Scheme deserialization failed: {ex.Message}");
            }
        }

        catalog.ParametersRequest += (_, args) =>
        {
            args.InputResult = false;
        };
    }
}
