# Vos.Alpine.DockerHost -- Philosophy

## Docker host is a separate contributor, not a flag

It's tempting to add a `bool enableDocker` parameter to `AlpineVirtualBoxContributor`. One class, one parameter, done. But this approach scales poorly:

- Next comes `enableK8s`, then `enableTraefik`, then `enableMonitoring`
- Each flag adds a code path, a test matrix, and a documentation burden
- The contributor becomes a god class that knows about everything

Instead, each capability is its own contributor. `DockerHostContributor` knows about Docker. `AlpineVirtualBoxContributor` knows about Alpine. Neither knows about the other's domain. The composition happens at the call site, not inside the class.

---

## Composition by delegation, not inheritance

`DockerHostContributor` does not extend `AlpineVirtualBoxContributor`. It creates one and calls it:

```csharp
new AlpineVirtualBoxContributor().Contribute(machineType);
```

This is intentional. Inheritance would bind the Docker contributor to a specific Alpine version parameter. Delegation lets the Docker contributor create the Alpine contributor with whatever parameters make sense -- or let the caller apply Alpine separately.

---

## Provisioning steps are data, not code

The Docker provisioning step is a `VosProvisioningStep` record with a key, version, enabled flag, and environment dictionary. It doesn't contain the installation script -- it describes what should be installed and how.

The actual shell script lives in the Vos provisioning system, keyed by the step's `Key` property. This separation means:

- The contributor declares intent ("install Docker")
- The provisioning system executes it ("run this shell script")
- The script can change without touching the contributor
- Different environments can have different script implementations for the same key

---

## Shared folders encode workflow assumptions

The two shared folders (`docker-compose` and `data`) encode a specific workflow: Docker Compose projects live in a host directory and are mounted into the guest, while persistent data is kept in a separate volume.

These are opinionated defaults, not universal truths. They reflect the FrenchExDev development workflow. Other teams might mount different paths. That's fine -- add more shared folders after `Contribute()`, or write a different contributor entirely.

The point of a contributor is to capture a known-good configuration pattern, not to anticipate every possible use case.
