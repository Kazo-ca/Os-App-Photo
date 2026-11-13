> [English version](../en/plan.md)

# Plan : l'app Kazo Photo pour Kazo OS

Étape 0 du [guide de portage des apps Craft](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/craft-app-port-guide.md)
de Kazo OS, écrite le 2026-11-13 (issue `TODO` [#1](https://github.com/Kazo-ca/Os-App-Photo/issues/1)).
Ce document fixe le but, ce que la lecture de PhotoCraft a montré, les décisions, les questions
posées à Benoit et les chantiers. Chaque chantier est écrit pour être confié tel quel à un fil
d'implémentation. Les références les plus proches sont Kazo Fig
([plan](https://github.com/Kazo-ca/Os-App-Fig/blob/main/doc/fr/plan.md), plusieurs documents en
onglets) et Kazo Pdf ([plan](https://github.com/Kazo-ca/Os-App-PDF/blob/pdf/base/doc/fr/plan.md),
exports reçus comme copies).

## But

Kazo Photo est **l'éditeur d'images de Kazo OS** : calques, masques, réglages, filtres, texte,
fichiers `.psd`. Il emballe le build web de [PhotoCraft](https://github.com/storytold/photocraft)
(équipe ArtCraft, `MIT OR Apache-2.0`, Rust et egui, en alpha) et le branche sur les fichiers,
les dialogues et les associations de Kazo OS. Kazo Paint, l'app de base, reste l'outil simple ;
Kazo Photo est l'outil complet.

**Ce qui distingue ce portage des autres** : PhotoCraft n'a **ni mode hébergé
(`craft-web-host`) ni API de commandes (`craft-web-api`)**. WordCraft, GridCraft, PdfCraft et
FigCraft les avaient ; ici il faut les écrire, dans notre fork
[`Kazo-ca/photocraft`](https://github.com/Kazo-ca/photocraft), avant que la fenêtre Kazo puisse
ouvrir ou enregistrer quoi que ce soit. Kazo Photo est donc, dès sa première version, un build
**modifié** compilé depuis le fork ; il n'y a pas d'étape « zip officiel ».

Le code Kazo commun (iframe, proxy, sélecteur de fichiers, raccourcis, réglages,
`DocumentSession`, exports) vient du module `Kazo.Os.AppSdk.Craft` de Kazo OS
([ADR 0259](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0259-shared-craft-app-host-module.md),
[ADR 0264](https://github.com/Kazo-ca/Os/blob/craft-exports/base/doc/fr/wasm/dev/adr/0264-craft-app-exports-reach-the-window.md)).

## Lecture de PhotoCraft (étape 0, 2026-11-13)

Lue sur `main` du fork (commit `e052eab`, 2026-11-13, version 0.23.1) et compilée depuis ce
commit. L'étiquette amont `v0.23.1` est le commit `45066c48` de la branche `release`, que le fork
n'a pas encore.

| Point | Constat |
| --- | --- |
| Build web | `apps/photocraft-web` (Trunk 0.21.14, `public_url = "./"`, profil Cargo `wasm-release`, fonctionnalité `heif`), eframe sur **wgpu** : WebGPU, repli WebGL 2 (`?webgl`), `?cpu` pour le canevas sans GPU. Rust 1.95 au minimum. Langues de `craft-i18n` intégrées au `.wasm` (révision `e02939e`, fichier `craft-i18n.rev`). |
| Mode hébergé | **Absent.** Aucune dépendance à `craft-web-host` ; `apps/photocraft-web/src/web.rs` ne connaît que le navigateur : Ouvrir = sélecteur du navigateur (`rfd`), Enregistrer = téléchargement. |
| API de commandes | **Absente** dans le build web (`window.photocraft` n'existe pas), mais tout ce qu'il faut est déjà là : `PhotocraftApp::with_control` prend une file de `ControlRequest`, la même que le serveur TCP du bureau (`docs/control-protocol.md`). Il manque seulement le branchement `craft-web-api`, comme `apps/gridcraft-web/src/api.rs` (150 lignes). |
| Canevas | `photocraft_canvas`, écrit en dur : une instance par document HTML, donc une iframe par fenêtre. La page porte aussi `#photocraft_loading` et `#photocraft_stopped` (message quand le module s'arrête, par exemple faute de mémoire). |
| Zip officiel | `photocraft-web-<version>.zip` (`packaging/web/package.sh`) : le `.wasm` y est compressé et coupé en parts de 20 Mio, démarrées par `photocraft-web-boot`. Inutile ici : sans mode hébergé, et le module Kazo attend un `.js` et un `.wasm`. On prend la sortie directe de Trunk (`dist/web`). |
| Taille | `.wasm` de **37,6 Mio** après `wasm-opt`, colle `.js` de 179 Kio (mesuré, voir « Essai »). Sous les limites de Kazo OS (128 Mio par fichier ; paquet de 64 Mio compressé, 128 Mio décompressé) et sous l'avertissement de GitHub à 50 Mo. |
| Enregistrer | Tout passe par `Services::write(path, bytes)` : le document (Enregistrer, Enregistrer sous), mais aussi les copies et exports (Enregistrer une copie, Exporter sous, Export rapide PNG, Enregistrer pour le web, calques vers fichiers) et les fichiers de préréglages (nuancier, actions, raccourcis, contours, tables de couleurs…), soit une quinzaine d'appels. La fonction ne sait pas si elle écrit le document ou une copie ; l'app, elle, le sait (`write_document(…, copy)`). |
| Nom à l'enregistrement | Un fichier ouvert dans le navigateur n'a pas de chemin (`path = None`). Enregistrer propose alors toujours `nom.psd`, même pour un JPEG à un seul calque. Avec un chemin (`menus/invoke.rs`, `file.save`) : un fichier à calques qui contient tout le document (`.pcraft`, `.psd`, `.psb`, `.tif`) est réécrit sur place ; pour une image plate, Enregistrer repasse par la demande de nom en proposant son propre fichier (`.png` pour un JPEG devenu transparent) ; si le format ne peut plus contenir le document, il propose `.psd` et l'ancien format devient une copie, comme Photoshop. |
| « Modifié » | Sur le web, `write_unconfirmed = true` : après un téléchargement, le document **reste modifié** (le navigateur ne confirme rien) et l'avertissement de fermeture de page reste armé. |
| Documents | Plusieurs documents en **onglets** dans une page (Fichier › Nouveau, Ouvrir plusieurs fichiers, glisser un calque d'un onglet à l'autre). Commande `session.inspect` : `{ active, documents[{ index, name, path, width, height, dirty, revision }] }`. Pas d'identifiant stable dans cette liste (seulement la place de l'onglet). `ui.inspect` ne liste pas les documents. |
| Deux onglets, un fichier | `detach_others_from_file` : quand un onglet enregistre sous le chemin d'un autre onglet, l'autre perd son fichier et devient « modifié ». Dans le navigateur les chemins sont de simples noms : deux fichiers de même nom venant de deux dossiers seraient pris pour le même. |
| Commandes | `engine.execute { command, params }` (sans dialogue), `ui.menu.invoke` (comme un clic de menu), `engine.commands`, `ui.inspect`, `ui.set`, `ui.screenshot`, `ui.dialog.*`. Fichier : `file.new`, `file.open`, `file.save`, `file.saveAs`, `file.saveACopy`, `file.close`, `file.closeAll`, `file.revert`, `file.export.*`, `file.print`. Documents : `session.inspect`, `document.inspect`, `document.activate`. `app.open` et `app.save` demandent des accès fichier que le web n'a pas. |
| Thème | `ui.set { theme }` : `pro` (sombre, défaut), `proMedium`, `proLightGray`, `proLight`, `studio`, `studioLight`, `classic`, `adwaita`, `adwaitaDark`, `solarizedDark`, `breezeLight`, `breezeDark`, `softDark`, `softLight`. |
| Langue | Préférence `interface.language` (`prefs.set`), `auto` par défaut (langues du navigateur). 35 langues ont un catalogue PhotoCraft : `ar az bn bs cs da de el es fi fr he hi hu id it ja ka ko nb nl pl pt-br ro ru sr-latn sv ta th tr uk ur vi zh-hans zh-hant`. |
| Sélecteur du navigateur (`rfd`) | Une dizaine d'usages : ouvrir (un ou plusieurs fichiers), importer (incorporer), fichiers d'une pile, LUT et préréglages (pinceaux `.abr`, dégradés `.grd`, styles `.asl`, nuanciers), actions, SVG d'un objet dynamique. Tous passent par le même `input type=file` : le sélecteur du module les remplace. Les demandes de **dossier** répondent déjà « rien » sur le web. |
| Formats ouverts | `pcraft psd psb psdt xcf xcfgz ora kra krz pdn af afphoto afdesign afpub` (documents à calques), `png jpg jpeg gif bmp webp tif tiff tga ico qoi exr hdr pbm pgm ppm pam pfm dds fits fit fts dpx cin dcm dicom dc3 iff lbm ilbm heic heif hif svg svgz` (images), `dng cr2 cr3 nef nrw arw pef orf rw2 raf` (RAW), et les préréglages `abr grd asl aco act ase`, qui vont aux bibliothèques au lieu d'ouvrir un document. |
| Formats enregistrés | Document : `.pcraft` (natif), `.psd`, `.psb`, `.tif` ; `.ora` s'il contient tout le document ; un format plat (`png jpg webp gif bmp tga qoi exr hdr…`) seulement pour un document plat. |
| Réglages | `localStorage`, clé `photocraft.preferences` : le module les garde par utilisateur Kazo. Les **préréglages** (pinceaux…) sont dans IndexedDB : partagés par tous les utilisateurs Kazo du même navigateur (même limite que les polices ajoutées de Kazo Word). |
| Polices | Le build web n'a que **Inter et JetBrains Mono**. D'autres polices peuvent être « servies » : un fichier `fonts/manifest.txt` **relatif à la page** (format de `craft-fonts`), chaque famille téléchargée à son premier usage. `craft-fonts` entier pèse 82 Mo (dont 55 Mo de polices CJK). Question Q4. |
| Mémoire | wasm32 : 4 Gio au plus par page, donc par fenêtre. PhotoCraft vérifie la place avant Importer, Taille de l'image et chaque enregistrement, prévient à 512 Mio du plafond, et montre « PhotoCraft stopped » si le module s'arrête. |
| Fermeture de page | Avertissement `beforeunload` s'il reste du travail ; la garde de fermeture de la fenêtre Kazo passe avant. |
| Pipette d'écran, presse-papiers, stylet | API du navigateur (`EyeDropper`, presse-papiers d'eframe, Pointer Events) : marchent dans l'iframe, que le module ouvre avec `clipboard-read` et `clipboard-write`. |
| Taille de fenêtre | Bureau : 1440 × 900, minimum 760 × 480. |
| Marque | Le nom et les logos ArtCraft sont des marques (`docs/brand/LICENSE-brand.txt`) : un build modifié doit les retirer et ne pas se présenter comme un produit ArtCraft ; « basé sur PhotoCraft de l'équipe ArtCraft » en texte est permis. Le lien « ArtCraft » de la fenêtre À propos porte le nom ; l'écran d'accueil porte trois liens vers ArtCraft et une icône à vérifier (voir « Essai »). |
| Règles du dépôt amont | `AGENTS.md` : Rust seulement, pas de `unwrap`/`panic` hors tests, tout est une commande, ne jamais casser wasm (`cargo xtask wasm`), pas de changement de CI dans une PR de fonctionnalité, pas de mention d'agent dans une PR, une issue avant une PR. |

### Essai (2026-11-13)

Fait sur le fork (`main`, e052eab, v0.23.1), dans le conteneur de cette session (4 cœurs, Linux), sans aucun changement au code.

| Point | Résultat |
| --- | --- |
| Outils | Rust stable 1.100.0, cible `wasm32-unknown-unknown`, Trunk 0.21.14, `craft-i18n` à la révision épinglée (`craft-i18n.rev`, `CRAFT_I18N_REQUIRED=1`). `craft-fonts` n'a pas été fourni. |
| Commande | `trunk build --release` dans `apps/photocraft-web` (profil `wasm-release`, fonction `heif`). |
| Durée | 13 minutes à froid : 9 min de `cargo`, puis 3 min 30 de `wasm-opt -Oz --converge`. |
| Sortie | `dist/web` : `index.html`, un `.js` de 179 Kio, un `.wasm` de 37,6 Mio (39,4 Mo ; 51,8 Mo avant `wasm-opt` ; 16,0 Mo en gzip), un dossier `snippets/`. |
| Démarrage | Chromium sans carte graphique (WebGL2 par SwiftShader, `?webgl`) : l'app démarre en moins d'une seconde depuis un serveur local et affiche son écran d'accueil. Elle se rabat seule sur le rendu processeur (« CPU Rendering » dans la barre d'état). |
| Ouvrir | Le bouton « Open… » ouvre le sélecteur de fichiers du navigateur. Un PNG de 640 × 400 s'ouvre dans un onglet. |
| Enregistrer | Ctrl+S sur ce PNG : téléchargement de `sample.psd`, sans demande, et l'onglet prend ce nom. C'est ce que H2 corrige : le fichier ouvert n'a pas de chemin. |
| API de page | `typeof window.photocraft` vaut `undefined`. |
| Stockage | Une clé `photocraft.preferences` dans le `localStorage`. |
| Console | Une requête en 404 (`fonts/manifest.txt`, polices servies absentes), sans effet. |

Deux points vus à l'écran : l'écran d'accueil porte les liens « Website », « Discord » et « GitHub » d'ArtCraft, le nom PhotoCraft et une icône en haut à gauche (H9, dans le fork) ; la fenêtre de 1280 × 820 est bien remplie, ce qui confirme une taille de départ large.

Captures : `photo/trials/` dans les fichiers du projet (hors dépôt).

## Ce qu'il faut ajouter à PhotoCraft (fork)

Le contrat est celui de `craft-web-host` (`storytold/craft-libs`, version « 1 »), le même que les
autres apps Craft, avec le préfixe `photocraft:`. Le code vit dans `apps/photocraft-web`, sur le
modèle de `apps/gridcraft-web/src/{api,host,hostmode}.rs` ; deux points demandent en plus une
petite ouverture dans `crates/ui-egui` (marqués ★), parce que la coquille web ne voit pas
aujourd'hui ce qu'il lui faut.

| # | Comportement en mode hébergé (`data-host="page"` ou `?host=parent`) | Pourquoi |
| --- | --- | --- |
| H1 | `photocraft:ready { version: "1", appVersion, file: false }` quand l'app écoute ; `photocraft:failed { error }` si elle ne démarre pas, **et quand le module s'arrête** (le gestionnaire qui montre déjà « PhotoCraft stopped »). | La fenêtre Kazo sait quand envoyer le fichier et quand montrer une erreur. |
| H2 | `photocraft:open { name, bytes }` ouvre le fichier comme Fichier › Ouvrir, dans un nouvel onglet, refusé au-delà d'une limite avant d'être copié. ★ Le document ouvert par l'hôte **garde son nom comme chemin** (`path = Some(name)`). | Avec un chemin, Enregistrer réécrit le même fichier quand son format le permet, au lieu de toujours proposer `.psd`. |
| H3 | ★ L'enregistrement **du document** (`copy == false`) part en `photocraft:save { name, bytes, saveAs, id }`. `saveAs` est faux quand le document est écrit sous son propre chemin (réécriture sur place, ou Enregistrer d'une image plate quand le nom proposé est son propre fichier), vrai dans les autres cas (Enregistrer sous, premier enregistrement, format qui ne peut plus contenir le document). | L'hôte sait s'il réécrit le fichier lié ou s'il doit demander où enregistrer. |
| H4 | Le document n'est marqué enregistré qu'à la réception de `photocraft:saved { id }` ; sans réponse, il reste modifié. | Corrige pour PhotoCraft le piège « l'app se dit propre avant l'écriture » (craft-libs #611). |
| H5 | Les **copies, exports et fichiers de préréglages** gardent le téléchargement (`<a download>`). | Le module Kazo les intercepte déjà et les remet à la fenêtre comme copies (ADR 0264) ; rien à inventer. |
| H6 | `photocraft:dirty { dirty }` quand un des documents ouverts a des modifications, ou n'en a plus. | Titre de la fenêtre. |
| H7 | `window.photocraft` (`craft-web-api` : `request`, `execute`, `commands`, `docInspect`, `uiInspect`, `screenshot`) sur la file de contrôle de l'app, installé avec `?control` comme PdfCraft et FigCraft. Un appel abandonné par la page n'est pas exécuté (`Dispatch::give_up`). | Thème, langue, liste des documents, garde de fermeture. |
| H8 | Hors mode hébergé, rien ne change (téléchargements, sélecteur du navigateur). | Le patch reste proposable en amont. |
| H9 | Le lien « ArtCraft » de la fenêtre À propos devient le texte permis par la licence de marque. En mode hébergé, l'écran d'accueil ne montre pas les liens « Website », « Discord » et « GitHub » ; l'icône en haut à gauche est vérifiée contre `docs/brand/` et retirée si c'est une marque. | Build modifié. |

Tests demandés dans le fork : tests unitaires des règles pures (nom par défaut, `saveAs`,
limite de taille), `cargo clippy -D warnings`, `cargo xtask layers`, `cargo xtask wasm`, un
build Trunk, et un parcours Playwright sur une page d'essai qui joue l'hôte (`open`, `save`,
`saved`, `dirty`, `failed`, `window.photocraft.request`).

## Décisions

| # | Sujet | Décision |
| --- | --- | --- |
| 1 | Nom et identité | « **Kazo Photo** ». Id `ca.kazo.photo`, éditeur `pub.kazo`, assembly `Kazo.Photo`, catégorie de Kazo Fig, plusieurs fenêtres, fenêtre de 1280 × 820, contenu minimum 760 × 480. |
| 2 | Code commun | Module `Kazo.Os.AppSdk.Craft`. Version minimale : **la version de Kazo OS qui contient `craft-exports/base`** (ADR 0264), parce que H5 en dépend ; pas encore publiée, Benoit en choisit le moment. D'ici là, le sous-module de l'OS suit `craft-exports/base`, comme Kazo Pdf. |
| 3 | Changement d'écran | `canChangeScreen: false` dans le manifeste dès le chantier 1 (issue [#2](https://github.com/Kazo-ca/Os-App-Photo/issues/2), ADR 0263). |
| 4 | Provenance du build | Toujours compilé depuis le fork : branche `kazo/v0.23.1` (= étiquette amont `v0.23.1`, commit `45066c48`), branche d'intégration `kazo/base` créée d'elle, qui reçoit les chantiers du fork. Sous-module `external/photocraft` sur `kazo/base`. Sortie de Trunk (`dist/web`) copiée dans `wwwroot/photocraft/` avec `photocraft.json` (version, commit, noms et SHA-256), dans le dépôt : le Store ne compile pas de Rust. |
| 5 | Page de l'iframe | `wwwroot/host/index.html` : canevas `photocraft_canvas` avec `data-host="page"`, `data-craft-app="photocraft"`, `data-craft-build="../photocraft/photocraft.json"`, plus les éléments `#photocraft_loading` et `#photocraft_stopped` de la page amont. Paramètre `control` pour `window.photocraft`. |
| 6 | Enregistrement | `Saving` : règle du module (`DocumentSession.Decide(name, saveAs)`), la fenêtre renvoie `true` seulement quand le fichier est écrit, ce qui déclenche `saved` (H4). `Exporting` : toujours une copie (`DecideCopy`). |
| 7 | Deux fichiers de même nom | Un nom n'est lié qu'une fois par fenêtre. Ouvrir un fichier dont le nom est déjà lié dans cette fenêtre l'ouvre dans une **nouvelle fenêtre** : PhotoCraft prendrait sinon les deux onglets pour le même fichier. |
| 8 | Garde de fermeture | Lue dans `session.inspect` (`documents[{ index, name, path, dirty }]`) ; la fenêtre active chaque document modifié (`document.activate`), lance `file.save` et attend la fin de ses écritures. |
| 9 | Thème et langue | `ui.set { theme }` : `pro` pour le thème sombre de l'OS, `proLight` pour le clair, à chaque changement. Langue : `prefs.set` de `interface.language` au démarrage (`CraftLanguage.Pick` sur les 35 codes). |
| 10 | Raccourcis | `ReservedKeys.Os` **sans** `Ctrl+N` : dans un éditeur d'images à onglets, `Ctrl+N` est « Nouveau document » de PhotoCraft. Les conflits entre les accords de l'OS (`Alt+F3`…`F11`, `Alt+Espace`) et ceux de PhotoCraft sont relevés au chantier 2 et notés dans la fiche. |
| 11 | Grands fichiers | La limite de Kazo OS (128 Mio par fichier, ADR 0245) s'applique ; un fichier plus grand est refusé avec un message. Le chantier 2 mesure l'ouverture et l'enregistrement d'un `.psd` de 50 à 100 Mo (temps, mémoire) et note le résultat. |
| 12 | Piste amont | En parallèle du fork : les textes des issues (en anglais, sans mention d'agent) sont préparés dans `/mnt/project-files/photo/upstream/` ; Benoit les publie. Les branches du fork sont écrites pour pouvoir devenir des PR amont. |
| 13 | ADR de Kazo OS | Aucune prévue : le module a déjà ce qu'il faut. Si un chantier en demande une, prochain numéro libre **0267** au 2026-11-13 (à revérifier sur `main`, les PR ouvertes et toutes les branches `*/base` au moment de l'écrire). |

## Questions posées à Benoit

Posées dans le fil le 2026-11-13. **Benoit a répondu A aux quatre questions le 2026-11-13** : mode hébergé dans notre fork et proposé en amont (Q1), onglets de PhotoCraft et nouvelle fenêtre au double-clic (Q2), app par défaut pour ce que rien d'autre n'ouvre (Q3), polices latines de `craft-fonts` servies à la demande (Q4).

**Q1. Où vit le mode hébergé de PhotoCraft ?**

- **A (recommandée)** : dans notre fork maintenant, et proposé en amont en parallèle (Claude
  prépare l'issue et les branches, Benoit publie). Kazo Photo n'attend personne ; si l'amont
  accepte, le fork redevient une simple copie.
- B : en amont seulement. Rien à maintenir, mais Kazo Photo attend la réponse et une version
  d'amont, sans date.
- C : dans le fork seulement, sans rien proposer. Chaque montée de version de PhotoCraft
  demande de reporter le patch, pour toujours.

**Q2. Plusieurs images dans une fenêtre ?**

- **A (recommandée)** : on garde les onglets de PhotoCraft. Une fenêtre Kazo Photo tient
  plusieurs images, chaque onglet lié à son fichier (comme Kazo Fig). Un double-clic sur un
  fichier dans l'explorateur ouvre une nouvelle fenêtre. On garde le glisser de calques entre
  images, et on évite de payer une instance entière de PhotoCraft (plusieurs centaines de Mo de
  mémoire) par image.
- B : une image par fenêtre, comme Kazo Word. Règle d'enregistrement plus simple, mais chaque
  image coûte une instance, et les fonctions entre documents perdent leur sens.
- C : comme A, et un double-clic dans l'explorateur ajoute un onglet à la fenêtre déjà ouverte.
  Plus proche de Photoshop, mais demande à l'OS de router une ouverture vers une fenêtre
  existante : un chantier de plus dans `Kazo-ca/Os`.

**Q3. Formats associés à Kazo Photo.** Kazo Paint ouvre déjà `.png .jpg .jpeg .bmp .gif .webp`.

- **A (recommandée)** : app par défaut pour ce que rien d'autre n'ouvre : documents à calques
  (`.pcraft .psd .psb .xcf .ora .kra .pdn`, fichiers Affinity), RAW, et les images que Kazo
  Paint ne lit pas (`.tif .tiff .tga .exr .hdr .heic .heif .dds .qoi .ico`…). « Ouvrir avec »
  seulement pour `.png .jpg .jpeg .bmp .gif .webp .svg` : un double-clic sur une photo ouvre
  toujours l'app légère.
- B : Kazo Photo par défaut pour toutes les images, une fois installé. Un double-clic sur un
  PNG lance alors un éditeur lourd qui met plusieurs secondes à démarrer.
- C : par défaut seulement pour `.pcraft .psd .psb`, tout le reste en « Ouvrir avec ».

**Q4. Polices de l'outil Texte.** Le build web n'a que Inter et JetBrains Mono.

- **A (recommandée)** : livrer dans le paquet les polices latines de `craft-fonts` (environ
  25 Mo, licences OFL), servies à la demande : une famille n'est chargée qu'à son premier
  usage. Pas de polices CJK dans la v1 (55 Mo de plus).
- B : aucune police de plus dans la v1 ; limite écrite dans la fiche.
- C : tout `craft-fonts` (82 Mo). Le paquet dépasserait 100 Mio décompressé, près de la limite
  de 128 Mio de Kazo OS.

## Chantiers

Deux dépôts : le fork `Kazo-ca/photocraft` (Rust) et `Kazo-ca/Os-App-Photo` (C#). Dans chacun,
une branche d'intégration, une branche et une PR par chantier ; un fil d'implémentation par
chantier. Benoit fusionne `photo/base` dans `main` ; `main` du fork reste la copie de l'amont.

| N° | Dépôt, branche (vers) | Contenu | Dépend de |
| --- | --- | --- | --- |
| 0 | Os-App-Photo, `photo/0-plan` (`photo/base`) | ce plan (fr et en) | — |
| F1 | photocraft, `kazo/1-web-api` (`kazo/base`) | H7 : `window.photocraft` | plan fusionné |
| F2 | photocraft, `kazo/2-web-host` (`kazo/base`) | H1 à H6, H8, H9 : mode hébergé | F1 (mêmes fichiers) |
| 1 | Os-App-Photo, `photo/1-solution` (`photo/base`) | solution, manifeste, compilation depuis le fork, page d'iframe, fenêtre qui démarre | plan fusionné ; « terminé » attend F2 |
| 2 | Os-App-Photo, `photo/2-window` (`photo/base`) | ouvrir, enregistrer, onglets, exports, garde de fermeture, sélecteur, raccourcis, thème, langue, réglages, polices (Q4) | 1 et F2 |
| 3 | Os-App-Photo, `photo/3-store` (`photo/base`) | fiche, README, licences, mesures, tag, catalogue, `features.md` | 2, et la version de Kazo OS qui contient `craft-exports/base` |

**Ordre** : F1 puis F2 dans un même fil (ils touchent les mêmes fichiers) ; le chantier 1 dans un
deuxième fil, en même temps ; puis 2 ; puis 3.

### F1. `window.photocraft` (fork)

- Créer `kazo/v0.23.1` sur le commit amont `45066c48` (`git fetch https://github.com/storytold/photocraft refs/tags/v0.23.1`), puis `kazo/base` de cette branche.
- `apps/photocraft-web/src/api.rs` sur le modèle de `apps/gridcraft-web/src/api.rs` : une file `ControlRequest` donnée à l'app par `PhotocraftApp::with_control`, `craft_web_api::page::PageApi::new("photocraft", …).install()`, installé avec `?control`. Un appel que la page abandonne libère la requête (`Dispatch::give_up`).
- `craft-web-api` épinglé à une révision de `storytold/craft-libs`, comme les autres dépendances `craft-*` du `Cargo.toml` racine.
- **Terminé quand** : dans une page servie par `trunk serve`, `await window.photocraft.request("engine.execute", { command: "session.inspect" })` répond, `ui.set { theme: "proLight" }` change le thème, et les contrôles de la section « Ce qu'il faut ajouter » passent.

### F2. Mode hébergé (fork)

- `apps/photocraft-web/src/host.rs` et `hostmode.rs` sur le modèle de GridCraft : `Host::connect("photocraft", canvas)`, écoute de `open` et `saved`, `post_ready`, `post_save`, `post_dirty`, `post_failed`.
- Les deux ouvertures ★ dans `crates/ui-egui`, les plus petites possibles et utiles hors de Kazo : un fichier remis avec son chemin (H2), et l'écriture du document distinguée de l'écriture d'une copie (H3), avec la confirmation (H4) à la place de `write_unconfirmed` en mode hébergé. Lire `app/files.rs` (`save_dialog_flow`, `write_document`, `saved`, `saves_copy`) et `file_open.rs` avant de choisir la forme.
- H9, et une ligne dans `docs/development.md` (« Web build ») qui décrit le mode hébergé.
- **Terminé quand** : la page d'essai qui joue l'hôte ouvre un PNG et un PSD, reçoit `save` avec le bon `saveAs` dans les quatre cas (réécriture d'un PSD, PNG resté plat, PNG devenu à calques, Enregistrer sous), le document ne redevient propre qu'après `saved`, Exporter sous télécharge toujours, et la même page sans `data-host` se comporte comme avant.

### 1. Solution et compilation (`photo/1-solution`)

- Solution renommée (`Kazo.Photo`, `Kazo.Photo.Os`, `Kazo.Photo.Tests`), `[AppManifest]` de la décision 1, `canChangeScreen: false`, capacités de Kazo Fig, associations selon Q3.
- Sous-module `external/KazoOs` sur `craft-exports/base` (CI : `KAZOOS_BRANCH`), sous-module `external/photocraft` sur `kazo/base`.
- `tools/build-photocraft.sh` (Rust stable du jour, cible `wasm32-unknown-unknown`, Trunk 0.21.14, `craft-i18n` à la révision de `craft-i18n.rev`, `CRAFT_I18N_REQUIRED=1`, `trunk build --release` dans `apps/photocraft-web`), `tools/vendor-photocraft.sh --dir`, workflow `build-photocraft.yml`, test xUnit « le commit de `photocraft.json` est celui du sous-module ». Modèles : `Os-App-Fig` et `Os-App-PDF`.
- Page d'iframe (décision 5) et fenêtre sur `CraftFrame` et `FrameStartup` : chargement, app, erreur, « Réessayer ».
- Tant que F2 n'est pas fusionné, compiler `kazo/v0.23.1` pour roder l'outillage ; le parcours qui attend `ready` vient avec F2.
- **Terminé quand** : l'app démarre en WebGPU et en WebGL 2, deux fenêtres tournent ensemble, le paquet `.kazoapp` s'installe dans un Kazo OS publié ; taille du paquet et mémoire par fenêtre notées ici.

**Fait le 2026-11-13** (PR `photo/1-solution` → `photo/base`), avec le fork encore sur `kazo/v0.23.1` (F2 pas fusionné) :

| Point | Résultat |
| --- | --- |
| Solution | `Kazo.Photo.slnx` : `Kazo.Photo` (app), `Kazo.Photo.Os` (hôte), `Kazo.Photo.Tests` (xUnit), `tests/e2e` (Playwright). |
| Manifeste | `ca.kazo.photo` 0.1.0, `pub.kazo`, `graphics`, plusieurs fenêtres, 1280 × 820, minimum 762 × 520 (760 × 480 de contenu plus le cadre), `canChangeScreen: false`, capacités de Kazo Fig, version minimale 3.3.3. |
| Associations (Q3 = A) | `open` et `edit` pour les documents à calques, les RAW, les images que Kazo Paint ne lit pas et les préréglages (`.abr .grd .asl .aco .act .ase`) ; `open` seul pour `.png .jpg .jpeg .bmp .gif .webp .svg`. `.svgz` et `.svg` ont deux types de média, pour qu'un type ne mélange pas les deux cas (`Documents/PhotoFormats.cs`). |
| Sous-modules | `external/KazoOs` sur `craft-exports/base` (`c91effd`, CI : `KAZOOS_BRANCH`, `KAZOOS_USE_SOURCE`), `external/photocraft` sur `kazo/v0.23.1` (`45066c48`). |
| Compilation | `tools/build-photocraft.sh` : Trunk 0.21.14 directement (pas `packaging/web/package.sh`, qui coupe le `.wasm` en parts), `craft-i18n` obligatoire à la révision de `craft-i18n.rev`. 16 minutes à froid ici (4 cœurs, Rust 1.97.0). Workflow `build-photocraft.yml` sur le modèle de Kazo Pdf. |
| Build | `.wasm` de 39 250 977 octets (37,4 Mio), `.js` de 182 Ko, plus les licences (MIT, Apache, `NOTICE`, `ATTRIBUTION.md`, OFL d'Inter et de JetBrains Mono). Paquet `.kazoapp` (`-t:KazoOsPack`) : **16,2 Mo**. |
| Fenêtre | `CraftFrame` et `FrameStartup` : écran de chargement, erreur avec « Réessayer » au bout de 60 s, thème (`pro`, `proLight`) et langue (`prefs.set interface.language`) à `ready`, `ReservedKeys.Os` sans `Ctrl+N` (décision 10). Page d'iframe avec `#photocraft_loading` et `#photocraft_stopped` (`host/photocraft-page.css`). |
| Navigateur | Dans Kazo OS (Chromium sans carte graphique), PhotoCraft démarre dans l'iframe de chaque fenêtre, deux fenêtres ensemble, et dessine son écran d'accueil en WebGL 2 (`--webgl`). En WebGPU logiciel (SwiftShader), PhotoCraft perd son périphérique juste après le démarrage (« A valid external Instance reference no longer exists ») : limite de ce navigateur, **à vérifier sur une vraie carte graphique**. |
| Mémoire | Processus de rendu de Chromium, sans image ouverte : 768 Mio l'OS seul, 886 Mio avec une fenêtre Kazo Photo, 930 Mio avec deux. À refaire avec un `.psd` au chantier 2 (décision 11). |
| Tests | 69 tests xUnit ; 3 parcours Playwright verts, le 4ᵉ (« la fenêtre montre PhotoCraft une fois prêt ») marqué `todo` jusqu'à F2. |

**Suite le 2026-11-13, après F2** (PR `photo/1b-host-mode-build` → `photo/base`) : `external/photocraft` passe sur `kazo/base` (`16ae9d59`, mode hébergé de F2), build recompilé (`.wasm` de 39 297 323 octets, `.js` de 193 Ko). PhotoCraft envoie `photocraft:ready` : la fenêtre quitte l'écran de chargement et montre PhotoCraft. Le parcours `ready` n'est plus `todo` : 4 parcours Playwright verts, 69 tests xUnit.

**Reste, non vérifiable ici** : WebGPU sur une vraie carte graphique et l'installation du `.kazoapp` dans un Kazo OS publié (celle-ci attend une version de Kazo OS qui contient `craft-exports/base`, chantier 3).

### 2. Fenêtre (`photo/2-window`)

- Ouverture : fichier demandé au lancement, `OpenAsync`, décision 7. Préréglages (`.abr .grd .asl .aco .act .ase`) : envoyés à PhotoCraft sans liaison.
- Enregistrement : décision 6 ; liste de formats (`CraftDocumentFormats`) ; les quatre cas de F2 de bout en bout dans Kazo OS ; un dialogue annulé ou une écriture refusée laisse le document modifié.
- Onglets selon Q2 : liaison de chaque nom à son chemin, titre de la fenêtre sur l'onglet actif, garde de fermeture (décision 8).
- Exports comme copies (`Exporting`), sélecteur de fichiers (`PickRequested`, un ou plusieurs fichiers), raccourcis (décision 10), thème et langue (décision 9), réglages par utilisateur (`WebStorageFile`), polices servies selon Q4 (`host/fonts/manifest.txt`, licences dans `THIRD-PARTY-NOTICES`).
- Mesure de la décision 11.
- **Terminé quand** : aucun cas ne perd de modifications sans que l'utilisateur l'ait choisi, aucun dialogue ni téléchargement du navigateur ne sort de l'app, et les raccourcis de l'OS marchent le curseur dans l'image.

### 3. Store (`photo/3-store`)

- Icône sans logo ArtCraft, fiche fr et en (« basé sur PhotoCraft de l'équipe ArtCraft », limites connues : 128 Mio par fichier, mémoire, préréglages partagés entre utilisateurs, polices), captures, README refait sur la fiche (`store:readme`, « Essayer » avec `?autolaunch=ca.kazo.photo`), `THIRD-PARTY-NOTICES`.
- Sous-module de l'OS sur l'étiquette de la version qui contient `craft-exports/base`, `minimumVersion`, parcours dans un Kazo OS publié, workflow « Tag a release », PR au catalogue `Os-AppMarketPlace`, ligne de `features.md` dans `Kazo-ca/Os`.
- **Terminé quand** : Kazo Photo s'installe depuis le Store, ouvre un `.psd` par double-clic et l'enregistre.

## Piste en amont

Brouillons en anglais, publiés par Benoit, dans `/mnt/project-files/photo/upstream/` :

- PhotoCraft : mode hébergé du build web (`craft-web-host`), avec les comportements H1 à H6 ;
- PhotoCraft : `window.photocraft` (`craft-web-api`) ;
- `craft-libs` : se joindre à #611 (confirmation d'enregistrement) et #610 (`open-request`).

Non vérifié : les issues déjà ouvertes en amont sur ces sujets (`storytold` est hors de portée de
la session) ; Benoit regarde avant de publier. DesignCraft et VectorCraft ont le même manque : ce
qui est fait ici leur sert de modèle.

## Avancement

- [x] Étape 0 : lecture de PhotoCraft 0.23.1, compilation et essai du build web.
- [x] Plan fr et en.
- [x] Réponses de Benoit à Q1 à Q4 : A partout (2026-11-13).
- [x] F1, F2 (fork, 2026-11-13) : `window.photocraft` avec `?control` (Kazo-ca/photocraft#1) et mode hôte craft-web-host (Kazo-ca/photocraft#2), fusionnés dans `kazo/base`. Commit à épingler : `16ae9d59ecbaf987a26427b66edff708463b5c1f`. Vérifié dans Chromium headless (`?webgl`) avec une page hôte de test : 22 contrôles sur 22 (ready, open, dirty, save avec le bon `saveAs`, saved, copie téléchargée, Blob refusé, H9). Le clippy wasm de `photocraft-web` garde une erreur antérieure (`wildcard_imports` dans `preset_bridge.rs`, code amont).
- [x] Chantier 1 : solution, manifeste, compilation depuis le fork, page d'iframe, fenêtre (2026-11-13), fork sur `kazo/base` avec le mode hébergé.
- [ ] Chantiers 2 et 3.
