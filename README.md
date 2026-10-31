# Ergalo · Relevé de vos outils

Petit programme Windows, sans installation, qui dresse la liste des outils de travail d'un PC (logiciels installés, extensions et sites web les plus utilisés) et l'enregistre dans un fichier que la personne relit, puis dépose elle-même dans son questionnaire [Ergalo](https://ergalo.fr). Il sert au diagnostic du parc digital d'une entreprise : savoir quels outils sont vraiment utilisés, pour repérer les doublons et le temps perdu.

Le code est public pour que chacun puisse vérifier ce que fait le programme avant de le lancer. [English summary below.](#english-summary)

## Ce que fait le programme

- Il lit la liste des logiciels installés et des programmes lancés au démarrage de Windows.
- Il lit le nom des extensions installées dans les navigateurs.
- Il lit l'historique des 90 derniers jours des navigateurs du PC (famille Chromium : Chrome, Edge, Brave, Opera, Vivaldi, Arc… ; famille Firefox : Firefox, Waterfox, LibreWolf, Zen, Floorp). Il travaille sur une copie, effacée aussitôt, et ne garde de chaque adresse que le **nom du site** (`notion.so`) et un nombre de visites.
- Il compare ce qu'il trouve au catalogue des outils d'Ergalo ([`catalogue.tsv`](catalogue.tsv), embarqué dans le programme). Les outils reconnus sont cochés d'office ; les autres sont proposés décochés.
- Il montre le fichier **mot pour mot** avant de l'enregistrer sur le PC (`ergalo-outils-AAAA-MM-JJ.json`). Seules les lignes laissées cochées y figurent.

## Ce qu'il ne fait pas

- **Il ne se connecte jamais à Internet.** Le code ne contient aucune fonction réseau ; la CI de ce dépôt le vérifie à chaque modification.
- Il ne lit ni mots de passe, ni cookies, ni formulaires, ni fichiers personnels, ni le contenu ou le titre des pages.
- Il n'écrit dans le fichier ni adresse de page complète, ni chemin de dossier, ni nom d'utilisateur Windows.
- Il n'envoie rien : c'est la personne qui dépose le fichier dans son questionnaire.
- Il ne demande aucun droit d'administrateur (manifeste `asInvoker`) et n'installe rien.

## Pourquoi Windows ou l'antivirus peuvent se méfier

Windows SmartScreen juge un programme sur sa signature et sur sa réputation (le nombre de PC qui l'ont lancé sans incident). Un programme récent, peu téléchargé, part sans réputation. Et lire l'historique des navigateurs ressemble, vu de loin, à ce que font les logiciels malveillants : c'est pour cela que tout le code est ici, lisible. La signature du programme est en cours (voir la [politique de signature](POLITIQUE-DE-SIGNATURE.md)).

## Vérifier l'exécutable

Chaque [version publiée](https://github.com/Ergalo/scanner-parc/releases) est compilée par la CI de ce dépôt ([`.github/workflows/construire.yml`](.github/workflows/construire.yml)), jamais sur un poste de travail. Elle porte :

- l'exécutable seul (`ergalo-releve.exe`) et le même dans un zip (`ergalo-releve.zip`), certains navigateurs bloquant moins un zip ;
- l'empreinte SHA-256 de l'exécutable (`SHA256.txt`), à comparer avec celle du fichier téléchargé : dans PowerShell, `Get-FileHash .\ergalo-releve.exe` ;
- une attestation de provenance GitHub, qui prouve que le fichier sort de ce code : `gh attestation verify ergalo-releve.exe --repo Ergalo/scanner-parc`.

Le questionnaire d'Ergalo renvoie vers ces fichiers, inchangés : c'est le même programme pour tout le monde. Le fichier qu'il produit se dépose sur la page du questionnaire, ouverte par le lien privé du client ; c'est elle qui sait à quel questionnaire il appartient. Jusqu'à la version 1.1.1, le programme portait un code d'accès dans son nom et refusait de tourner sans lui : c'est retiré depuis la version 1.2.0.

## Code signing policy

Signature gratuite demandée à SignPath Foundation : Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org) (une fois accordée). Équipe, règles et vie privée : [politique de signature](POLITIQUE-DE-SIGNATURE.md).

## Pour les développeurs

- Code : [`src/`](src) (C# 5, .NET Framework 4.8 livré avec Windows 10 et 11 ; SQLite par `winsqlite3.dll`, livrée avec Windows). Aucune dépendance extérieure.
- Compiler et tester : `powershell -ExecutionPolicy Bypass -File construire.ps1`. Produit `dist/ergalo-releve.exe` et `dist/SHA256.txt`. Les tests tournent sur un PC fictif fabriqué dans un dossier temporaire, jamais sur les navigateurs de la machine.
- Essai à la main, sur un PC fictif : `obj\tests.exe --fabriquer <dossier>`, puis lancer `dist\ergalo-releve.exe --essai <dossier>`.
- Publier une version : changer la version dans [`src/Programme.cs`](src/Programme.cs), fusionner, puis poser l'étiquette `vX.Y.Z`. Chaque nouvelle compilation repart de zéro en réputation auprès de Windows : on publie le moins souvent possible.
- Signaler une faille : [SECURITY.md](SECURITY.md).

## English summary

**Ergalo · Relevé de vos outils** is a small, portable Windows program used by [Ergalo](https://ergalo.fr), a French digital-workplace consultancy, to inventory the work tools of a PC: installed software, startup programs, browser extensions, and the host names of the websites visited in the last 90 days (Chromium and Firefox family browsers, all profiles). It matches them against Ergalo's public tool catalogue ([`catalogue.tsv`](catalogue.tsv)), shows the user the exact file before saving it locally, and **never connects to the Internet**: the user uploads the file themselves to their Ergalo questionnaire. It reads no passwords, cookies, form data, page titles or full URLs, writes no user name or folder path, and requires no administrator rights.

Releases are built by GitHub Actions from this repository only, with a published SHA-256 hash and a GitHub build provenance attestation. Everyone gets the same file (also offered as a zip); the user uploads its output from their private questionnaire link, which is what ties it to them. Licensed under the [MIT License](LICENSE). Code signing policy: [POLITIQUE-DE-SIGNATURE.md](POLITIQUE-DE-SIGNATURE.md).
