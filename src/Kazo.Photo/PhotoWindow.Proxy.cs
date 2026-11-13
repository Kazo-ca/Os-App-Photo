using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.IO;
using Kazo.Photo.Documents;
using Kazo.Photo.Hosting;

namespace Kazo.Photo;

/// <summary>
/// What the window does with the module's proxy (doc/en/plan.md, work item 2): PhotoCraft's file
/// pickers, the user's PhotoCraft preferences, and the OS theme and language.
/// </summary>
/// <remarks>
/// The proxy's messages arrive through a <c>[JSInvokable]</c> callback, outside the scope the OS opens
/// for app code: each handler that reaches the SDK (files, dialogs) enters the app's context itself.
/// The chords the window keeps are the OS's only (<see cref="ReservedKeys.Os"/>, decision 10), all
/// handled by the module, so the window has no chord of its own to answer.
/// </remarks>
public sealed partial class PhotoWindow
{
    /// <summary>
    /// How long PhotoCraft's storage writes are gathered before the file is written: counted from
    /// the first unsaved write, so a PhotoCraft that keeps writing still reaches the file.
    /// </summary>
    public static readonly TimeSpan SettingsWriteDelay = TimeSpan.FromMilliseconds(500);

    private WebStorageFile? _settingsFile;
    private CancellationTokenSource? _settingsWrite;

    // Reads the user's PhotoCraft storage before the iframe starts: PhotoCraft reads its preferences
    // (photocraft.preferences: tools, panels, colour settings, recent colours…) as it starts. It
    // lives in ~/.config/ca.kazo.photo/, so each Kazo user has their own instead of the browser's
    // shared localStorage. PhotoCraft's presets (brushes…) are in IndexedDB, which the module does
    // not reach: they stay shared by every Kazo user of the browser (a known limit, work item 3).
    private async Task<IReadOnlyDictionary<string, string>?> LoadSettingsAsync()
    {
        _settingsFile ??= new WebStorageFile(AppContext.HomeDirectory, AppId);
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            return new Dictionary<string, string>(await _settingsFile.LoadAsync(AppContext.CancellationToken), StringComparer.Ordinal);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // The proxy's events of one frame (subscribed in StartFrameAsync, removed in StopFrameAsync).
    private void ConnectProxy(CraftFrame frame, bool connect)
    {
        if (connect)
        {
            frame.PickRequested += OnPickRequestedAsync;
            frame.SettingChanged += OnSettingChangedAsync;
            frame.SchemeChanged += OnSchemeChangedAsync;
        }
        else
        {
            frame.PickRequested -= OnPickRequestedAsync;
            frame.SettingChanged -= OnSettingChangedAsync;
            frame.SchemeChanged -= OnSchemeChangedAsync;
        }
    }

    // PhotoCraft is listening: it takes the OS theme and the user's language (decision 9). A refused
    // command leaves PhotoCraft as it was: the images matter more.
    private async Task ApplyThemeAndLanguageAsync(CraftFrame frame)
    {
        await OnSchemeChangedAsync(await frame.GetColorSchemeAsync());
        await ExecuteAsync(frame, "prefs.set", new { path = "interface.language", value = PhotoCraftLanguage.Of(AppContext.Culture) });
    }

    // PhotoCraft follows the OS theme (decision 9): its "pro" theme for the dark scheme, "proLight"
    // for the light one.
    private async Task OnSchemeChangedAsync(string scheme)
    {
        if (_frame is { } frame)
        {
            await frame.RequestAsync("ui.set", new { theme = PhotoCraftTheme.Of(scheme) }, cancellationToken: AppContext.CancellationToken);
        }
    }

    // PhotoCraft opened its file picker: the OS dialog instead. File › Open (and the home screen's
    // Open…) is not handed to PhotoCraft: it hears "cancelled", and the window opens each chosen
    // file itself (OpenPathAsync), bound to its file, or in a new window when its name is taken
    // (decision 7). Every other picker (Place, a LUT, brushes, an action set, a stack…) gets the
    // files read and handed over: PhotoCraft reads their bytes itself.
    private Task OnPickRequestedAsync(PickRequest request) => InvokeAsync(async () =>
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        CraftFrame? frame = _frame;
        if (frame is null)
        {
            return;
        }

        bool open = PhotoFormats.IsOpenPicker(request);
        IReadOnlyList<PickedFile>? files = null;
        IReadOnlyList<string> toOpen = [];
        try
        {
            OpenFileDialogResult choice = await OpenFileAsync(
                new OpenFileDialogRequest
                {
                    Title = L[open ? "OpenTitle" : "PickTitle"],
                    InitialDirectory = VirtualPath.Parse(OpenFolder()),
                    AllowMultiple = request.Multiple,
                    Filters = FiltersOf(request, open),
                },
                AppContext.CancellationToken);
            if (choice.Status == FileDialogStatus.Selected && choice.Resources.Count > 0)
            {
                if (open)
                {
                    toOpen = [.. choice.Resources.Select(resource => resource.Path.Value)];
                }
                else
                {
                    files = await ReadPickedAsync(choice.Resources);
                }
            }
        }
        finally
        {
            // PhotoCraft waits on its picker until it hears back: it always does, even on a failure.
            await frame.AnswerPickAsync(request.Id, files);
        }

        // After the answer: PhotoCraft's picker is closed before the files open.
        foreach (string path in toOpen)
        {
            await OpenPathAsync(path);
        }
    });

