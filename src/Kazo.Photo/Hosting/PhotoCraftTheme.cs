namespace Kazo.Photo.Hosting;

/// <summary>The PhotoCraft theme (<c>ui.set { theme }</c>) for a Kazo OS colour scheme (decision 9).</summary>
public static class PhotoCraftTheme
{
    /// <summary>PhotoCraft's dark theme, its default.</summary>
    public const string Dark = "pro";

    /// <summary>PhotoCraft's light theme.</summary>
    public const string Light = "proLight";

    /// <summary>
    /// <see cref="Light"/> for the OS's light scheme, <see cref="Dark"/> for anything else (the OS
    /// default is dark).
    /// </summary>
    /// <param name="scheme">The scheme <c>CraftFrame</c> reports (<c>light</c> or <c>dark</c>).</param>
    /// <returns>The value of PhotoCraft's <c>theme</c> setting.</returns>
    public static string Of(string? scheme) =>
        string.Equals(scheme, "light", StringComparison.OrdinalIgnoreCase) ? Light : Dark;
}
