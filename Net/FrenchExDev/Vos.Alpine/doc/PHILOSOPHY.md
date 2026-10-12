# Vos.Alpine -- Philosophy

## Machine types are composable contributors, not class hierarchies

The natural instinct when modeling "an Alpine VM" and "an Alpine Docker VM" is inheritance: `AlpineDockerMachineType : AlpineMachineType : BaseMachineType`. This breaks down quickly:

- What about an Alpine K8s VM that shares Docker config but not Alpine networking?
- What about a Debian Docker VM that shares Docker config but not Alpine anything?
- Multiple inheritance doesn't exist in C#. Mixins don't either.

The contributor pattern sidesteps this entirely. Each contributor is a function: `VosMachineType -> void`. Contributors compose by calling each other:

```
DockerHostContributor.Contribute(mt)
  -> AlpineVirtualBoxContributor.Contribute(mt)   // base
  -> add Docker provisioning                        // extension
```

No class hierarchy. No diamond problem. No abstract base classes with 15 virtual methods.

---

## Defaults should be non-destructive

A contributor that overwrites existing configuration is a contributor you can't compose. If `AlpineVirtualBoxContributor` always sets `Box = "frenchexdev/alpine-3.21-virt"`, you can't use a custom box.

Vos.Alpine uses null-coalescing (`??=`) for the box and provider, `Contains()` checks for plugins, and `TryAdd()` for variables. This means:

- Set a value before calling `Contribute()` -> it's preserved
- Don't set it -> the contributor provides a sensible default

This is the open-closed principle applied to configuration: open for extension (override after), closed for surprise (defaults don't clobber).

---

## VBoxManage commands encode hardware knowledge

The 9 VBoxManage commands in this contributor are not arbitrary -- they encode specific knowledge about running Alpine Linux well on VirtualBox:

- **Nested virtualization** enables Docker and K8s inside the VM
- **SATA SSD mode** prevents VirtualBox from scheduling I/O as if it were a spinning disk
- **NIC promiscuous mode** enables bridged networking for multi-VM clusters
- **Large pages and PAE** improve memory management for workloads that need it

This knowledge belongs in the contributor, not in documentation that developers have to read and manually apply. The contributor is the executable specification.

---

## One class, one file, one responsibility

`AlpineVirtualBoxContributor` is 60 lines. It does exactly one thing: configure a `VosMachineType` for Alpine Linux on VirtualBox. It has no dependencies beyond the Vos core. It has 7 tests and 100% branch coverage.

Not every project needs to be complex. Some of the most valuable code in a system is the small, well-tested building block that larger pieces compose from.
