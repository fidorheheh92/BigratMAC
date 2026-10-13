# MAC Address Manager

Safe WPF foundation for Windows MAC address management. The current build uses a mock service so UI workflows and tests never modify real adapters. MAC parsing, validation, locally administered random generation, original-MAC preservation, apply, and restore workflows are implemented behind `INetworkAdapterService`.

Build with `dotnet build MacChanger.slnx` and test with `dotnet test MacChanger.slnx`. Automated tests do not change network configuration. A future Windows implementation must isolate adapter discovery and privileged changes, require explicit user action, verify results, and document driver/persistence limitations.
