# Dependencies of the disposable test bundle

This bundle is a development test artifact, not a Thermiqra release installer.

- WiX Toolset DTF 5.0.2, copyright .NET Foundation and contributors; Microsoft Reciprocal License (MS-RL). Unmodified custom-action wrapper and support library are embedded in the test MSI files. License and corresponding upstream source: https://github.com/wixtoolset/wix/tree/v5.0.2 . Build-package repository commit: aa65968c419420d32e3e1b647aea0082f5ca5b78.
- Microsoft .NET 10 and Windows Desktop runtime, copyright .NET Foundation and contributors; MIT and applicable third-party notices. Upstream license: https://github.com/dotnet/runtime/blob/v10.0.13/LICENSE.TXT . Windows desktop sources: https://github.com/dotnet/wpf and https://github.com/dotnet/winforms .
- Thermiqra and its existing dependencies are copied without changing their licensing. Dependency list and source versions remain in PCHardwareMonitor.csproj and NuGet package metadata. LibreHardwareMonitor uses MPL-2.0; its upstream source is https://github.com/LibreHardwareMonitor/LibreHardwareMonitor . This test bundle never installs or loads PawnIO or other hardware drivers.

The WiX command-line compiler is a local build tool, not installed into the guest. The test payload and runner source are in this directory. Runtime and dependency source files are not modified by the test bundle build.
