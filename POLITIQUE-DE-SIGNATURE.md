# Politique de signature du code · Code signing policy

## En français

**État** : demande de signature gratuite en préparation auprès de SignPath Foundation (programme pour les projets open source). Tant qu'elle n'est pas accordée, les versions publiées ne sont pas signées ; leur empreinte SHA-256 et leur attestation de provenance GitHub permettent déjà de les vérifier.

Une fois la signature accordée :

- Seuls les exécutables compilés par la CI de ce dépôt (GitHub Actions, fichier [`.github/workflows/construire.yml`](.github/workflows/construire.yml)), à partir d'une étiquette `vX.Y.Z` de la branche `main`, sont envoyés à la signature. Aucun fichier compilé sur un poste de travail n'est signé.
- Chaque demande de signature est approuvée à la main par une personne de l'équipe ci-dessous.
- Le programme signé est exactement celui de ce code, sans composant propriétaire ni binaire extérieur : il n'utilise que .NET Framework 4.8 et `winsqlite3.dll`, livrés avec Windows.

**Équipe**

| Rôle | Personnes |
|---|---|
| Auteurs et relecteurs (committers and reviewers) | Guillaume Clerc, fondateur d'Ergalo ; les modifications passent par une proposition de modification relue et une CI verte |
| Approbation des signatures (approvers) | Guillaume Clerc |

**Vie privée** : ce programme ne transmet aucune information à un autre système en réseau. Il ne se connecte jamais à Internet ; le fichier qu'il produit reste sur le PC, et c'est la personne qui le dépose elle-même dans son questionnaire Ergalo. Politique de confidentialité d'Ergalo : <https://ergalo.fr/confidentialite>.

## In English

**Status**: a free code signing request to SignPath Foundation is being prepared (open source program). Until it is granted, releases are unsigned; their SHA-256 hash and GitHub build provenance attestation already allow verification.

Once granted:

- Only executables built by this repository's CI (GitHub Actions, [`.github/workflows/construire.yml`](.github/workflows/construire.yml)) from a `vX.Y.Z` tag on `main` are submitted for signing. Nothing built on a workstation is ever signed.
- Every signing request is manually approved by a team member listed above.
- The signed program is built from this source only, with no proprietary component or third-party binary: it relies solely on .NET Framework 4.8 and `winsqlite3.dll`, both shipped with Windows.

**Team**: committers, reviewers and approvers are listed in the table above (Guillaume Clerc, founder of Ergalo).

**Privacy**: this program will not transfer any information to other networked systems. It never connects to the Internet; the file it produces stays on the PC until the user uploads it themselves to their Ergalo questionnaire. Ergalo privacy policy: <https://ergalo.fr/confidentialite>.
