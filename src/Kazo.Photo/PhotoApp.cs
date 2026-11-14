using Kazo.Os.App.Abstractions;
using Kazo.Photo.Documents;

namespace Kazo.Photo;

/// <summary>
/// The app's manifest (doc/en/plan.md, decision 1). The build generates <c>manifest.json</c> from
/// this class (Kazo.Os.App.Build); it is never instantiated at run time.
/// </summary>
/// <remarks>
/// Work item 1 declares the identity, the window and its size, several windows at once, a window that
/// stays on its screen (decision 3), the capabilities of Kazo Fig and the file associations of
/// Benoit's answer Q3 = A (<see cref="PhotoFormats"/>). Opening and saving those files come with work
/// item 2, once PhotoCraft has its host mode (fork, F2).
/// </remarks>
[AppManifest]
public sealed class PhotoApp : AppDefinition
{
    /// <summary>
    /// Smallest usable width of PhotoCraft, in pixels: its desktop minimum (toolbar, tools, a panel
    /// column and some canvas).
    /// </summary>
    public const int ContentMinWidth = 760;

    /// <summary>Smallest usable height of PhotoCraft, in pixels: its desktop minimum.</summary>
    public const int ContentMinHeight = 480;

    /// <inheritdoc />
    public override string Id => "ca.kazo.photo";

    /// <inheritdoc />
    public override string Name => "Kazo Photo";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(PhotoWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.kazo";

    /// <inheritdoc />
    public override string Description => "A layered image editor with masks, adjustments and filters, based on PhotoCraft by the ArtCraft team";

    /// <inheritdoc />
    /// <remarks>The category of Kazo Fig (decision 1).</remarks>
    public override string Category => "graphics";

    /// <inheritdoc />
    /// <remarks>The tag that publishes to the Kazo OS Store must be v + this version.</remarks>
    public override string Version => "0.1.0";

    /// <inheritdoc />
    /// <remarks>
    /// Several windows at once: each one runs its own PhotoCraft in its own iframe, because the
    /// PhotoCraft canvas id is fixed and allows one instance per HTML document (doc/en/plan.md).
    /// </remarks>
    public override bool SingleInstancePerUser => false;

    /// <inheritdoc />
    /// <remarks>
    /// Kazo Fig's list (doc/en/plan.md, work item 1): the OS Open and Save dialogs, the user's files,
    /// the app's private folder (per-user settings), and launching a new window of the app (a file
    /// whose name is already bound in this window opens in another one, decision 7).
    /// </remarks>
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.DialogFileOpen,
        AppCapabilities.DialogFileSave,
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.FileSystemPrivateRead,
        AppCapabilities.FileSystemPrivateWrite,
        AppCapabilities.AppsLaunch,
    ];

    /// <inheritdoc />
    /// <remarks>
    /// What nothing else opens with <c>open</c> and <c>edit</c> (Kazo Photo becomes their app); the
    /// images Kazo Paint already opens with <c>open</c> only, for "Open with" (Q3 = A).
    /// </remarks>
    public override IReadOnlyList<FileHandler> FileHandlers => PhotoFormats.FileHandlers;

    /// <inheritdoc />
    public override IReadOnlyList<string> Icons => ["icons/app.svg"];

    /// <inheritdoc />
    public override IReadOnlyList<AssetManifest> Assets =>
        [new("icons/app.svg", AssetKind.Image, new string('0', 64), "app-icon")];

    /// <inheritdoc />
    /// <remarks>
    /// Sizes are the outer size of the OS window (title bar included): 1280 × 820 by default, which
    /// PhotoCraft's tools, options bar and panels fill (doc/en/plan.md, "Trial"); at least
    /// PhotoCraft's minimum plus the frame. <see cref="WindowOptions.CanChangeScreen"/> is
    /// <see langword="false"/>: moving the window to another screen moves its element into another
    /// document, which reloads PhotoCraft's iframe and loses the open images (Kazo OS ADR 0263,
    /// issue #2).
    /// </remarks>
    public override WindowOptions? Window => new()
    {
        DefaultWidth = 1280,
        DefaultHeight = 820,
        MinWidth = ContentMinWidth + WindowChromeWidth,
        MinHeight = ContentMinHeight + WindowChromeHeight,
        CanChangeScreen = false,
    };

    // Borders and title bar of an OS window (the tallest title bar of the Kazo OS themes, 2.35rem).
    private const int WindowChromeWidth = 2;
    private const int WindowChromeHeight = 40;
}
