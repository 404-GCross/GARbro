using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GameRes;

namespace GARbro.GUI.Linux;

public sealed class MainWindow : Window
{
    private readonly ObservableCollection<ArchiveEntryItem> _entries = new();
    private readonly TextBlock _archiveText;
    private readonly TextBlock _statusText;
    private readonly TextBox _outputBox;
    private readonly TextBox _filterBox;
    private readonly ListBox _entryList;
    private readonly Button _extractSelectedButton;
    private readonly Button _extractAllButton;
    private ArcFile _archive;
    private string _archivePath;

    public MainWindow(string initialPath)
    {
        Title = "GARbro Linux";
        Width = 1040;
        Height = 680;
        MinWidth = 760;
        MinHeight = 480;

        _archiveText = new TextBlock
        {
            Text = "No archive loaded",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _statusText = new TextBlock
        {
            Text = "Ready",
            VerticalAlignment = VerticalAlignment.Center
        };
        _outputBox = new TextBox
        {
            Watermark = "Extraction folder",
            Text = Environment.CurrentDirectory,
            MinWidth = 280
        };
        _filterBox = new TextBox
        {
            Watermark = "Filter entries",
            MinWidth = 180
        };
        _entryList = CreateEntryList();
        _extractSelectedButton = CreateButton("Extract selected", OnExtractSelectedAsync);
        _extractAllButton = CreateButton("Extract all", OnExtractAllAsync);

        Content = BuildLayout();
        SetArchiveActionsEnabled(false);

        _filterBox.TextChanged += (_, _) => ApplyFilter();

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            OpenArchive(initialPath);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _archive?.Dispose();
        base.OnClosed(e);
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Background = Brushes.White
        };

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto,Auto"),
            Margin = new Thickness(12),
            ColumnSpacing = 8
        };
        toolbar.Children.Add(Place(CreateButton("Open archive", OnOpenArchiveAsync), 0));
        toolbar.Children.Add(Place(_extractSelectedButton, 1));
        toolbar.Children.Add(Place(_extractAllButton, 2));
        toolbar.Children.Add(Place(_archiveText, 3));
        toolbar.Children.Add(Place(_filterBox, 4));
        toolbar.Children.Add(Place(CreateButton("Formats", OnShowFormats), 5));
        root.Children.Add(Place(toolbar, 0, 0));

