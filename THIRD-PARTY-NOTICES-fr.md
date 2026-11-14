# Avis de tiers

> [English version](THIRD-PARTY-NOTICES.md)

Kazo Photo est sous licence MIT ([LICENSE](LICENSE)). Son code C#, sa page d'iframe
(`src/Kazo.Photo/wwwroot/host/index.html`, `photocraft-page.css`) et son icône
(`src/Kazo.Photo/wwwroot/icons/app.svg`, `store/icon.png`) sont l'œuvre de ce dépôt. Le proxy
d'iframe qu'il utilise (`craft-proxy.js`) appartient à Kazo OS (`Kazo.Os.AppSdk.Craft`, MIT).

Il contient deux œuvres de tiers : **PhotoCraft**, qui apporte ses propres éléments de tiers, et un
ensemble de **polices** sous SIL Open Font License.

## PhotoCraft

| | |
| --- | --- |
| Œuvre | [PhotoCraft](https://github.com/storytold/photocraft), un éditeur d'images écrit en Rust, de l'équipe ArtCraft et des contributeurs de PhotoCraft |
| Version | 0.23.1, compilée depuis notre fork [`Kazo-ca/photocraft`](https://github.com/Kazo-ca/photocraft) (branche `kazo/base`, commit `16ae9d59ecbaf987a26427b66edff708463b5c1f` : l'étiquette amont `v0.23.1` de la branche `kazo/v0.23.1`, plus les changements ci-dessous) |
| Licence | MIT ou Apache-2.0, au choix de l'utilisateur |
| Fichiers | `src/Kazo.Photo/wwwroot/photocraft/` (le build web : `.js`, `.wasm`, `snippets/`) |
| Provenance | `src/Kazo.Photo/wwwroot/photocraft/photocraft.json` : le commit du fork, les compilateurs et le SHA-256 de chaque fichier |

**Changements faits dans le fork** (MIT ou Apache-2.0, comme PhotoCraft, pour pouvoir les proposer
en amont) : le build web gagne `window.photocraft`, une interface de contrôle installée avec
`?control` (Kazo-ca/photocraft#1), et un mode hébergé pour une page qui l'intègre
(`craft-web-host`, Kazo-ca/photocraft#2) : l'hôte lui remet les fichiers, il demande à l'hôte
d'enregistrer le document et attend la réponse, et dans ce mode il ne montre aucun lien ArtCraft.
Hors d'une page hôte, PhotoCraft se comporte comme en amont.

Les avis de PhotoCraft sont livrés **sans modification** à côté de son build, et dans le paquet
installé (`_content/Kazo.Photo/photocraft/`) :

| Fichier | Contenu |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | Les licences de PhotoCraft |
| `NOTICE` | Le droit d'auteur de PhotoCraft, son avis de marque, et les éléments de tiers qu'il contient, avec les avis que leurs licences demandent (icônes egui, lecteur PDF, polices Foxit, CMaps…) |
| `ATTRIBUTION.md` | La liste, fichier par fichier, des ressources de PhotoCraft (auteur, source, licence) |
| `OFL-Inter.txt`, `OFL-JetBrainsMono.txt` | Les polices Inter et JetBrains Mono qu'il intègre (SIL Open Font License 1.1) |

### Fichiers cités par le NOTICE et l'ATTRIBUTION.md

Le `NOTICE` et l'`ATTRIBUTION.md` de PhotoCraft nomment des fichiers de licence de son dépôt source
que son build web ne contient pas. Kazo Photo les livre, copiés sans modification depuis le fork au
commit livré (`16ae9d59ecbaf987a26427b66edff708463b5c1f`), dans
`src/Kazo.Photo/wwwroot/legal/photocraft/` :

| Fichier | Source dans PhotoCraft | Couvre |
| --- | --- | --- |
| `LICENSE-lucide.txt` | `assets/icons/` | Les icônes Lucide (ISC, et MIT pour celles dérivées de Feather) |
| `LICENSE-noun-magnetic-lasso.txt` | `assets/icons/` | Le glyphe du Lasso magnétique, adapté de « Magnetic Lasso Tool » de Lil' Seal, Noun Project (CC BY 3.0) |
| `LICENSE-symmetry-axes.txt` | `assets/icons/` | Les glyphes de symétrie de peinture (MIT ou Apache-2.0) |
| `app-icon-LICENSE.txt` | `assets/app-icon/LICENSE.txt` | L'icône de l'app PhotoCraft (MIT ou Apache-2.0) |
| `LICENSE-solarized.txt` | `crates/ui-egui/src/` | La palette Solarized du thème Solarized Dark (MIT) |
| `LICENSE-SCOWL.txt` | `assets/dict/` | La liste de mots anglais SCOWL (licence SCOWL) |
| `LICENSE-libfacedetection.txt` | `crates/algo/data/` | Les poids et le réseau de libfacedetection (BSD-3-Clause) |

### La marque ArtCraft

Le nom, le logotype et les logos ArtCraft sont des marques de l'équipe ArtCraft ; la licence libre
de PhotoCraft ne les couvre pas. Kazo Photo n'en utilise aucun : son nom, son icône et sa fiche du
Store sont l'œuvre de ce dépôt, et dans Kazo OS PhotoCraft tourne en mode hébergé, qui ne montre
aucun lien ArtCraft. « Basé sur PhotoCraft de l'équipe ArtCraft » dit seulement d'où vient le
logiciel.

## Polices

L'outil Texte de PhotoCraft propose le catalogue latin de
[`storytold/craft-fonts`](https://github.com/storytold/craft-fonts) au commit de
`tools/craft-fonts.rev` (`c1db6d1a8ed772191241c3d185e2104be84d8691`), moins Inter, que PhotoCraft
contient déjà (`tools/vendor-fonts.sh`). Elles sont dans `src/Kazo.Photo/wwwroot/host/fonts/`, sous
SIL Open Font License 1.1, sans modification. Le `OFL.txt` de chaque famille (droit d'auteur et
licence) est dans son dossier ; `fonts/manifest.txt` liste chaque fichier avec son SHA-256 et sa
source.

| Famille | Fichier de licence | Source |
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

## Dans le paquet

Le paquet Kazo OS (`.kazoapp`) contient, sous `wwwroot/` :

- `legal/LICENSE.txt` : la licence MIT de Kazo Photo (le `LICENSE` de ce dépôt) ;
- `legal/THIRD-PARTY-NOTICES.md` et `legal/THIRD-PARTY-NOTICES-fr.md` : la version anglaise de ce
  fichier et ce fichier ;
- `legal/photocraft/` : les fichiers de licence cités par le `NOTICE` et l'`ATTRIBUTION.md` de
  PhotoCraft, comme ci-dessus ;
- `photocraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `ATTRIBUTION.md`, `OFL-Inter.txt`,
  `OFL-JetBrainsMono.txt` : les avis de PhotoCraft, comme ci-dessus ;
- `host/fonts/<famille>/OFL.txt` et `host/fonts/manifest.txt` : les licences et sources des polices.

Un test C# (`LegalFilesTests`) vérifie que le projet met ces fichiers dans le paquet.

## Données de test

Les tests de bout en bout fabriquent leurs images dans le code ; ils ne livrent aucun fichier de
tiers.
