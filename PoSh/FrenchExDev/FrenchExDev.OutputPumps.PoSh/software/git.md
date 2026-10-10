# Git

Git gère les commits, lʼindex et les modifications du répertoire de travail. Ces pompes sont en lecture seule et ne font aucun fetch : ahead/behind décrit uniquement les références locales connues.

Status utilise `--porcelain=v2 --branch -z --untracked-files=all`. Les chemins sont groupés par état (Staged, Unstaged, Untracked, Conflict) et les renommages conservent les deux noms. `Path` réduit le périmètre ; une capture trop large exige un chemin plus précis. Les branches détachées ou sans commit restent identifiables.

Diff utilise `--numstat -z`, sans pager, textconv ni diff externe. `Staged` sélectionne lʼindex, `Revision` une base explicite. Les fichiers binaires nʼont pas de compteurs de lignes. Pour inspecter du contenu, `Get-PumpGitChanges` exige un chemin et renvoie des lignes changées structurées, pas le patch brut. Log demande un élément supplémentaire pour détecter la troncature ; le total de lʼhistorique peut rester inconnu. Les sujets sont assainis ; auteurs et emails ne sont pas exportés.

Version vérifiée : Git 2.55.0.windows.3, commandes locales vérifiées le 2026-10-10.

Documentation officielle :

- <https://git-scm.com/docs/git-status>
- <https://git-scm.com/docs/git-diff>
- <https://git-scm.com/docs/git-log>

Module : `Modules/Git.psm1`.

Appels disponibles :

- Route : `git status` ; identifiant `git.status`.
- `Get-PumpGitStatus` : Branche, ahead/behind et chemins groupés.
  Paramètres du besoin : `WorkingDirectory, Path`.
  Exemple : `Get-PumpGitStatus -WorkingDirectory $repo -MaxItems 10`.
- Route : `git diff` ; identifiant `git.diff`.
- `Get-PumpGitDiff` : Statistiques des changements par chemin.
  Paramètres du besoin : `WorkingDirectory, Path, Staged, Revision`.
  Exemple : `Get-PumpGitDiff -WorkingDirectory $repo -Path 'src/'`.
- Route : `git diff --patch` ; identifiant `git.changes`.
- `Get-PumpGitChanges` : Lignes changées dʼun chemin ciblé.
  Paramètres du besoin : `WorkingDirectory, Path, Staged`.
  Exemple : `Get-PumpGitChanges -WorkingDirectory $repo -Path 'src/app.cs'`.
- Route : `git log` ; identifiant `git.log`.
- `Get-PumpGitLog` : Historique limité, sujets et dates.
  Paramètres du besoin : `WorkingDirectory, Path, Revision`.
  Exemple : `Get-PumpGitLog -WorkingDirectory $repo -MaxItems 5`.

Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.