        var output = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(12, 0, 12, 12),
            ColumnSpacing = 8
        };
        output.Children.Add(Place(new TextBlock
        {
            Text = "Output",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }, 0));
        output.Children.Add(Place(_outputBox, 1));
        output.Children.Add(Place(CreateButton("Choose", OnChooseOutputAsync), 2));
        root.Children.Add(Place(output, 1, 0));

        var listFrame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 214, 220)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 12, 12),
            Child = _entryList
        };
        root.Children.Add(Place(listFrame, 2, 0));

        var status = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 246, 248)),
            Padding = new Thickness(12, 6),
            Child = _statusText
        };
        root.Children.Add(Place(status, 3, 0));

        return root;
    }

    private ListBox CreateEntryList()
    {
        var list = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = _entries,
            ItemTemplate = new FuncDataTemplate<ArchiveEntryItem>((item, _) => CreateEntryRow())
        };
        return list;
    }

    private static Control CreateEntryRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,120,120,100"),
            ColumnSpacing = 12,
            Margin = new Thickness(8, 4)
        };

        grid.Children.Add(Place(BoundText(nameof(ArchiveEntryItem.Name), TextTrimming.CharacterEllipsis), 0));
        grid.Children.Add(Place(BoundText(nameof(ArchiveEntryItem.Type), TextTrimming.None), 1));
        grid.Children.Add(Place(BoundText(nameof(ArchiveEntryItem.SizeText), TextTrimming.None), 2));
        grid.Children.Add(Place(BoundText(nameof(ArchiveEntryItem.OffsetText), TextTrimming.None), 3));

        return grid;
    }

    private static TextBlock BoundText(string property, TextTrimming trimming)
    {
        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = trimming
        };
        text.Bind(TextBlock.TextProperty, new Binding(property));
        return text;
    }

    private static Button CreateButton(string text, Func<Task> onClick)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 108,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += async (_, _) => await onClick();
        return button;
    }

    private static Button CreateButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 92,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private async Task OnOpenArchiveAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open archive",
            AllowMultiple = false
        });
        var path = files.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            OpenArchive(path);
        }
    }

    private async Task OnChooseOutputAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose extraction folder"
        });
        var folder = folders.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            _outputBox.Text = folder;
        }
    }

    private void OpenArchive(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                SetStatus($"File not found: {path}");
                return;
            }

            var nextArchive = ArcFile.TryOpen(path);
            if (nextArchive == null)
            {
                SetStatus($"Unknown or unsupported archive: {Path.GetFileName(path)}");
                return;
            }

            _archive?.Dispose();
            _archive = nextArchive;
            _archivePath = path;
            _archiveText.Text = path;
            _filterBox.Text = "";
            LoadEntries(_archive.Dir);
            SetArchiveActionsEnabled(true);
            SetStatus($"Opened {Path.GetFileName(path)}: {_entries.Count} entries, {_archive.Description}");
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}");
        }
    }

    private void LoadEntries(IEnumerable<Entry> source)
    {
        _entries.Clear();
        foreach (var entry in source.OrderBy(e => e.Offset).Select(e => new ArchiveEntryItem(e)))
        {
            _entries.Add(entry);
        }
    }

    private void ApplyFilter()
    {
        if (_archive == null)
        {
            return;
        }

        var filter = _filterBox.Text;
        var source = _archive.Dir.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            source = source.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        LoadEntries(source);
        SetStatus($"{_entries.Count} entries shown");
    }

    private async Task OnExtractSelectedAsync()
    {
        var selected = _entryList.SelectedItems?.OfType<ArchiveEntryItem>().Select(i => i.Entry).ToList();
        if (selected == null || selected.Count == 0)
        {
            SetStatus("Select one or more entries first.");
            return;
        }
        await ExtractAsync(selected, $"Extracted {selected.Count} selected entries");
    }

    private async Task OnExtractAllAsync()
    {
        if (_archive == null)
        {
            return;
        }
        await ExtractAsync(_archive.Dir.OrderBy(e => e.Offset).ToList(), $"Extracted {_archive.Dir.Count} entries");
    }

    private async Task ExtractAsync(IReadOnlyList<Entry> entries, string doneMessage)
    {
        if (_archive == null || entries.Count == 0)
        {
            return;
        }

        var outputDir = _outputBox.Text;
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            SetStatus("Choose an output folder first.");
            return;
        }

        try
        {
            Directory.CreateDirectory(outputDir);
            SetBusy(true);
            SetStatus($"Extracting to {outputDir} ...");

            await Task.Run(() =>
            {
                var original = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(outputDir);
                try
                {
                    for (var i = 0; i < entries.Count; ++i)
                    {
                        var entry = entries[i];
                        Dispatcher.UIThread.Post(() => SetStatus($"Extracting {i + 1}/{entries.Count}: {entry.Name}"));
                        _archive.Extract(entry);
                    }
                }
                finally
                {
                    Directory.SetCurrentDirectory(original);
                }
            });

            SetStatus($"{doneMessage} to {outputDir}");
        }
        catch (Exception ex)
        {
            SetStatus($"Extract failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnShowFormats()
    {
        var count = FormatCatalog.Instance.ArcFormats.Count();
        SetStatus($"{count} archive formats loaded. Use the filter box to narrow entries after opening an archive.");
    }

    private void SetArchiveActionsEnabled(bool enabled)
    {
        _extractSelectedButton.IsEnabled = enabled;
        _extractAllButton.IsEnabled = enabled;
    }

    private void SetBusy(bool busy)
    {
        _extractSelectedButton.IsEnabled = !busy && _archive != null;
        _extractAllButton.IsEnabled = !busy && _archive != null;
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static T Place<T>(T control, int row, int column) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }
}
