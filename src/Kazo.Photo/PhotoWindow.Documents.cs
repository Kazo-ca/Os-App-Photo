using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.IO;
using Kazo.Photo.Documents;
using Microsoft.JSInterop;

namespace Kazo.Photo;

/// <summary>
/// Opening, saving, the exports and closing (doc/en/plan.md, work item 2): the window reads and
/// writes the files with <see cref="KazoFile"/>, asks the OS dialogs, keeps the title and guards the
/// close. <see cref="PhotoDocuments"/> binds each PhotoCraft tab to its file and decides each save.
/// </summary>
/// <remarks>
/// <para>
/// PhotoCraft in host mode (fork F2) hands over only the saves of a document (<c>photocraft:save</c>,
/// decision 6), with <c>saveAs</c> true for Save As, a first save and a format that can no longer
/// hold the document; it marks the document saved only when the window answers that the file was
/// written (H4). Copies, exports and preset files are downloads, which the module hands to the
/// window as copies (<see cref="CraftFrame.Exporting"/>, ADR 0264 of Kazo OS).
/// </para>
/// <para>
/// A name is bound once per window (decision 7): a file whose name is already taken here opens in a
/// new Kazo Photo window, and so does every file the OS opens with Kazo Photo (Q2 = A), since each
/// one starts its own window.
/// </para>
/// </remarks>
public sealed partial class PhotoWindow : IWindowCloseGuard
{
    /// <summary>The app's id, to start another Kazo Photo window on a file.</summary>
    private const string AppId = "ca.kazo.photo";

    /// <summary>
    /// How long the close guard waits for PhotoCraft's save of one image: PhotoCraft may first ask a
    /// question of its own (JPEG quality, TIFF layers), and a save that never comes must not hold the
    /// window open for good.
    /// </summary>
    private static readonly TimeSpan CloseSaveTimeout = TimeSpan.FromMinutes(2);

    /// <summary>How long the window waits for PhotoCraft to show a file it sent (a large PSD takes a while).</summary>
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(90);

    /// <summary>How often the window looks for the tab of a file it sent.</summary>
    private static readonly TimeSpan OpenPollInterval = TimeSpan.FromMilliseconds(150);

    private readonly PhotoDocuments _documents = new();

    // Saves run one at a time: each may show a dialog, and the order is PhotoCraft's.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // Completed with the outcome of the next save (true = written), for the close guard.
    private TaskCompletionSource<bool>? _nextSave;

    // A message over PhotoCraft: a failed write, a cancelled save, a copy written.
    private string? _notice;
    private bool _noticeIsError;

    /// <summary>Gets the title the OS shows for this window: <c>*name — Kazo Photo</c>.</summary>
    internal string DocumentTitle => _documents.Title(L["Untitled"], _startup.State == FrameState.Running);

    /// <summary>
    /// Asks before closing a window with work that is not in a file: PhotoCraft lists its tabs
    /// (<c>session.inspect</c>); "Yes" brings each modified one to the front and saves it, and the
    /// window closes only once every write succeeded (decision 8).
    /// </summary>
    /// <param name="cancellationToken">Cancels the close request.</param>
    /// <returns><see langword="true"/> to close.</returns>
    public async ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        if (_frame is not { } frame || _startup.State != FrameState.Running)
        {
            return true;
        }

        // A save still being written must end first: closing now would lose the write.
        await _saveGate.WaitAsync(cancellationToken);
        _saveGate.Release();

        if (!await RefreshTabsAsync(frame, cancellationToken))
        {
            // PhotoCraft does not answer, so it cannot save either: closing without saving is the
            // only choice left, and it is the user's.
            if (!_documents.IsDirty)
            {
                return true;
            }

            MessageBoxDialogResult discard = await MessageBox(
                L["CloseTitle"], L["CloseUnanswered", _documents.Active?.Title ?? L["Untitled"]], MessageBoxType.YesNo, cancellationToken);
            return discard.Result == MessageBoxResult.Yes;
        }

        IReadOnlyList<OpenDocument> unsaved = _documents.WithUnsavedWork();
        if (unsaved.Count == 0)
        {
            return true;
        }

