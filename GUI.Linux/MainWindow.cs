using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GameRes;

namespace GARbro.GUI.Linux;

public sealed class MainWindow : Window
{
    private enum ViewMode
    {
        Directory,
        Archive
    }

    private enum OverwriteChoice
    {
        Cancel,
        Overwrite,
        Skip
    }

    private enum SortKey
    {
        Name,
        Type,
        Size,
        Offset
    }

    private readonly ObservableCollection<ArchiveEntryItem> _entries = new();
    private readonly ObservableCollection<string> _recentPaths;
    private readonly LinuxSettings _settings;
    private readonly TextBlock _locationText;
    private readonly TextBlock _statusText;
    private readonly TextBox _outputBox;
    private readonly TextBox _filterBox;
    private readonly TextBox _passwordBox;
    private readonly ComboBox _convertFormatBox;
    private readonly ComboBox _recentBox;
    private readonly ComboBox _sortBox;
    private readonly ListBox _entryList;
    private readonly Border _previewHost;
    private readonly ProgressBar _progressBar;
    private readonly Button _upButton;
    private readonly Button _extractSelectedButton;
    private readonly Button _extractAllButton;
    private readonly Button _convertImageButton;
    private readonly Button _openExternalButton;
    private readonly Button _cancelButton;
    private readonly Button _sortDirectionButton;
    private ArcFile _archive;
    private string _archivePath;
    private string _currentDirectory;
    private ViewMode _mode;
    private CancellationTokenSource _cancelSource;
    private SortKey _sortKey = SortKey.Name;
    private bool _sortAscending = true;
    private bool _updatingRecent;

