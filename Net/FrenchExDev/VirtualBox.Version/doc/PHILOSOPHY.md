# VirtualBox.Version -- Philosophy

## Version discovery is a separate concern

Knowing which VirtualBox is installed is not the same as controlling VirtualBox. Tools like Vagrant, Packer, and Vos all need to answer the question "what version of VirtualBox is on this machine?" before they do anything else. Embedding that logic in each tool means duplicating process-launching, output-parsing, and error-handling code.

VirtualBox.Version isolates this concern into a single library with a clean interface. Any tool in the ecosystem can depend on it without pulling in the full weight of a VM orchestrator.

---

## Validate before you provision

A Vagrant `up` that fails 10 minutes in because VirtualBox is the wrong version is a waste of time. A version check that takes 200ms and fails immediately is not.

The same applies to checksum verification: downloading a Guest Additions ISO and discovering it's corrupted after a 5-minute install is worse than checking the SHA256 before you start.

VirtualBox.Version provides both checks as first-class operations, not afterthoughts.

---

## Interfaces for boundaries, records for data

The library defines two interfaces (`IVirtualBoxSystemVersionDiscoverer`, `IVirtualBoxVersionInformationSearcher`) because both operations cross I/O boundaries -- one launches a process, the other makes an HTTP request. These are the only things worth abstracting.

The data itself is modeled as immutable records: `VirtualBoxVersionRecord`, `VirtualBoxVersionInfos`, `VirtualBoxVersionInformationSearchingFilters`. Records are value-equal, immutable, and concise -- the right tool for data transfer.

---

## Zero dependencies by design

This library has no NuGet dependencies. It uses only `System.Diagnostics.Process` and `System.Net.Http.HttpClient` -- both part of the .NET runtime. This keeps the dependency graph clean and ensures any project in the ecosystem can reference it without version conflicts or transitive bloat.