    // The Open dialog starts in the folder of the image in front, else in the user's home.
    private string OpenFolder() =>
        _documents.Active?.Name is { } name && _documents.PathOf(name) is { } path && CraftDocumentFormats.FolderOf(path) is { } folder
            ? folder
            : AppContext.HomeDirectory;

    private async Task<IReadOnlyList<PickedFile>?> ReadPickedAsync(IReadOnlyList<SelectedFileResource> resources)
    {
        List<PickedFile> files = [];
        foreach (SelectedFileResource resource in resources)
        {
            string path = resource.Path.Value;
            string name = KazoPath.GetFileName(path) ?? path;
            try
            {
                byte[] bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
                files.Add(new PickedFile(name, string.Empty, bytes));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                await ShowProblemAsync(L["ReadFailed", name, exception.Message]);
                return null;
            }
        }

        return files;
    }

    // The extensions PhotoCraft accepts (for File › Open, the formats Kazo Photo knows, in their
    // order), then every file (PhotoCraft checks what it gets).
    private List<FileDialogFilter> FiltersOf(PickRequest request, bool open)
    {
        List<FileDialogFilter> filters = [];
        if (open)
        {
            filters.Add(new FileDialogFilter(L["ImagesFilter"], PhotoFormats.OpenExtensions, []));
        }
        else if (request.Extensions.Count > 0)
        {
            filters.Add(new FileDialogFilter(L["FilesFilter"], request.Extensions, []));
        }

        filters.Add(new FileDialogFilter(L["AllFiles"], [], []));
        return filters;
    }

    // PhotoCraft wrote to its storage: the user's file is written a moment later, with every write
    // gathered until then. A write already waiting takes the new value: it saves the file as it is
    // when it runs.
    private Task OnSettingChangedAsync(string key, string? value)
    {
        if (_settingsFile is null || !_settingsFile.Set(key, value) || _settingsWrite is not null)
        {
            return Task.CompletedTask;
        }

        CancellationTokenSource write = CancellationTokenSource.CreateLinkedTokenSource(AppContext.CancellationToken);
        _settingsWrite = write;
        _ = WriteSettingsLaterAsync(write);
        return Task.CompletedTask;
    }

    private async Task WriteSettingsLaterAsync(CancellationTokenSource write)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        CancellationToken cancellationToken = write.Token;
        try
        {
            await Task.Delay(SettingsWriteDelay, cancellationToken);
            // From here a new write schedules its own save, since this one may already have read the values.
            if (ReferenceEquals(_settingsWrite, write))
            {
                _settingsWrite = null;
                write.Dispose();
            }

            await _settingsFile!.SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed: FlushSettingsAsync writes what was waiting.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The values stay in this window's PhotoCraft; the next write tries again.
        }
    }

    // Writes what is still waiting when the window closes.
    private async Task FlushSettingsAsync()
    {
        if (_settingsWrite is null || _settingsFile is null)
        {
            return;
        }

        _settingsWrite.Cancel();
        _settingsWrite.Dispose();
        _settingsWrite = null;
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await _settingsFile.SaveAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Closing: nothing more to do.
        }
    }

    private async Task ShowProblemAsync(string message)
    {
        try
        {
            await MessageBox(L["ProblemTitle"], message, MessageBoxType.Ok, MessageBoxIcon.Error, AppContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
    }
}