    public MainWindow(string initialPath)
    {
        Title = "GARbro Linux";
        Width = 1200;
        Height = 760;
        MinWidth = 900;
        MinHeight = 560;

        _settings = LinuxSettings.Load();
        _recentPaths = new ObservableCollection<string>(_settings.ExistingRecentPaths());
        _locationText = new TextBlock
        {
            Text = "Ready",
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
            Text = GetInitialOutputDirectory(),
            MinWidth = 260
        };
        _filterBox = new TextBox
        {
            Watermark = "Filter",
            MinWidth = 150
        };
        _passwordBox = new TextBox
        {
            Watermark = "Password/key",
            MinWidth = 130
        };
        _convertFormatBox = new ComboBox
        {
            Width = 82,
            SelectedIndex = 0,
            ItemsSource = new[] { "png", "jpg", "webp" }
        };
        _recentBox = new ComboBox
        {
            Width = 150,
            ItemsSource = _recentPaths,
            SelectedIndex = -1
        };
        _sortBox = new ComboBox
        {
            Width = 104,
            SelectedIndex = 0,
            ItemsSource = new[] { "Name", "Type", "Size", "Offset" }
        };
        _entryList = CreateEntryList();
        _previewHost = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 214, 220)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 253)),
            Padding = new Thickness(10)
        };
        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 8,
            IsVisible = false
        };
        _upButton = CreateButton("Up", OnUpAsync, 64);
        _extractSelectedButton = CreateButton("Extract selected", OnExtractSelectedAsync, 122);
        _extractAllButton = CreateButton("Extract all", OnExtractAllAsync, 92);
        _convertImageButton = CreateButton("Convert image", OnConvertImageAsync, 112);
        _openExternalButton = CreateButton("Open external", OnOpenExternalAsync, 112);
        _cancelButton = CreateButton("Cancel", OnCancelAsync, 76);
        _cancelButton.IsEnabled = false;
        _sortDirectionButton = CreateButton("Asc", OnToggleSortDirectionAsync, 64);

        Content = BuildLayout();
        KeyDown += OnKeyDown;
        _filterBox.TextChanged += (_, _) => ApplyFilter();
        _passwordBox.TextChanged += (_, _) => LinuxRuntimeOptions.Password = _passwordBox.Text;
        _recentBox.SelectionChanged += (_, _) => OnRecentSelectionChanged();
        _sortBox.SelectionChanged += (_, _) => OnSortChanged();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        SetPreviewMessage("Open a folder or archive to begin.");

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            OpenInitialPath(initialPath);
        }
        else
        {
            NavigateDirectory(Environment.CurrentDirectory);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancelSource?.Cancel();
        _archive?.Dispose();
        base.OnClosed(e);
    }

    private string GetInitialOutputDirectory()
    {
        return Directory.Exists(_settings.OutputDirectory)
            ? _settings.OutputDirectory
            : Environment.CurrentDirectory;
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
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(12),
            ColumnSpacing = 8
        };
        toolbar.Children.Add(Place(CreateButton("Open archive", OnOpenArchiveAsync, 112), 0));
        toolbar.Children.Add(Place(CreateButton("Open folder", OnOpenFolderAsync, 106), 1));
        toolbar.Children.Add(Place(_upButton, 2));
        toolbar.Children.Add(Place(_extractSelectedButton, 3));
        toolbar.Children.Add(Place(_extractAllButton, 4));
        toolbar.Children.Add(Place(_convertImageButton, 5));
        toolbar.Children.Add(Place(_convertFormatBox, 6));
        toolbar.Children.Add(Place(_openExternalButton, 7));
        toolbar.Children.Add(Place(_cancelButton, 8));
        toolbar.Children.Add(Place(CreateButton("Formats", OnShowFormatsAsync, 82), 9));
        toolbar.Children.Add(Place(_locationText, 10));
        root.Children.Add(Place(toolbar, 0, 0));

        var options = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            Margin = new Thickness(12, 0, 12, 12),
            ColumnSpacing = 8
        };
        options.Children.Add(Place(new TextBlock
        {
            Text = "Output",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }, 0));
        options.Children.Add(Place(_outputBox, 1));
        options.Children.Add(Place(CreateButton("Choose", OnChooseOutputAsync, 82), 2));
        options.Children.Add(Place(new TextBlock
        {
            Text = "Recent",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }, 3));
        options.Children.Add(Place(_recentBox, 4));
        options.Children.Add(Place(new TextBlock
        {
            Text = "Access",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }, 5));
        options.Children.Add(Place(_passwordBox, 6));
        options.Children.Add(Place(_filterBox, 7));
        options.Children.Add(Place(new TextBlock
        {
            Text = "Sort",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold
        }, 8));
        options.Children.Add(Place(_sortBox, 9));
        options.Children.Add(Place(_sortDirectionButton, 10));
        root.Children.Add(Place(options, 1, 0));

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,360"),
            Margin = new Thickness(12, 0, 12, 12),
            ColumnSpacing = 12
        };
        var listFrame = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 214, 220)),
            BorderThickness = new Thickness(1),
            Child = _entryList
        };
        body.Children.Add(Place(listFrame, 0));
        body.Children.Add(Place(_previewHost, 1));
        root.Children.Add(Place(body, 2, 0));

        var statusGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,220"),
            ColumnSpacing = 12,
            Background = new SolidColorBrush(Color.FromRgb(245, 246, 248)),
            Margin = new Thickness(0),
        };
        statusGrid.Children.Add(Place(_statusText, 0));
        statusGrid.Children.Add(Place(_progressBar, 1));
        root.Children.Add(Place(statusGrid, 3, 0));

        return root;
    }

    private ListBox CreateEntryList()
    {
        var list = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            ItemsSource = _entries,
            ItemTemplate = new FuncDataTemplate<ArchiveEntryItem>((_, _) => CreateEntryRow())
        };
        list.SelectionChanged += async (_, _) => await PreviewSelectionAsync();
        list.DoubleTapped += async (_, _) => await OnItemDefaultActionAsync();
        list.ContextMenu = BuildContextMenu();
        return list;
    }

    private ContextMenu BuildContextMenu()
    {
        var preview = new MenuItem { Header = "Preview" };
        preview.Click += async (_, _) => await PreviewSelectionAsync(force: true);
        var extract = new MenuItem { Header = "Extract selected" };
        extract.Click += async (_, _) => await OnExtractSelectedAsync();
        var convert = new MenuItem { Header = "Convert image" };
        convert.Click += async (_, _) => await OnConvertImageAsync();
        var external = new MenuItem { Header = "Open external" };
        external.Click += async (_, _) => await OnOpenExternalAsync();
        return new ContextMenu
        {
            ItemsSource = new[] { preview, extract, convert, external }
        };
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

    private static Button CreateButton(string text, Func<Task> onClick, double minWidth)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = minWidth,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += async (_, _) => await onClick();
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

    private async Task OnOpenFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open folder"
        });
        var folder = folders.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            NavigateDirectory(folder);
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
            _settings.OutputDirectory = folder;
            _settings.Save();
        }
    }

    private Task OnToggleSortDirectionAsync()
    {
        _sortAscending = !_sortAscending;
        UpdateSortDirectionButton();
        ApplyFilter();
        return Task.CompletedTask;
    }

    private void OnSortChanged()
    {
        if (_sortBox.SelectedIndex < 0)
        {
            return;
        }
        _sortKey = (SortKey)_sortBox.SelectedIndex;
        ApplyFilter();
    }

    private void OnRecentSelectionChanged()
    {
        if (_updatingRecent)
        {
            return;
        }

        var path = _recentBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _recentBox.SelectedIndex = -1;
        OpenInitialPath(path);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = GetFirstDroppedPath(e.Data) == null
            ? DragDropEffects.None
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var path = GetFirstDroppedPath(e.Data);
        if (!string.IsNullOrWhiteSpace(path))
        {
            OpenInitialPath(path);
        }
        e.Handled = true;
    }

    private Task OnShowFormatsAsync()
    {
        var count = FormatCatalog.Instance.ArcFormats.Count();
        SetStatus($"{count} archive formats loaded.");
        return Task.CompletedTask;
    }

    private Task OnCancelAsync()
    {
        _cancelSource?.Cancel();
        SetStatus("Cancel requested. Current file will finish, then extraction stops.");
        return Task.CompletedTask;
    }

    private Task OnUpAsync()
    {
        if (_mode == ViewMode.Directory)
        {
            var parent = Directory.GetParent(_currentDirectory);
            if (parent != null)
            {
                NavigateDirectory(parent.FullName);
            }
        }
        else if (!string.IsNullOrEmpty(_archivePath))
        {
            var parent = Path.GetDirectoryName(_archivePath);
            if (!string.IsNullOrEmpty(parent))
            {
                NavigateDirectory(parent);
            }
        }
        return Task.CompletedTask;
    }

    private void OpenInitialPath(string path)
    {
        if (Directory.Exists(path))
        {
            NavigateDirectory(path);
        }
        else if (File.Exists(path))
        {
            OpenArchive(path);
        }
        else
        {
            NavigateDirectory(Environment.CurrentDirectory);
            SetStatus($"Path not found: {path}");
        }
    }

    private void NavigateDirectory(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            if (!directory.Exists)
            {
                SetStatus($"Folder not found: {path}");
                return;
            }

            _archive?.Dispose();
            _archive = null;
            _archivePath = null;
            _currentDirectory = directory.FullName;
            _mode = ViewMode.Directory;
            _locationText.Text = _currentDirectory;
            _filterBox.Text = "";
            LoadDirectory(directory);
            RememberPath(_currentDirectory);
            SetPreviewMessage("Select a file to preview. Double-click a folder to enter or an archive to open.");
            SetStatus($"Opened folder: {_currentDirectory}");
            UpdateActions();
        }
        catch (Exception ex)
        {
            SetStatus($"Open folder failed: {ex.Message}");
        }
    }

    private void LoadDirectory(DirectoryInfo directory)
    {
        var items = new List<ArchiveEntryItem>();
        if (directory.Parent != null)
        {
            items.Add(ArchiveEntryItem.FromDirectory(directory.Parent, isParent: true));
        }

        try
        {
            items.AddRange(directory.EnumerateDirectories()
                .Where(d => !d.Attributes.HasFlag(FileAttributes.System))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => ArchiveEntryItem.FromDirectory(d)));
            items.AddRange(directory.EnumerateFiles()
                .Where(f => !f.Attributes.HasFlag(FileAttributes.System))
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => ArchiveEntryItem.FromFile(f)));
        }
        catch (UnauthorizedAccessException ex)
        {
            SetStatus(ex.Message);
        }

        LoadItems(items);
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

            LinuxRuntimeOptions.Password = _passwordBox.Text;
            var nextArchive = ArcFile.TryOpen(path);
            if (nextArchive == null)
            {
                SetStatus($"Unknown or unsupported archive: {Path.GetFileName(path)}");
                return;
            }

            _archive?.Dispose();
            _archive = nextArchive;
            _archivePath = path;
            _currentDirectory = Path.GetDirectoryName(path);
            _mode = ViewMode.Archive;
            _locationText.Text = path;
            _filterBox.Text = "";
            LoadArchiveEntries(_archive.Dir);
            RememberPath(path);
            SetPreviewMessage("Select an entry to preview. Use Extract buttons to write files.");
            SetStatus($"Opened {Path.GetFileName(path)}: {_entries.Count} entries, {_archive.Description}");
            UpdateActions();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Open cancelled. Try entering a password/key and reopening the archive.");
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}");
        }
    }

    private void LoadArchiveEntries(IEnumerable<Entry> source)
    {
        LoadItems(source.OrderBy(e => e.Offset).Select(ArchiveEntryItem.FromEntry));
    }

    private void LoadItems(IEnumerable<ArchiveEntryItem> source)
    {
        _entries.Clear();
        foreach (var item in SortItems(ApplyTextFilter(source)))
        {
            _entries.Add(item);
        }
        UpdateActions();
    }

    private IEnumerable<ArchiveEntryItem> SortItems(IEnumerable<ArchiveEntryItem> source)
    {
        var items = source.ToList();
        var parents = items.Where(i => i.IsParentDirectory);
        var regular = items.Where(i => !i.IsParentDirectory);

        IEnumerable<ArchiveEntryItem> sorted = _sortKey switch
        {
            SortKey.Type => _sortAscending
                ? regular.OrderBy(GroupKey).ThenBy(i => i.Type, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : regular.OrderBy(GroupKey).ThenByDescending(i => i.Type, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            SortKey.Size => _sortAscending
                ? regular.OrderBy(GroupKey).ThenBy(i => i.Size).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : regular.OrderBy(GroupKey).ThenByDescending(i => i.Size).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            SortKey.Offset => _sortAscending
                ? regular.OrderBy(GroupKey).ThenBy(NormalizedOffset).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : regular.OrderBy(GroupKey).ThenByDescending(NormalizedOffset).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            _ => _sortAscending
                ? regular.OrderBy(GroupKey).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                : regular.OrderBy(GroupKey).ThenByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase)
        };

        return parents.Concat(sorted);
    }

    private IEnumerable<ArchiveEntryItem> ApplyTextFilter(IEnumerable<ArchiveEntryItem> source)
    {
        var filter = _filterBox.Text;
        if (string.IsNullOrWhiteSpace(filter))
        {
            return source;
        }
        return source.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyFilter()
    {
        if (_mode == ViewMode.Archive && _archive != null)
        {
            LoadArchiveEntries(_archive.Dir);
            SetStatus($"{_entries.Count} entries shown");
        }
        else if (_mode == ViewMode.Directory && !string.IsNullOrEmpty(_currentDirectory))
        {
            LoadDirectory(new DirectoryInfo(_currentDirectory));
            SetStatus($"{_entries.Count} items shown");
        }
    }

    private async Task OnItemDefaultActionAsync()
    {
        var item = GetSingleSelectedItem();
        if (item == null)
        {
            return;
        }

        if (item.IsDirectory)
        {
            NavigateDirectory(item.FullPath);
        }
        else if (item.IsFileSystemItem)
        {
            OpenArchive(item.FullPath);
        }
        else
        {
            await PreviewSelectionAsync(force: true);
        }
    }

    private async Task PreviewSelectionAsync(bool force = false)
    {
        var selected = GetSelectedItems();
        UpdateActions();
        if (selected.Count == 0)
        {
            SetPreviewMessage("No selection.");
            return;
        }
        if (selected.Count > 1 && !force)
        {
            SetPreviewMessage($"{selected.Count} items selected.");
            return;
        }

        var item = selected[0];
        if (item.IsDirectory)
        {
            SetPreviewMessage("Folder. Double-click to open.");
            return;
        }

        try
        {
            string imageError = null;
            var data = await ReadItemBytesAsync(item, maxBytes: 64 * 1024 * 1024);
            if (data == null)
            {
                SetPreviewMessage("File is too large for preview.");
                return;
            }

            if (MediaTools.IsLikelyImage(item.Name) && MediaTools.TryCreateBitmap(data, out var bitmap, out imageError))
            {
                SetImagePreview(bitmap, item.Name);
                return;
            }

            if (MediaTools.IsLikelyText(item.Name) || !MediaTools.LooksBinary(data))
            {
                SetTextPreview(MediaTools.DecodeTextPreview(data));
                return;
            }

            if (MediaTools.IsLikelyExternalMedia(item.Name))
            {
                SetPreviewMessage("Media file. Use Open external to play with the desktop handler.");
                return;
            }

            SetPreviewMessage(MediaTools.IsLikelyImage(item.Name)
                ? $"Image preview failed: {imageError}"
                : "Binary file. Use Extract or Open external.");
        }
        catch (Exception ex)
        {
            SetPreviewMessage($"Preview failed: {ex.Message}");
        }
    }

    private async Task<byte[]> ReadItemBytesAsync(ArchiveEntryItem item, long maxBytes = long.MaxValue)
    {
        if (item.Size > maxBytes)
        {
            return null;
        }

        await using var stream = OpenItemStream(item);
        if (stream == null)
        {
            return null;
        }
        if (stream.CanSeek && stream.Length > maxBytes)
        {
            return null;
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }

    private Stream OpenItemStream(ArchiveEntryItem item)
    {
        if (item.IsArchiveEntry)
        {
            return _archive?.OpenEntry(item.Entry);
        }
        if (item.IsFileSystemItem && !item.IsDirectory)
        {
            return File.OpenRead(item.FullPath);
        }
        return null;
    }

    private async Task OnExtractSelectedAsync()
    {
        if (_mode != ViewMode.Archive)
        {
            SetStatus("Open an archive before extracting.");
            return;
        }

        var selected = GetSelectedItems().Where(i => i.IsArchiveEntry).Select(i => i.Entry).ToList();
        if (selected.Count == 0)
        {
            SetStatus("Select one or more archive entries first.");
            return;
        }
        await ExtractAsync(selected, $"Extracted {selected.Count} selected entries");
    }

    private async Task OnExtractAllAsync()
    {
        if (_archive == null)
        {
            SetStatus("Open an archive before extracting.");
            return;
        }
        await ExtractAsync(_archive.Dir.OrderBy(e => e.Offset).ToList(), $"Extracted {_archive.Dir.Count} entries");
    }

    private async Task ExtractAsync(IReadOnlyList<Entry> entries, string doneMessage)
    {
        var outputDir = _outputBox.Text;
        if (_archive == null || entries.Count == 0)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            SetStatus("Choose an output folder first.");
            return;
        }

        try
        {
            Directory.CreateDirectory(outputDir);
            var existing = entries.Count(e => File.Exists(BuildOutputPath(outputDir, e.Name)));
            var overwrite = OverwriteChoice.Overwrite;
            if (existing > 0)
            {
                overwrite = await AskOverwriteAsync(existing);
                if (overwrite == OverwriteChoice.Cancel)
                {
                    SetStatus("Extraction cancelled.");
                    return;
                }
            }

            _cancelSource = new CancellationTokenSource();
            SetBusy(true, entries.Count);
            var extracted = 0;
            var skipped = 0;
            await Task.Run(async () =>
            {
                for (var i = 0; i < entries.Count; ++i)
                {
                    _cancelSource.Token.ThrowIfCancellationRequested();
                    var entry = entries[i];
                    var destination = BuildOutputPath(outputDir, entry.Name);
                    if (overwrite == OverwriteChoice.Skip && File.Exists(destination))
                    {
                        ++skipped;
                        await Dispatcher.UIThread.InvokeAsync(() => UpdateProgress(i + 1, entries.Count, $"Skipped existing: {entry.Name}"));
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    await using var input = _archive.OpenEntry(entry);
                    await using var output = File.Create(destination);
                    await input.CopyToAsync(output, _cancelSource.Token);
                    ++extracted;
                    await Dispatcher.UIThread.InvokeAsync(() => UpdateProgress(i + 1, entries.Count, $"Extracted {entry.Name}"));
                }
            });

            SetStatus($"{doneMessage} to {outputDir}. {extracted} written, {skipped} skipped.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Extraction cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Extract failed: {ex.Message}");
        }
        finally
        {
            _cancelSource?.Dispose();
            _cancelSource = null;
            SetBusy(false, 1);
        }
    }

    private async Task OnConvertImageAsync()
    {
        var item = GetSingleSelectedItem();
        if (item == null || item.IsDirectory)
        {
            SetStatus("Select one image first.");
            return;
        }

        var outputDir = _outputBox.Text;
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            SetStatus("Choose an output folder first.");
            return;
        }

        var format = _convertFormatBox.SelectedItem?.ToString() ?? "png";
        var targetName = Path.ChangeExtension(item.Name, format);
        var destination = BuildOutputPath(outputDir, targetName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (File.Exists(destination) && await AskOverwriteAsync(1) != OverwriteChoice.Overwrite)
            {
                SetStatus("Image conversion skipped.");
                return;
            }

            var data = await ReadItemBytesAsync(item);
            await using var output = File.Create(destination);
            if (!MediaTools.TryConvertImage(data, output, format, out var error))
            {
                SetStatus($"Image conversion failed: {error}");
                return;
            }
            SetStatus($"Converted image to {destination}");
        }
        catch (Exception ex)
        {
            SetStatus($"Image conversion failed: {ex.Message}");
        }
    }

    private async Task OnOpenExternalAsync()
    {
        var item = GetSingleSelectedItem();
        if (item == null || item.IsDirectory)
        {
            SetStatus("Select one file first.");
            return;
        }

        try
        {
            string path;
            if (item.IsFileSystemItem)
            {
                path = item.FullPath;
            }
            else
            {
                path = Path.Combine(Path.GetTempPath(), "garbro-linux-preview", Guid.NewGuid() + "-" + MediaTools.SafeFileName(item.Name));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await using var input = _archive.OpenEntry(item.Entry);
                await using var output = File.Create(path);
                await input.CopyToAsync(output);
            }
            MediaTools.OpenWithDesktop(path);
            SetStatus($"Opened externally: {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"Open external failed: {ex.Message}");
        }
    }

    private async Task<OverwriteChoice> AskOverwriteAsync(int existingCount)
    {
        var dialog = new ChoiceDialog(
            "Existing files",
            $"{existingCount} output file(s) already exist.",
            ("Overwrite", OverwriteChoice.Overwrite),
            ("Skip", OverwriteChoice.Skip),
            ("Cancel", OverwriteChoice.Cancel));
        return await dialog.ShowDialog<OverwriteChoice>(this);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O)
        {
            e.Handled = true;
            _ = OnOpenArchiveAsync();
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.E)
        {
            e.Handled = true;
            _ = OnExtractSelectedAsync();
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.F)
        {
            e.Handled = true;
            _filterBox.Focus();
        }
        else if (e.Key == Key.Back)
        {
            e.Handled = true;
            _ = OnUpAsync();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = OnItemDefaultActionAsync();
        }
        else if (e.Key == Key.Escape && _cancelSource != null)
        {
            e.Handled = true;
            _cancelSource.Cancel();
        }
    }

    private List<ArchiveEntryItem> GetSelectedItems()
    {
        return _entryList.SelectedItems?.OfType<ArchiveEntryItem>().ToList() ?? new List<ArchiveEntryItem>();
    }

    private ArchiveEntryItem GetSingleSelectedItem()
    {
        return GetSelectedItems().FirstOrDefault();
    }

    private void UpdateActions()
    {
        var selected = GetSelectedItems();
        var hasArchive = _mode == ViewMode.Archive && _archive != null;
        var singleFile = selected.Count == 1 && !selected[0].IsDirectory;
        _upButton.IsEnabled = _mode == ViewMode.Archive || Directory.GetParent(_currentDirectory ?? "") != null;
        _extractSelectedButton.IsEnabled = hasArchive && selected.Any(i => i.IsArchiveEntry) && _cancelSource == null;
        _extractAllButton.IsEnabled = hasArchive && _cancelSource == null;
        _convertImageButton.IsEnabled = singleFile && _cancelSource == null;
        _openExternalButton.IsEnabled = singleFile && _cancelSource == null;
    }

    private void SetBusy(bool busy, int maximum)
    {
        _cancelButton.IsEnabled = busy;
        _progressBar.IsVisible = busy;
        _progressBar.Value = 0;
        _progressBar.Maximum = Math.Max(1, maximum);
        UpdateActions();
    }

    private void UpdateProgress(int current, int total, string text)
    {
        _progressBar.Maximum = Math.Max(1, total);
        _progressBar.Value = current;
        SetStatus(text);
    }

    private void SetImagePreview(Bitmap bitmap, string name)
    {
        _previewHost.Child = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                Place(new TextBlock
                {
                    Text = name,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8),
                    TextTrimming = TextTrimming.CharacterEllipsis
                }, 0, 0),
                Place(new ScrollViewer
                {
                    Content = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    }
                }, 1, 0)
            }
        };
    }

    private void SetTextPreview(string text)
    {
        _previewHost.Child = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = FontFamily.Parse("monospace")
        };
    }

    private void SetPreviewMessage(string message)
    {
        _previewHost.Child = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 86, 96))
        };
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
    }

    private void RememberPath(string path)
    {
        _settings.RememberPath(path);
        _settings.Save();
        RefreshRecentPaths();
    }

    private void RefreshRecentPaths()
    {
        _updatingRecent = true;
        try
        {
            _recentPaths.Clear();
            foreach (var path in _settings.ExistingRecentPaths())
            {
                _recentPaths.Add(path);
            }
            _recentBox.SelectedIndex = -1;
        }
        finally
        {
            _updatingRecent = false;
        }
    }

    private void UpdateSortDirectionButton()
    {
        _sortDirectionButton.Content = _sortAscending ? "Asc" : "Desc";
    }

    private static string GetFirstDroppedPath(IDataObject data)
    {
        var files = data.GetFiles();
        if (files == null)
        {
            return null;
        }

        foreach (var item in files)
        {
            var path = item.Path.LocalPath;
            if (Directory.Exists(path) || File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    private static int GroupKey(ArchiveEntryItem item)
    {
        return item.IsDirectory ? 0 : 1;
    }

    private static long NormalizedOffset(ArchiveEntryItem item)
    {
        return item.Offset < 0 ? long.MaxValue : item.Offset;
    }

    private static string BuildOutputPath(string outputDir, string entryName)
    {
        var root = Path.GetFullPath(outputDir);
        var parts = entryName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != "." && p != "..");
        var path = parts.Aggregate(root, Path.Combine);
        path = Path.GetFullPath(path);
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && path != root)
        {
            throw new InvalidOperationException($"Unsafe output path: {entryName}");
        }
        return path;
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

    private sealed class ChoiceDialog : Window
    {
        public ChoiceDialog(string title, string message, params (string Label, OverwriteChoice Choice)[] choices)
        {
            Title = title;
            Width = 360;
            Height = 160;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };
            foreach (var (label, choice) in choices)
            {
                var button = new Button
                {
                    Content = label,
                    MinWidth = 84,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };
                button.Click += (_, _) => Close(choice);
                buttons.Children.Add(button);
            }

            Content = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Margin = new Thickness(16),
                Children =
                {
                    Place(new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    }, 0, 0),
                    Place(buttons, 1, 0)
                }
            };
        }
    }
}
