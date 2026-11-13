namespace Kazo.Photo;

/// <summary>
/// The PhotoCraft web build this app carries in <c>wwwroot/photocraft/</c>, described by its
/// provenance file <c>photocraft.json</c> (written by <c>tools/vendor-photocraft.sh</c>, checked by
/// the tests).
/// </summary>
public static class PhotoCraftBuild
{
    /// <summary>
    /// The PhotoCraft version of the build. Kept equal to the <c>version</c> of
    /// <c>wwwroot/photocraft/photocraft.json</c> by a test, so the window can show it without
    /// reading the file at run time.
    /// </summary>
    public const string Version = "0.23.1";

    /// <summary>Folder of the build under the app's static files (<c>_content/Kazo.Photo/</c>).</summary>
    public const string Folder = "photocraft";
}
