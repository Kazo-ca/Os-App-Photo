using Kazo.Os.AppSdk.Craft;

namespace Kazo.Photo.Documents;

/// <summary>Where a file asked for opens (<see cref="PhotoDocuments.PlaceOf"/>).</summary>
public enum OpenPlace
{
    /// <summary>A new tab of this window, bound to its file.</summary>
    Here,

    /// <summary>The file is already open in this window: its tab is brought to the front.</summary>
    AlreadyOpen,

    /// <summary>
    /// Another Kazo Photo window: a file of the same name is already in this one, and PhotoCraft
    /// would take the two tabs for one file (decision 7).
    /// </summary>
    NewWindow,

    /// <summary>A preset file (brushes, gradients, styles, swatches): handed to PhotoCraft's libraries, never bound.</summary>
    Preset,
}

/// <summary>
/// The images of one Kazo Photo window and their files (doc/en/plan.md, work item 2, Benoit's
/// Q2 = A): each PhotoCraft tab is bound to its own file, by the name PhotoCraft saves it under.
/// </summary>
/// <remarks>
/// <para>
/// In host mode PhotoCraft keeps a file the window opened under its name (fork, H2), and hands each
/// save of a document over with that name and <c>saveAs</c> (H3). The module's
/// <see cref="DocumentSession"/> binds names to files and decides each save; this class adds what a
/// window with tabs needs around it: the tabs PhotoCraft lists (<c>session.inspect</c>), where a file
/// opens (decision 7), the save dialog's folder, and the title.
/// </para>
/// <para>
/// PhotoCraft marks a document saved only when the window answers that the file was written (H4), so
/// its <c>dirty</c> state is trusted as is: a save the window refused leaves the tab modified.
/// </para>
/// </remarks>
public sealed class PhotoDocuments
{
    private readonly DocumentSession _session = new(PhotoFormats.Formats, AppName);
    private IReadOnlyList<OpenDocument> _tabs = [];

    /// <summary>The window's name for its app, at the end of its title.</summary>
    public const string AppName = "Kazo Photo";

    /// <summary>Gets the tabs as PhotoCraft last listed them.</summary>
    public IReadOnlyList<OpenDocument> Tabs => _tabs;

    /// <summary>Gets the tab in front, or <see langword="null"/> when PhotoCraft shows its home screen.</summary>
    public OpenDocument? Active => _tabs.FirstOrDefault(tab => tab.Active);

    /// <summary>Gets whether the window has work that is not in a file.</summary>
    public bool IsDirty => _session.FrameDirty || _tabs.Any(tab => tab.Dirty) || _session.UnsavedNames.Count > 0;

    /// <summary>Gets the file a name is bound to.</summary>
    /// <param name="name">The name PhotoCraft saves a document under.</param>
    /// <returns>The file's absolute path, or <see langword="null"/>.</returns>
    public string? PathOf(string name) => _session.PathOf(name);

    /// <summary>Takes PhotoCraft's tab list (<c>session.inspect</c>, read by <see cref="PhotoDocumentList"/>).</summary>
    /// <param name="tabs">The tabs PhotoCraft has open.</param>
    public void Update(IReadOnlyList<OpenDocument> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        _tabs = tabs;
    }

    /// <summary>PhotoCraft reported that unsaved changes appeared, or went away, in its tabs.</summary>
    /// <param name="dirty">The new state.</param>
    public void SetFrameDirty(bool dirty) => _session.SetFrameDirty(dirty);

    /// <summary>
    /// Says where a file opens: here, in its tab when it is already open, in a new window when its
    /// name is taken by another file (decision 7), or in PhotoCraft's libraries for a preset.
    /// </summary>
    /// <param name="path">The file's absolute path.</param>
    /// <returns>The place.</returns>
    public OpenPlace PlaceOf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string name = CraftDocumentFormats.FileNameOf(path);
        if (PhotoFormats.IsPreset(name))
        {
            return OpenPlace.Preset;
        }

        if (TabOf(path) is not null)
        {
            return OpenPlace.AlreadyOpen;
        }

