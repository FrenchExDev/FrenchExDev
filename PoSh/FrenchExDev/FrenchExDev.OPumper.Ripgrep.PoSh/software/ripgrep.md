# ripgrep (rg)

Ripgrep recherche dans les fichiers en respectant par défaut les règles dʼignorance ; il évite normalement les fichiers cachés et binaires. Utiliser Path et Glob pour cibler le besoin, Literal pour une chaîne exacte et IgnoreCase si nécessaire.

La recherche lit `rg --json` : chemins, numéros de ligne et extraits sont sélectionnés. La découverte de fichiers utilise `--files --null` pour préserver les noms inhabituels. Code 0 : résultats ; 1 : aucun résultat, succès fonctionnel ; 2 : erreur, potentiellement accompagnée de résultats partiels. Une erreur de lecture ne prouve jamais lʼabsence de correspondance. Les événements à encodage non représentable sont omis et signalés. Les extraits longs ou sensibles sont limités et assainis.

Ne pas utiliser `--max-count` comme plafond global : cette option est par fichier. Les deux pompes analysent progressivement tous les enregistrements et attendent la fin du processus et des flux. MaxItems borne les extraits, sans arrêter la commande ni le comptage. Data.Complete certifie MatchLines ou FileCount ; un total exact devient nul si l’analyse est incomplète. Une ligne géante dépassant la limite d’enregistrement est signalée par RecordLimit. Aucun flux source n’est affiché.

Version vérifiée : ripgrep 15.2.0, commandes locales vérifiées le 2026-10-10.

Documentation officielle :

- <https://github.com/BurntSushi/ripgrep/blob/master/GUIDE.md>
- <https://github.com/BurntSushi/ripgrep/blob/master/doc/rg.1.txt.tpl>

Module : `Ripgrep-OPumper.psm1`.

Appels disponibles :

- Route : `rg ` ; identifiant `ripgrep.search`.
- `Find-PumpText` : Correspondances structurées, filtres et compteurs.
  Paramètres du besoin : `WorkingDirectory, Pattern, Path, Glob, Literal, IgnoreCase, Environment`.
  Exemple : `Find-PumpText -WorkingDirectory $repo -Pattern 'TODO' -Path 'src/' -MaxItems 10`.
- Route : `rg --files` ; identifiant `ripgrep.files`.
- `Find-PumpFiles` : Chemins de fichiers ciblés.
  Paramètres du besoin : `WorkingDirectory, Path, Glob, Environment`.
  Exemple : `Find-PumpFiles -WorkingDirectory $repo -Glob '*.cs' -Path 'src/'`.

Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.