        string names = string.Join(", ", unsaved.Select(NameOf));
        MessageBoxDialogResult answer = await MessageBox(
            L["CloseTitle"], L["CloseUnsaved", names], MessageBoxType.YesNoCancel, cancellationToken);
        return answer.Result switch
        {
            MessageBoxResult.Yes => await SaveAllAsync(frame, unsaved, cancellationToken),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    // After `ready`: the file the window was started for. PhotoCraft starts on its home screen
    // (New, Open…), as on the desktop: a window with no file stays there.
    private async Task PrepareDocumentAsync()
    {
        // Runs after `ready`, a [JSInvokable] callback: the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        if (_request.Path is { } path)
        {
            await OpenPathAsync(path);
        }

        await UpdateTitleAsync();
    }

    /// <summary>
    /// Opens a file where it belongs (<see cref="PhotoDocuments.PlaceOf"/>): a new tab here, the tab
    /// that already has it, a new Kazo Photo window when its name is taken, or PhotoCraft's
    /// libraries for a preset file.
    /// </summary>
    /// <param name="path">The file's absolute path.</param>
    /// <returns>A task that completes once the file is shown, or a message says why not.</returns>
    private async Task OpenPathAsync(string path)
    {
        if (_frame is not { } frame)
        {
            return;
        }

        await RefreshTabsAsync(frame);
        switch (_documents.PlaceOf(path))
        {
            case OpenPlace.AlreadyOpen when _documents.TabOf(path) is { } tab:
                await ExecuteAsync(frame, "document.activate", new { document = tab.Id });
                await RefreshTabsAsync(frame);
                await UpdateTitleAsync();
                return;
            case OpenPlace.NewWindow:
                await LaunchWindowAsync(path);
                return;
            case OpenPlace.Preset:
                await SendFileAsync(frame, path, bind: false);
                return;
            default:
                await SendFileAsync(frame, path, bind: true);
                return;
        }
    }

    // Reads a file and sends it to PhotoCraft (H2), which opens it in a new tab under its name; a
    // document is then bound to its file. A file that does not read, or that PhotoCraft does not
    // show, leaves a message.
    private async Task SendFileAsync(CraftFrame frame, string path, bool bind)
    {
        string name = CraftDocumentFormats.FileNameOf(path);
        byte[] bytes;
        try
        {
            bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await ShowNoticeAsync(L["OpenFailed", name, exception.Message], error: true);
            return;
        }

        int before = _documents.Tabs.Count;
        if (!await frame.OpenAsync(name, bytes))
        {
            await ShowNoticeAsync(L["OpenFailed", name, L["NotConnected"]], error: true);
            return;
        }

        if (!bind)
        {
            // A preset goes to PhotoCraft's libraries, which say so themselves.
            return;
        }

        // Bound at once: the next save of this name, even one made before the tab shows, is this file's.
        _documents.Opened(path);
        await ClearNoticeAsync();
        await WaitForTabAsync(frame, name, before);
        await UpdateTitleAsync();
    }

    // PhotoCraft opens the file on its next frames, or later for a large one: wait for its tab, so
    // the title names it. A file PhotoCraft cannot read comes back as `failed`, a message.
    private async Task WaitForTabAsync(CraftFrame frame, string name, int before)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + OpenTimeout;
        while (DateTimeOffset.UtcNow < deadline && !AppContext.CancellationToken.IsCancellationRequested)
        {
            if (await RefreshTabsAsync(frame)
                && _documents.Tabs.Count > before
                && _documents.Tabs.Any(tab => string.Equals(tab.Name, name, StringComparison.Ordinal)))
            {
                return;
            }

            try
            {
                await Task.Delay(OpenPollInterval, AppContext.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // Another Kazo Photo window on a file (decision 7).
    private async Task LaunchWindowAsync(string path)
    {
        try
        {
            await KazoApps.LaunchAsync(AppId, [path], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is KazoAppLaunchException or KazoUnauthorizedAccessException)
        {
            await ShowNoticeAsync(L["OpenFailed", CraftDocumentFormats.FileNameOf(path), exception.Message], error: true);
        }
    }

    // Asks PhotoCraft for its tabs (session.inspect); false when it does not answer.
    private async Task<bool> RefreshTabsAsync(CraftFrame frame, CancellationToken cancellationToken = default)
    {
        FrameCommandResult inspect = await ExecuteAsync(frame, "session.inspect", new { }, cancellationToken);
        if (!inspect.Ok)
        {
            return false;
        }

        _documents.Update(PhotoDocumentList.ListFrom(inspect.Value));
        return true;
    }

    private async Task OnDirtyChangedAsync(bool dirty)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        _documents.SetFrameDirty(dirty);
        if (_frame is { } frame)
        {
            // The tab in front may have changed too: the title names it.
            await RefreshTabsAsync(frame);
        }

        await UpdateTitleAsync();
    }

    // One save of a document PhotoCraft hands over: decided by PhotoDocuments, written here. The
    // answer is what PhotoCraft hears (`saved` only when true, H4).
    private async Task<bool> OnSavingAsync(FrameSave save)
    {
        // Called from JavaScript ([JSInvokable]): the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        await _saveGate.WaitAsync(AppContext.CancellationToken);
        bool written = false;
        try
        {
            written = await SaveAsync(save);
        }
        finally
        {
            _saveGate.Release();
            Interlocked.Exchange(ref _nextSave, null)?.TrySetResult(written);
            await UpdateTitleAsync();
        }

        return written;
    }

    // One file PhotoCraft exports (Save a Copy, Export As, Quick Export, Save for Web, layers to
    // files, preset files…; ADR 0264 of Kazo OS): always a copy, in the folder of the image in
    // front. It does not wait for the save gate: it never touches the document.
    private async Task<bool> OnExportingAsync(FrameSave export)
    {
        // Called from JavaScript ([JSInvokable]): the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        if (string.IsNullOrEmpty(export.Name))
        {
            return false;
        }

        SaveDecision decision = _documents.DecideCopy(export.Name);
        try
        {
            return await CarryOutAsync(decision, export.Bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await ShowNoticeAsync(L["SaveFailed", export.Name, exception.Message], error: true);
            return false;
        }
    }

    private async Task<bool> SaveAsync(FrameSave save)
    {
        if (string.IsNullOrEmpty(save.Name))
        {
            return false;
        }

        if (_frame is { } frame)
        {
            // The save is the tab's in front: its file gives the dialog's folder.
            await RefreshTabsAsync(frame);
        }

        SaveDecision decision = _documents.Decide(save.Name, save.SaveAs);
        try
        {
            return await CarryOutAsync(decision, save.Bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever went wrong (a dialog, the file system), the image must not pass for saved.
            _documents.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", save.Name, exception.Message], error: true);
            return false;
        }
    }

    // Asks for the path when the decision says so, writes the bytes, and records the outcome.
    private async Task<bool> CarryOutAsync(SaveDecision decision, byte[] bytes)
    {
        string? path = decision.AsksForPath ? await AskSavePathAsync(decision) : decision.Path;
        if (path is null)
        {
            _documents.SaveFailed(decision);
            if (decision.SavesTheDocument)
            {
                await ShowNoticeAsync(L["SaveCancelled", decision.Name], error: false);
            }

            return false;
        }

        try
        {
            await KazoFile.WriteAllBytesAsync(path, bytes, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _documents.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", CraftDocumentFormats.FileNameOf(path), exception.Message], error: true);
            return false;
        }

        _documents.Saved(decision, path);
        if (decision.Action == SaveAction.AskForCopy)
        {
            await ShowNoticeAsync(L["CopySaved", CraftDocumentFormats.FileNameOf(path)], error: false);
            return true;
        }

        // PhotoCraft has no command to rename a tab: it keeps the name it suggested, and its next
        // save under that name goes to the file just chosen (the binding).
        await ClearNoticeAsync();
        return true;
    }

    // The OS save dialog, in the image's folder, for the format PhotoCraft chose.
    private async Task<string?> AskSavePathAsync(SaveDecision decision)
    {
        string extension = CraftDocumentFormats.ExtensionOf(decision.Name);
        CraftDocumentFormat? format = PhotoFormats.Formats.Find(decision.Name);
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = decision.Action == SaveAction.AskForCopy ? L["SaveCopyTitle"] : L["SaveTitle"],
            InitialDirectory = VirtualPath.Parse(decision.Folder ?? AppContext.HomeDirectory),
            SuggestedFileName = decision.Name,
            DefaultExtension = extension.Length > 0 ? extension : null,
            Filters = extension.Length > 0
                ? [new FileDialogFilter(L["FilterFormat", extension], [extension], format is null ? [] : [format.MediaType])]
                : [],
            ConfirmOverwrite = true,
        });
        return result.Status == FileDialogStatus.Selected && result.Resource is { } chosen
            ? CraftDocumentFormats.WithExtensionOf(chosen.Path.Value, decision.Name)
            : null;
    }

    // "Yes" in the close dialog: each image is brought to the front (document.activate) and saved
    // by PhotoCraft's own File › Save; the window closes only when all of them reached a file.
    private async Task<bool> SaveAllAsync(CraftFrame frame, IReadOnlyList<OpenDocument> documents, CancellationToken cancellationToken)
    {
        foreach (OpenDocument document in documents)
        {
            TaskCompletionSource<bool> outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextSave = outcome;
            FrameCommandResult result = await ExecuteAsync(frame, "document.activate", new { document = document.Id }, cancellationToken);
            if (result.Ok)
            {
                result = await frame.RequestAsync("ui.menu.invoke", new { id = "file.save" }, cancellationToken: cancellationToken);
            }

            if (!result.Ok)
            {
                _nextSave = null;
                await ShowNoticeAsync(L["SaveFailed", NameOf(document), result.Error ?? string.Empty], error: true);
                return false;
            }

            bool written;
            try
            {
                written = await outcome.Task.WaitAsync(CloseSaveTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                _nextSave = null;
                written = false;
            }

            if (!written)
            {
                return false;
            }
        }

        return true;
    }

    // The name of an image in a message: its file's, else its tab's.
    private string NameOf(OpenDocument document) =>
        document.Name is { } name && _documents.PathOf(name) is { } path
            ? CraftDocumentFormats.FileNameOf(path)
            : document.Title.Length > 0 ? document.Title : L["Untitled"].Value;

    private async Task UpdateTitleAsync()
    {
        try
        {
            await SetWindowTitleAsync(DocumentTitle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSDisconnectedException)
        {
            // The window is closing.
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowNoticeAsync(string text, bool error)
    {
        _notice = text;
        _noticeIsError = error;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearNoticeAsync()
    {
        if (_notice is not null)
        {
            _notice = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void DismissNotice() => _notice = null;
}
