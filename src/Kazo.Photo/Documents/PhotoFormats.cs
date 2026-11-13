using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Photo.Documents;

/// <summary>
/// The files Kazo Photo knows (doc/en/plan.md, "Formats opened", Benoit's answer Q3 = A): the files
/// nothing else in Kazo OS opens, which Kazo Photo becomes the app of, and the images Kazo Paint
/// already opens, which Kazo Photo only offers in "Open with".
/// </summary>
/// <remarks>
/// <para>
/// The manifest makes one file handler per media type, and a media type takes the "default app" flag
/// of its first format (<see cref="CraftDocumentFormats.FileHandlers"/>). So a format offered in
/// "Open with" only never shares its media type with a default one: <c>.svgz</c> (default) and
/// <c>.svg</c> (Open with) have two types.
/// </para>
/// <para>
/// Work item 1 only declares the associations. Which saves are the document and which are copies
/// (<see cref="CraftDocumentFormat.IsCopy"/>) is decided by PhotoCraft itself in host mode (fork, H3:
/// only the document's own save is a <c>save</c> message, everything else a download), so no format
/// is marked as a copy here.
/// </para>
/// </remarks>
public static class PhotoFormats
{
    /// <summary>PhotoCraft's own document format.</summary>
    public const string NativeExtension = ".pcraft";

    /// <summary>Gets every format, with what Kazo Photo does with it.</summary>
    public static CraftDocumentFormats Formats { get; } = new(
    [
        // Default app (open and edit), Q3 = A. Layered documents: PhotoCraft's own, Photoshop,
        // GIMP, OpenRaster, Krita, Paint.NET and Affinity files.
        Default(".pcraft", "application/x-photocraft"),
        Default(".psd", "image/vnd.adobe.photoshop"),
        Default(".psb", "application/x-photoshop-large"),
        Default(".psdt", "application/x-photoshop-template"),
        Default(".xcf", "image/x-xcf"),
        Default(".xcfgz", "image/x-compressed-xcf"),
        Default(".ora", "image/openraster"),
        Default(".kra", "application/x-krita"),
        Default(".krz", "application/x-krita-archive"),
        Default(".pdn", "image/x-paintdotnet"),
        Default(".af", "application/x-affinity"),
        Default(".afphoto", "application/x-affinity-photo"),
        Default(".afdesign", "application/x-affinity-designer"),
        Default(".afpub", "application/x-affinity-publisher"),

        // Camera RAW files.
        Default(".dng", "image/x-adobe-dng"),
        Default(".cr2", "image/x-canon-cr2"),
        Default(".cr3", "image/x-canon-cr3"),
        Default(".nef", "image/x-nikon-nef"),
        Default(".nrw", "image/x-nikon-nrw"),
        Default(".arw", "image/x-sony-arw"),
        Default(".pef", "image/x-pentax-pef"),
        Default(".orf", "image/x-olympus-orf"),
        Default(".rw2", "image/x-panasonic-rw2"),
        Default(".raf", "image/x-fuji-raf"),

        // The images Kazo Paint does not read.
        Default(".tif", "image/tiff"),
        Default(".tiff", "image/tiff"),
        Default(".tga", "image/x-tga"),
        Default(".ico", "image/vnd.microsoft.icon"),
        Default(".qoi", "image/qoi"),
        Default(".exr", "image/x-exr"),
        Default(".hdr", "image/vnd.radiance"),
        Default(".pbm", "image/x-portable-bitmap"),
        Default(".pgm", "image/x-portable-graymap"),
        Default(".ppm", "image/x-portable-pixmap"),
        Default(".pam", "image/x-portable-arbitrarymap"),
        Default(".pfm", "image/x-portable-floatmap"),
        Default(".dds", "image/vnd-ms.dds"),
        Default(".fits", "image/fits"),
        Default(".fit", "image/fits"),
        Default(".fts", "image/fits"),
        Default(".dpx", "image/x-dpx"),
        Default(".cin", "image/x-cineon"),
        Default(".dcm", "application/dicom"),
        Default(".dicom", "application/dicom"),
        Default(".dc3", "application/dicom"),
        Default(".iff", "image/x-iff"),
        Default(".lbm", "image/x-iff"),
        Default(".ilbm", "image/x-iff"),
        Default(".heic", "image/heic"),
        Default(".heif", "image/heif"),
        Default(".hif", "image/heif"),
        Default(".svgz", "image/svg+xml-compressed"),

        // PhotoCraft's preset files (brushes, gradients, styles, swatches): they go to its libraries
        // instead of opening a document (work item 2), and nothing else opens them.
        Default(".abr", "application/x-photoshop-brush"),
        Default(".grd", "application/x-photoshop-gradient"),
        Default(".asl", "application/x-photoshop-style"),
        Default(".aco", "application/x-photoshop-swatches"),
        Default(".act", "application/x-photoshop-color-table"),
        Default(".ase", "application/x-adobe-swatch-exchange"),

        // "Open with" only, Q3 = A: a double click on a photo keeps opening the light app.
        OpenWith(".png", "image/png"),
        OpenWith(".jpg", "image/jpeg"),
        OpenWith(".jpeg", "image/jpeg"),
        OpenWith(".bmp", "image/bmp"),
        OpenWith(".gif", "image/gif"),
        OpenWith(".webp", "image/webp"),
        OpenWith(".svg", "image/svg+xml"),
    ]);

    /// <summary>Gets the extensions the OS Open dialog proposes, in the order above.</summary>
    public static IReadOnlyList<string> OpenExtensions => Formats.OpenExtensions;

    /// <summary>Gets the file associations of the manifest.</summary>
    public static IReadOnlyList<FileHandler> FileHandlers => Formats.FileHandlers;

    private static CraftDocumentFormat Default(string extension, string mediaType) =>
        new(extension, mediaType, IsDefault: true, IsCopy: false);

    private static CraftDocumentFormat OpenWith(string extension, string mediaType) =>
        new(extension, mediaType, IsDefault: false, IsCopy: false);
}
