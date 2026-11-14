# Kazo Photo

Un éditeur d'images à calques pour Kazo OS. Kazo Photo est basé sur
[PhotoCraft](https://github.com/storytold/photocraft) de l'équipe ArtCraft, un éditeur d'images
libre écrit en Rust, qui tourne ici dans une fenêtre de Kazo OS.

## Retoucher des images

- Calques, groupes, masques de fusion et modes de fusion ; calques de réglage (Niveaux, Courbes,
  Teinte/Saturation, Vibrance, Noir et blanc, Courbe de transfert de dégradé…) qui gardent les
  pixels d'origine.
- Styles de calque : ombres, lueurs, biseau, contour, incrustations de couleur, de dégradé et de
  motif.
- Sélections : rectangle et ellipse, lassos, baguette magique, sélection rapide, Sélectionner et
  masquer.
- Pinceaux, outils de retouche, dégradés, calques de texte et formes vectorielles.
- Filtres avec aperçu en direct : flou, netteté, déformation, bruit, et d'autres.
- Ouvre et enregistre les fichiers à calques `.psd` et `.psb`, et lit beaucoup d'autres formats :
  PNG, JPEG, WebP, TIFF, GIF, BMP, SVG, OpenRaster, Krita, GIMP, fichiers RAW d'appareils photo,
  HEIC, EXR et d'autres.
- Plusieurs fenêtres à la fois, chacune avec ses images en onglets.

## Vos fichiers Kazo OS

- Double-cliquez sur un `.psd`, une photo RAW ou une autre image à calques dans l'Explorateur de
  fichiers : elle s'ouvre dans Kazo Photo. Les images PNG, JPEG, GIF, BMP, WebP et SVG le proposent
  dans « Ouvrir avec ».
- Enregistrer réécrit le fichier ouvert. Enregistrer sous, et le premier enregistrement d'une
  nouvelle image ou d'une image plate qui a maintenant des calques, passent par le dialogue
  d'enregistrement de Kazo OS.
- Enregistrer une copie et Exporter sous passent par le dialogue « Enregistrer une copie » de
  Kazo OS : l'image reste liée à son fichier. Rien ne part dans les téléchargements du navigateur.
- Fermer la fenêtre demande avant de perdre des modifications, pour chaque image ouverte.
- Fichier › Ouvrir, et les fichiers à importer ou à placer, viennent des fichiers Kazo OS, par le
  dialogue de Kazo OS. Ouvrir un fichier dont le nom est déjà pris dans une fenêtre ouvre une
  nouvelle fenêtre.
- L'outil Texte propose une quinzaine de familles de polices (Roboto, Open Sans, Lato, Montserrat,
  Liberation, Source Serif…), livrées avec l'app sous SIL Open Font License.
- Kazo Photo suit le thème (clair ou sombre) et la langue de l'OS ; ses réglages sont gardés par
  utilisateur Kazo OS dans `~/.config/ca.kazo.photo/`.

## Limites connues

- **PhotoCraft est en alpha.** La plupart des images s'ouvrent et s'enregistrent bien, mais gardez
  une copie des plus importantes.
- Kazo OS ouvre les fichiers jusqu'à 128 Mio. Les grandes images sont lentes à ouvrir et à
  enregistrer (environ 40 secondes pour un `.psd` de 50 Mo), et chaque fenêtre demande beaucoup de
  mémoire (environ 600 Mo, davantage avec de grandes images). Sur une grande image entière,
  certains filtres s'arrêtent avec « not enough working memory ».
- Les pinceaux, dégradés, motifs et autres préréglages sont gardés par le navigateur, partagés par
  tous les utilisateurs Kazo OS de ce navigateur.
- Aucune police chinoise, japonaise ou coréenne n'est livrée ; ce texte peut s'afficher en carrés.
- PhotoCraft dessine avec WebGPU ou WebGL 2 : un navigateur ou un ordinateur sans l'un ni l'autre
  montre un écran d'erreur au lieu de l'image.
- Deux raccourcis de PhotoCraft appartiennent à Kazo OS : Alt+F9 (passez par Fenêtre › Actions) et
  Alt+Espace maintenu pour dézoomer (utilisez Ctrl+-).
- Après Enregistrer sous, l'onglet garde le nom proposé par PhotoCraft ; le titre de la fenêtre
  montre le fichier choisi.
- La fenêtre reste sur l'écran où elle s'est ouverte : la passer sur un autre écran rechargerait
  PhotoCraft et perdrait les images ouvertes.
- Le téléchargement fait environ 25 Mo (59 Mo une fois installé), et chaque fenêtre charge sa
  propre copie de PhotoCraft.
- Demande Kazo OS 3.3.4 ou plus récent.

## Licence

Kazo Photo est un logiciel libre sous licence MIT. PhotoCraft est sous licence MIT ou Apache-2.0 ;
ses avis, les licences des icônes et données qu'il contient, et celles des polices (SIL Open Font
License 1.1) sont livrés avec l'app (voir `THIRD-PARTY-NOTICES-fr.md` dans le dépôt). Le nom et les
logos ArtCraft sont des marques de l'équipe ArtCraft et ne sont pas utilisés par Kazo Photo.
