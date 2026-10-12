# Podman

Podman gère les conteneurs et images OCI. Sous Windows, la CLI peut dépendre dʼune machine et dʼun service accessibles. Ces pompes ne démarrent pas de machine, ne téléchargent pas dʼimage et ne modifient pas de conteneur.

Les inventaires utilisent `--format json`, puis sélectionnent identifiants, noms, images, états et tailles. Ils nʼexportent ni variables dʼenvironnement ni commandes complètes de conteneurs. Filter transmet les filtres natifs documentés, All inclut les conteneurs arrêtés.

Les logs exigent Container. Utiliser Since et Filter pour cibler le besoin ; seule une fin de journal plafonnée est interrogée, jamais follow. Chaque événement conserve sa date, son flux et son message assaini. Les flux sont triés par date ; lʼordre exact entre événements de même date nʼest pas garanti. Le total global reste inconnu. Un service inaccessible est un échec distinguable dʼun inventaire vide.

Version vérifiée : Podman CLI 6.1.2 ; disponibilité du service non présumée, vérifié le 2026-10-10.

Documentation officielle :

- <https://docs.podman.io/en/latest/markdown/podman-ps.1.html>
- <https://docs.podman.io/en/latest/markdown/podman-images.1.html>
- <https://docs.podman.io/en/latest/markdown/podman-logs.1.html>

Module : `../FrenchExDev.OPumper.Podman.PoSh/Podman-OPumper.psm1`.

Appels disponibles :

- Route : `podman ps` ; identifiant `podman.containers`.
- `Get-PumpPodmanContainers` : Inventaire de conteneurs sélectionnés.
  Paramètres du besoin : `WorkingDirectory, Filter, All, Environment`.
  Exemple : `Get-PumpPodmanContainers -Filter 'status=running' -MaxItems 10`.
- Route : `podman images` ; identifiant `podman.images`.
- `Get-PumpPodmanImages` : Inventaire dʼimages sélectionnées.
  Paramètres du besoin : `WorkingDirectory, Filter, Environment`.
  Exemple : `Get-PumpPodmanImages -Filter 'dangling=true'`.
- Route : `podman logs` ; identifiant `podman.log`.
- `Get-PumpPodmanLog` : Derniers événements dʼun conteneur.
  Paramètres du besoin : `WorkingDirectory, Container, Since, Filter, Environment`.
  Exemple : `Get-PumpPodmanLog -Container $container -Since '10m' -Filter 'error'`.

Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.
