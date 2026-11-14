# Third-party notices

> [Version française](THIRD-PARTY-NOTICES-fr.md)

Kazo Photo is under the MIT license ([LICENSE](LICENSE)). Its C# code, its iframe page
(`src/Kazo.Photo/wwwroot/host/index.html`, `photocraft-page.css`) and its icon
(`src/Kazo.Photo/wwwroot/icons/app.svg`, `store/icon.png`) are this repository's own work. The
iframe proxy it uses (`craft-proxy.js`) belongs to Kazo OS (`Kazo.Os.AppSdk.Craft`, MIT).

It bundles two third-party works: **PhotoCraft**, which brings its own third-party material, and a
set of **fonts** under the SIL Open Font License.

## PhotoCraft

| | |
| --- | --- |
| Work | [PhotoCraft](https://github.com/storytold/photocraft), an image editor written in Rust, by the ArtCraft team and the PhotoCraft contributors |
| Version | 0.23.1, compiled from our fork [`Kazo-ca/photocraft`](https://github.com/Kazo-ca/photocraft) (branch `kazo/base`, commit `16ae9d59ecbaf987a26427b66edff708463b5c1f`: the upstream tag `v0.23.1` on branch `kazo/v0.23.1`, plus the changes below) |
| License | MIT or Apache-2.0, at the user's option |
| Files | `src/Kazo.Photo/wwwroot/photocraft/` (the web build: `.js`, `.wasm`, `snippets/`) |
| Provenance | `src/Kazo.Photo/wwwroot/photocraft/photocraft.json`: the fork's commit, the compilers, and the SHA-256 of every file |

**Changes made in the fork** (MIT or Apache-2.0, like PhotoCraft, so they can be offered upstream):
the web build gains `window.photocraft`, a control interface installed with `?control`
(Kazo-ca/photocraft#1), and a host mode for a page that embeds it (`craft-web-host`,
Kazo-ca/photocraft#2): the host hands it files, it asks the host to save the document and waits for
the answer, and in that mode it shows no ArtCraft link. Outside a host page PhotoCraft behaves as
upstream.

PhotoCraft's own notices ship **unmodified** next to its build, and in the installed package
(`_content/Kazo.Photo/photocraft/`):

| File | Content |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | PhotoCraft's licenses |
| `NOTICE` | PhotoCraft's copyright, its trademark notice, and the third-party material it bundles, with the notices those licenses require (egui icons, PDF reader, Foxit fonts, CMaps…) |
| `ATTRIBUTION.md` | PhotoCraft's per-file list of assets (author, source, license) |
| `OFL-Inter.txt`, `OFL-JetBrainsMono.txt` | The Inter and JetBrains Mono typefaces it embeds (SIL Open Font License 1.1) |

### Files the NOTICE and ATTRIBUTION.md refer to

PhotoCraft's `NOTICE` and `ATTRIBUTION.md` name license files of its source repository that its
web build does not carry. Kazo Photo ships them, copied unmodified from the fork at the bundled
commit (`16ae9d59ecbaf987a26427b66edff708463b5c1f`), in `src/Kazo.Photo/wwwroot/legal/photocraft/`:

| File | Source in PhotoCraft | Covers |
| --- | --- | --- |
| `LICENSE-lucide.txt` | `assets/icons/` | The Lucide icons (ISC, and MIT for those derived from Feather) |
| `LICENSE-noun-magnetic-lasso.txt` | `assets/icons/` | The Magnetic Lasso glyph, adapted from "Magnetic Lasso Tool" by Lil' Seal, Noun Project (CC BY 3.0) |
| `LICENSE-symmetry-axes.txt` | `assets/icons/` | The painting symmetry glyphs (MIT or Apache-2.0) |
| `app-icon-LICENSE.txt` | `assets/app-icon/LICENSE.txt` | PhotoCraft's app icon (MIT or Apache-2.0) |
| `LICENSE-solarized.txt` | `crates/ui-egui/src/` | The Solarized palette of the Solarized Dark theme (MIT) |
| `LICENSE-SCOWL.txt` | `assets/dict/` | The SCOWL English word list (SCOWL license) |
| `LICENSE-libfacedetection.txt` | `crates/algo/data/` | The libfacedetection weights and network (BSD-3-Clause) |

### The ArtCraft brand

The ArtCraft name, wordmark and logos are trademarks of the ArtCraft team; PhotoCraft's
open-source license does not cover them. Kazo Photo uses none of them: its name, its icon and its
Store listing are this repository's own, and in Kazo OS PhotoCraft runs in host mode, which shows
no ArtCraft link. "Based on PhotoCraft by the ArtCraft team" only says where the software comes
from.

## Fonts

PhotoCraft's text tool offers the Latin catalog of
[`storytold/craft-fonts`](https://github.com/storytold/craft-fonts) at the commit of
`tools/craft-fonts.rev` (`c1db6d1a8ed772191241c3d185e2104be84d8691`), less Inter, which PhotoCraft
already carries (`tools/vendor-fonts.sh`). They are in `src/Kazo.Photo/wwwroot/host/fonts/`, under
the SIL Open Font License 1.1, unmodified. Each family's `OFL.txt` (copyright and license) is in its
folder; `fonts/manifest.txt` lists every file with its SHA-256 and source.

| Family | Licence file | Source |
| --- | --- | --- |
| Caladea | `fonts/caladea/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/caladea |
| Carlito | `fonts/carlito/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/carlito |
| Lato | `fonts/lato/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/lato |
| Liberation Mono | `fonts/liberation-mono/OFL.txt` | https://github.com/liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz |
| Liberation Sans | `fonts/liberation-sans/OFL.txt` | https://github.com/liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz |
| Liberation Serif | `fonts/liberation-serif/OFL.txt` | https://github.com/liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz |
| Merriweather | `fonts/merriweather/OFL.txt` | https://github.com/SorkinType/Merriweather/tree/4481226b336843648b5b2ee64f75737f262ded15/fonts/ttf |
| Montserrat | `fonts/montserrat/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/montserrat |
| Nunito Sans | `fonts/nunito-sans/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/nunitosans |
| Open Sans | `fonts/open-sans/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/opensans |
| Playfair Display | `fonts/playfair-display/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/playfairdisplay |
| Poppins | `fonts/poppins/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/poppins |
| Roboto | `fonts/roboto/OFL.txt` | https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/roboto |
| Source Sans 3 | `fonts/source-sans-3/OFL.txt` | https://github.com/adobe-fonts/source-sans/releases/download/3.052R/TTF-source-sans-3.052R.zip |
| Source Serif 4 | `fonts/source-serif-4/OFL.txt` | https://github.com/adobe-fonts/source-serif/releases/download/4.005R/source-serif-4.005_Desktop.zip |

## In the package

The Kazo OS package (`.kazoapp`) carries, under `wwwroot/`:

- `legal/LICENSE.txt`: Kazo Photo's MIT license (this repository's `LICENSE`);
- `legal/THIRD-PARTY-NOTICES.md` and `legal/THIRD-PARTY-NOTICES-fr.md`: this file and its French
  version;
- `legal/photocraft/`: the license files PhotoCraft's `NOTICE` and `ATTRIBUTION.md` refer to, as
  above;
- `photocraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `ATTRIBUTION.md`, `OFL-Inter.txt`,
  `OFL-JetBrainsMono.txt`: PhotoCraft's notices, as above;
- `host/fonts/<family>/OFL.txt` and `host/fonts/manifest.txt`: the fonts' licenses and sources.

A C# test (`LegalFilesTests`) checks that the project puts these files in the package.

## Test data

The end-to-end tests make their own images in code; they ship no third-party file.
