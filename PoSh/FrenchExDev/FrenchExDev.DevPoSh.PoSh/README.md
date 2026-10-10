# DevPoSh

Bibliothèque de projets PowerShell **7.4+ Core**, chargés depuis `src` en développement et depuis des artefacts explicites en Production.

```powershell
Import-Module ./DevPoSh.PoSh.psd1
$env:FRENCHEXDEV_MYDEVPOSH_PATH = 'C:/path/to/PoSh'
Import-DevPoShModule
Get-DevPoSh -Action CompileAll
Set-DevPoShPowershellProfile -Path "$HOME/devposh-profile.ps1"
```

La racine configurée contient les organisations et leurs projets. Le modèle unique [resources/profile.ps1.tpl](resources/profile.ps1.tpl) charge les sources lorsque `FRENCHEXDEV_DEVPOSH` est absent ; la valeur `Production` charge uniquement l’inventaire `$HOME/.posh.posh.compilations/startup.json`. Un démarrage ne reconstruit jamais les artefacts Production.

## Copier le profil

```powershell
Set-DevPoShPowershellProfile -Path $PROFILE.CurrentUserAllHosts
Set-DevPoShPowershellProfile -Path ./profile-export.ps1 -WhatIf
Set-DevPoShPowershellProfile -Path ./profile-export.ps1 -Force
```

`Path` est obligatoire et désigne un fichier. Une copie identique ne réécrit rien. Un fichier différent exige `Force` et fait l’objet d’une sauvegarde avant remplacement atomique. Le résultat contient `Path`, `Changed` et `Backup`. La copie n’importe ni n’exécute le fichier, ne compile rien et ne modifie aucune variable d’environnement.

L’utilisateur choisit la destination et l’intégration à `$PROFILE`. Les anciens paramètres de génération ne sont plus acceptés. Après modification du modèle, recopier explicitement le profil. L’ancien `DevPoSh/profile.ps1` délègue à ce modèle ; aucun `Install-DevPoSh` n’est ajouté.

## Compiler explicitement

```powershell
Get-DevPoSh -Action CompileAll
Get-DevPoSh -Action CompileAll -Path ./Example.PoSh
Get-DevPoSh -Action CompileAll -Path ./Organization -CompilationRoot ./artifacts
Get-DevPoSh FrenchExDev Example.PoSh Compile
```

Les appels historiques à un projet conservent `Acme`, `Name`, `Action` et le suffixe `Path`. `CompileAll` constitue un jeu de paramètres séparé et ne se combine pas avec les actions d’un projet. Sans sélection, il utilise la racine configurée.

Le compilateur respecte les frontières des modules, les dépendances, les variables, les classes et les ressources. Les sources sont embarquées avec des chemins virtuels permettant aux fonctions d’accéder à leurs ressources via `$PSScriptRoot`. Les ressources sont conservées dans des dossiers immuables identifiés par leur empreinte. Le JSON adjacent est un manifeste versionné ; le code exécutable est dans le `.psm1`.

`Export-DevPoShCompilation` fournit le moteur de compilation de code : verrou, validation d’import dans un processus neuf, publication atomique, conservation du dernier artefact valide et absence de réécriture à contenu identique. Les fournisseurs `FrenchExDev.OPumper.*.PoSh` sont compilés comme des modules ordinaires, avec identité, script `Setup-OutputPumps.ps1`, catalogue et ressources embarqués. OutputPumps prépare séparément un inventaire JSON sous `$HOME/.pwsh-pumps/registry.json`, sans dépendance à DevPoSh.

Après le chargement normal des modules, le profil appelle `Setup-OutputPumps` s’il est disponible. Cette commande lit `$HOME/.pumps-config.yaml` et réutilise les fournisseurs déjà chargés. DevPoSh reste optionnel : le README OutputPumps fournit aussi le bloc de profil autonome. Les fournisseurs ne dépendent pas de DevPoSh à l’exécution.

Une destination `CompilationRoot` personnalisée sert l’export ou les tests ; le profil standard lit l’emplacement utilisateur par défaut. Il ne cherche pas automatiquement des emplacements alternatifs.

Les tests unitaires DevPoSh sont regroupés dans [test/Unit/DevPoSh](../FrenchExDev.OutputPumps.PoSh/test/Unit/DevPoSh), avec les intégrations communes. Voir le [README OutputPump](../FrenchExDev.OutputPumps.PoSh/README.md) pour leur exécution et le parcours complet.
