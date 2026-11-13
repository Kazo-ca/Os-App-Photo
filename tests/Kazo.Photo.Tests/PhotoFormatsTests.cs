using Kazo.Os.App.Abstractions;
using Kazo.Photo.Documents;

namespace Kazo.Photo.Tests;

/// <summary>The file associations of Benoit's answer Q3 = A (doc/en/plan.md).</summary>
public sealed class PhotoFormatsTests
{
    [Theory]
    [InlineData(".pcraft")]
    [InlineData(".psd")]
    [InlineData(".psb")]
    [InlineData(".xcf")]
    [InlineData(".ora")]
    [InlineData(".kra")]
    [InlineData(".pdn")]
    [InlineData(".afphoto")]
    [InlineData(".dng")]
    [InlineData(".cr3")]
    [InlineData(".tif")]
    [InlineData(".tiff")]
    [InlineData(".tga")]
    [InlineData(".exr")]
    [InlineData(".hdr")]
    [InlineData(".heic")]
    [InlineData(".dds")]
    [InlineData(".qoi")]
    [InlineData(".ico")]
    [InlineData(".svgz")]
    [InlineData(".abr")]
    public void Kazo_Photo_is_the_app_of_what_nothing_else_opens(string extension)
    {
        FileHandler handler = HandlerOf(extension);
        Assert.Equal(["open", "edit"], handler.Actions);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    [InlineData(".webp")]
    [InlineData(".svg")]
    public void The_images_Kazo_Paint_opens_are_only_in_Open_with(string extension)
    {
        // Q3 = A: a double click on a photo keeps opening the light app.
        FileHandler handler = HandlerOf(extension);
        Assert.Equal(["open"], handler.Actions);
    }

    [Fact]
    public void No_media_type_mixes_a_default_format_with_an_Open_with_one()
    {
        // The manifest takes a media type's flag from its first format: a mix would make Kazo Photo
        // the default app of an image Kazo Paint opens, or the reverse.
        foreach (IGrouping<string, Os.AppSdk.Craft.CraftDocumentFormat> group in PhotoFormats.Formats.All.GroupBy(format => format.MediaType))
        {
            Assert.Single(group.Select(format => format.IsDefault).Distinct());
        }
    }

    [Fact]
    public void Every_format_opens_and_none_is_a_copy_yet()
    {
        // Copies are told apart by PhotoCraft's host mode (fork, H3), not by the extension.
        Assert.All(PhotoFormats.Formats.All, format =>
        {
            Assert.True(format.Opens);
            Assert.False(format.IsCopy);
        });
        Assert.Equal(PhotoFormats.Formats.All.Count, PhotoFormats.OpenExtensions.Count);
        Assert.Equal(PhotoFormats.NativeExtension, PhotoFormats.OpenExtensions[0]);
    }

    private static FileHandler HandlerOf(string extension) =>
        Assert.Single(PhotoFormats.FileHandlers, handler => handler.Extensions.Contains(extension));
}