        // A tab that already saves under this name, a name bound to another file, or another file
        // of this name shown here (a tab bound to it after Save As keeps PhotoCraft's name): PhotoCraft
        // would take the first two for one file (detach_others_from_file), and the third would show
        // two images under one title, so the file gets its own window.
        bool open = _tabs.Any(tab => string.Equals(tab.Name, name, StringComparison.Ordinal));
        string? bound = _session.PathOf(name);
        bool shown = _tabs.Any(tab => BoundPathOf(tab) is { } other && string.Equals(CraftDocumentFormats.FileNameOf(other), name, StringComparison.Ordinal));
        return open || shown || (bound is not null && !string.Equals(bound, path, StringComparison.Ordinal))
            ? OpenPlace.NewWindow
            : OpenPlace.Here;
    }

    /// <summary>The tab of an open file, when it is listed: the tab bound to it, whatever PhotoCraft names it.</summary>
    /// <param name="path">The file's absolute path.</param>
    /// <returns>The tab, or <see langword="null"/>.</returns>
    public OpenDocument? TabOf(string path) =>
        _tabs.FirstOrDefault(tab => string.Equals(BoundPathOf(tab), path, StringComparison.Ordinal));

    private string? BoundPathOf(OpenDocument tab) => tab.Name is { } name ? _session.PathOf(name) : null;

    /// <summary>A file was handed to PhotoCraft, which keeps it under its name (H2): the name is bound to it.</summary>
    /// <param name="path">The file's absolute path.</param>
    public void Opened(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _session.Opened(CraftDocumentFormats.FileNameOf(path), path);
    }

    /// <summary>
    /// Decides a save of the document in front: the module's rule (<see cref="DocumentSession.Decide"/>),
    /// with the save dialog opening in the folder of the tab's own file when the name is new (a PNG
    /// that gained layers saves as a <c>.psd</c> next to it).
    /// </summary>
    /// <param name="name">The name PhotoCraft gave.</param>
    /// <param name="saveAs">PhotoCraft's <c>saveAs</c>.</param>
    /// <returns>The decision.</returns>
    public SaveDecision Decide(string name, bool saveAs)
    {
        SaveDecision decision = _session.Decide(name, saveAs);
        return decision.Folder is null && ActiveFolder() is { } folder ? decision with { Folder = folder } : decision;
    }

    /// <summary>Decides an export (a copy, ADR 0264 of Kazo OS): always asks, in the folder of the tab in front.</summary>
    /// <param name="name">The name PhotoCraft gave.</param>
    /// <returns>The decision.</returns>
    public SaveDecision DecideCopy(string name)
    {
        SaveDecision decision = _session.DecideCopy(name);
        return decision.Folder is null && ActiveFolder() is { } folder ? decision with { Folder = folder } : decision;
    }

    /// <summary>The file was written.</summary>
    /// <param name="decision">The decision carried out.</param>
    /// <param name="path">The file written.</param>
    public void Saved(SaveDecision decision, string path) => _session.Saved(decision, path);

    /// <summary>The file was not written: the dialog was cancelled, or the write failed.</summary>
    /// <param name="decision">The decision that did not complete.</param>
    public void SaveFailed(SaveDecision decision) => _session.SaveFailed(decision);

    /// <summary>Picks the tabs with work that is not in a file, in PhotoCraft's order.</summary>
    /// <returns>The tabs to save before the window closes.</returns>
    public IReadOnlyList<OpenDocument> WithUnsavedWork() => _session.WithUnsavedWork(_tabs);

    /// <summary>
    /// Builds the window title: <c>*name — Kazo Photo</c>, the name of the front tab's file (or of the
    /// tab when it has none), the asterisk while any tab has work that is not in a file.
    /// </summary>
    /// <param name="untitled">The name of an image with no name, in the user's language.</param>
    /// <param name="running">Whether PhotoCraft is running.</param>
    /// <returns>The title.</returns>
    public string Title(string untitled, bool running = true)
    {
        if (!running)
        {
            return AppName;
        }

        OpenDocument? tab = Active;
        if (tab is null)
        {
            return IsDirty ? $"*{AppName}" : AppName;
        }

        string name = tab.Name is { } saved && _session.PathOf(saved) is { } path
            ? CraftDocumentFormats.FileNameOf(path)
            : tab.Name ?? (tab.Title.Length > 0 ? tab.Title : untitled);
        return $"{(IsDirty ? "*" : string.Empty)}{name} — {AppName}";
    }

    private string? ActiveFolder() =>
        Active?.Name is { } name && _session.PathOf(name) is { } path ? CraftDocumentFormats.FolderOf(path) : null;
}
