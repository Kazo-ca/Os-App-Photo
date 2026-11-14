# Kazo Photo

A layered image editor for Kazo OS. Kazo Photo is based on
[PhotoCraft](https://github.com/storytold/photocraft) by the ArtCraft team, an open-source image
editor written in Rust, which runs here in a Kazo OS window.

## Edit images

- Layers, groups, layer masks and blend modes; adjustment layers (Levels, Curves, Hue/Saturation,
  Vibrance, Black & White, Gradient Map…) that keep the original pixels.
- Layer styles: shadows, glows, bevel, stroke, color, gradient and pattern overlays.
- Selections: marquees, lassos, Magic Wand, Quick Selection, Select and Mask.
- Brushes, retouching tools, gradients, type layers and vector shapes.
- Filters with a live preview: blur, sharpen, distort, noise, and more.
- Opens and saves layered `.psd` and `.psb` files, and reads many other formats: PNG, JPEG, WebP,
  TIFF, GIF, BMP, SVG, OpenRaster, Krita, GIMP, camera RAW files, HEIC, EXR and more.
- Several windows at once, each with its own images in tabs.

## Your Kazo OS files

- Double-click a `.psd`, a RAW photo or another layered image in the File Explorer: it opens in
  Kazo Photo. PNG, JPEG, GIF, BMP, WebP and SVG images list it in "Open with".
- Save rewrites the open file. Save As, and the first save of a new image or of a flat image that
  now has layers, go through the Kazo OS save dialog.
- Save a Copy and Export As go through the Kazo OS "Save a copy" dialog: the image stays bound to
  its file. Nothing goes to the browser's downloads.
- Closing the window asks before losing changes, for each open image.
- File › Open, and the files to place or import, come from the Kazo OS files, through the Kazo OS
  dialog. Opening a file whose name is already taken in a window opens a new window.
- The text tool offers about fifteen font families (Roboto, Open Sans, Lato, Montserrat, Liberation,
  Source Serif…), shipped with the app under the SIL Open Font License.
- Kazo Photo follows the OS theme (light or dark) and language; its settings are kept per Kazo OS
  user in `~/.config/ca.kazo.photo/`.

## Known limits

- **PhotoCraft is in early alpha.** Most images open and save fine, but keep a copy of the
  important ones.
- Kazo OS opens files up to 128 MiB. Large images are slow to open and save (about 40 seconds for a
  50 MB `.psd`), and each window needs a lot of memory (about 600 MB, more with large images). On a
  whole large image, some filters stop with "not enough working memory".
- Brushes, gradients, patterns and other presets are kept by the browser, shared by every Kazo OS
  user of that browser.
- No Chinese, Japanese or Korean font is shipped; text in those scripts may show as boxes.
- PhotoCraft draws with WebGPU or WebGL 2: a browser or a computer without either shows an error
  screen instead of the image.
- Two PhotoCraft shortcuts belong to Kazo OS: Alt+F9 (use Window › Actions) and holding Alt+Space
  to zoom out (use Ctrl+-).
- After Save As, the tab keeps the name PhotoCraft proposed; the window title shows the chosen file.
- The window stays on the screen where it opened: moving it to another screen would reload
  PhotoCraft and lose the open images.
- The download is about 25 MB (59 MB once installed), and each window loads its own copy of
  PhotoCraft.
- Needs Kazo OS 3.3.4 or later.

## License

Kazo Photo is free software under the MIT license. PhotoCraft is under the MIT or Apache-2.0
license; its notices, the licenses of the icons and data it carries, and those of the fonts (SIL
Open Font License 1.1) ship with the app (see `THIRD-PARTY-NOTICES.md` in the repository). The
ArtCraft name and logos are trademarks of the ArtCraft team and are not used by Kazo Photo.
