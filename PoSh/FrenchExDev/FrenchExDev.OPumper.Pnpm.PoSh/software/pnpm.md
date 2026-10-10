# pnpm

pnpm gère les dépendances et workspaces Node. Cette première pompe inspecte les dépendances installées avec `list --json --depth`, sans installation, réparation ou modification du lockfile.

WorkingDirectory choisit le projet ; Package filtre les noms ; Recursive parcourt les projets du workspace. Depth vaut 0 par défaut, maximum 5 pour éviter une expansion incontrôlée. La sortie contient nom/version/type/projet/profondeur, sans chemins internes du store ni configuration dʼauthentification.

Sous Windows, invoquer directement pnpm.exe. Si Get-Command résout un launcher pnpm.ps1, chercher son exécutable pnpm voisin ; ne pas utiliser de couche cmd ou modifier lʼExecutionPolicy. Un exécutable direct introuvable produit Unsupported. Une dépendance absente ou un store déplacé ne déclenche aucune réinstallation automatique.

Les commandes install, run et exec nécessitent des pompes adaptées. Un script pnpm lançant un runner de tests doit fournir le rapport structuré de ce runner ; ne pas présenter son log comme un résultat de tests analysé.

Version vérifiée : pnpm 12.8.1, exécutable direct et list --json vérifiés le 2026-10-10.

Documentation officielle :

- <https://pnpm.io/cli/list>

Module : `Pnpm-OPumper.psm1`.

Appels disponibles :

- Route : `pnpm list` ; identifiant `pnpm.dependencies`.
- `Get-PumpPnpmDependencies` : Dépendances installées et versions sélectionnées.
  Paramètres du besoin : `WorkingDirectory, Package, Recursive, Depth`.
  Exemple : `Get-PumpPnpmDependencies -WorkingDirectory $repo -Depth 0 -MaxItems 10`.

Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.
