# <img src='/src/icon.png' height='30px'> Cymbal

[![Build status](https://ci.appveyor.com/api/projects/status/gd7jvcs0nv8pawc8/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/cymbal)
[![NuGet Status](https://img.shields.io/nuget/v/Cymbal.svg)](https://www.nuget.org/packages/Cymbal/)

Cymbal is an MSBuild task that enables bundling dotnet symbols for references with a deployed app. The goal being to enable line numbers for exceptions in a production system.

**See [Milestones](../../milestones?state=closed) for release notes.**


## Cymbal performs two operations


### 1. Copies symbols from references

Works around [symbols not being copied from references](https://github.com/dotnet/sdk/issues/1458). This is done via manipulating `ReferenceCopyLocalPaths`:

<!-- snippet: IncludeSymbolFromReferences -->
<a id='snippet-IncludeSymbolFromReferences'></a>
```targets
<Target Name="IncludeSymbolFromReferences"
        AfterTargets="ResolveAssemblyReferences"
        Condition="@(ReferenceCopyLocalPaths) != ''">
  <ItemGroup>
    <PdbFilesToAdd
            Include="%(ReferenceCopyLocalPaths.RelativeDir)%(ReferenceCopyLocalPaths.Filename).pdb"
            DestinationSubDirectory="%(ReferenceCopyLocalPaths.DestinationSubDirectory)" />
    <PdbFilesToAdd Remove="@(PdbFilesToAdd)"
                   Condition="!Exists('%(FullPath)')" />
    <ReferenceCopyLocalPaths Include="@(PdbFilesToAdd)" />
  </ItemGroup>
</Target>
```
<sup><a href='/src/Cymbal/build/Cymbal.targets#L19-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-IncludeSymbolFromReferences' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This is done at Build time.


### 2. Runs dotnet-symbol

On a [dotnet-publish](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) any missing symbols are attempted to be downloaded via the [dotnet-symbol tool](https://www.nuget.org/packages/dotnet-symbol) ([Source](https://github.com/dotnet/symstore)).

This is done at Publish time.


## Usage

Install the NuGet package in the top level project. i.e. the project that 'dotnet publish' is called on

https://nuget.org/packages/Cymbal/

```
Install-Package Cymbal
```


## Outcome

Given an exe project with a single package reference to `Microsoft.Data.SqlClient`, the output on disk is 39 files:

```
│   Azure.Core.dll
│   Azure.Identity.dll
│   Microsoft.Bcl.AsyncInterfaces.dll
│   Microsoft.Data.SqlClient.dll
│   Microsoft.Identity.Client.dll
│   Microsoft.Identity.Client.Extensions.Msal.dll
│   Microsoft.IdentityModel.Abstractions.dll
│   Microsoft.IdentityModel.JsonWebTokens.dll
│   Microsoft.IdentityModel.Logging.dll
│   Microsoft.IdentityModel.Protocols.dll
│   Microsoft.IdentityModel.Protocols.OpenIdConnect.dll
│   Microsoft.IdentityModel.Tokens.dll
│   Microsoft.SqlServer.Server.dll
│   Microsoft.Win32.SystemEvents.dll
│   SampleApp.deps.json
│   SampleApp.dll
│   SampleApp.exe
│   SampleApp.pdb
│   SampleApp.runtimeconfig.json
│   System.Configuration.ConfigurationManager.dll
│   System.Drawing.Common.dll
│   System.IdentityModel.Tokens.Jwt.dll
│   System.Memory.Data.dll
│   System.Runtime.Caching.dll
│   System.Security.Cryptography.ProtectedData.dll
│   System.Security.Permissions.dll
│   System.Windows.Extensions.dll
└───runtimes
    ├───unix/lib/net6.0
    │      Microsoft.Data.SqlClient.dll
    │      System.Drawing.Common.dll
    ├───win/lib/net6.0
    │      Microsoft.Data.SqlClient.dll
    │      Microsoft.Win32.SystemEvents.dll
    │      System.Drawing.Common.dll
    │      System.Runtime.Caching.dll
    │      System.Security.Cryptography.ProtectedData.dll
    │      System.Windows.Extensions.dll
    ├───win-arm/native
    │      Microsoft.Data.SqlClient.SNI.dll
    ├───win-arm64/native
    │      Microsoft.Data.SqlClient.SNI.dll
    ├───win-x64/native
    │      Microsoft.Data.SqlClient.SNI.dll
    └───win-x86/native
           Microsoft.Data.SqlClient.SNI.dll
```

With the addition of Cymbal, the output on disk is 73 files with the pdb files included:

```
│   Azure.Core.dll
│   Azure.Core.pdb
│   Azure.Identity.dll
│   Azure.Identity.pdb
│   Microsoft.Bcl.AsyncInterfaces.dll
│   Microsoft.Bcl.AsyncInterfaces.pdb
│   Microsoft.Data.SqlClient.dll
│   Microsoft.Data.SqlClient.pdb
│   Microsoft.Identity.Client.dll
│   Microsoft.Identity.Client.Extensions.Msal.dll
│   Microsoft.Identity.Client.Extensions.Msal.pdb
│   Microsoft.Identity.Client.pdb
│   Microsoft.IdentityModel.Abstractions.dll
│   Microsoft.IdentityModel.Abstractions.pdb
│   Microsoft.IdentityModel.JsonWebTokens.dll
│   Microsoft.IdentityModel.JsonWebTokens.pdb
│   Microsoft.IdentityModel.Logging.dll
│   Microsoft.IdentityModel.Logging.pdb
│   Microsoft.IdentityModel.Protocols.dll
│   Microsoft.IdentityModel.Protocols.OpenIdConnect.dll
│   Microsoft.IdentityModel.Protocols.OpenIdConnect.pdb
│   Microsoft.IdentityModel.Protocols.pdb
│   Microsoft.IdentityModel.Tokens.dll
│   Microsoft.IdentityModel.Tokens.pdb
│   Microsoft.SqlServer.Server.dll
│   Microsoft.SqlServer.Server.pdb
│   Microsoft.Win32.SystemEvents.dll
│   Microsoft.Win32.SystemEvents.pdb
│   SampleApp.deps.json
│   SampleApp.dll
│   SampleApp.exe
│   SampleApp.pdb
│   SampleApp.runtimeconfig.json
│   System.Configuration.ConfigurationManager.dll
│   System.Configuration.ConfigurationManager.pdb
│   System.Drawing.Common.dll
│   System.Drawing.Common.pdb
│   System.IdentityModel.Tokens.Jwt.dll
│   System.IdentityModel.Tokens.Jwt.pdb
│   System.Memory.Data.dll
│   System.Memory.Data.pdb
│   System.Runtime.Caching.dll
│   System.Runtime.Caching.pdb
│   System.Security.Cryptography.ProtectedData.dll
│   System.Security.Cryptography.ProtectedData.pdb
│   System.Security.Permissions.dll
│   System.Security.Permissions.pdb
│   System.Windows.Extensions.dll
│   System.Windows.Extensions.pdb
└───runtimes
    ├───unix/lib/net6.0
    │      Microsoft.Data.SqlClient.dll
    │      Microsoft.Data.SqlClient.pdb
    │      System.Drawing.Common.dll
    │      System.Drawing.Common.pdb
    ├───win/lib/net6.0
    │      Microsoft.Data.SqlClient.dll
    │      Microsoft.Data.SqlClient.pdb
    │      Microsoft.Win32.SystemEvents.dll
    │      Microsoft.Win32.SystemEvents.pdb
    │      System.Drawing.Common.dll
    │      System.Drawing.Common.pdb
    │      System.Runtime.Caching.dll
    │      System.Runtime.Caching.pdb
    │      System.Security.Cryptography.ProtectedData.dll
    │      System.Security.Cryptography.ProtectedData.pdb
    │      System.Windows.Extensions.dll
    │      System.Windows.Extensions.pdb
    ├───win-arm/native
    │      Microsoft.Data.SqlClient.SNI.dll
    │      Microsoft.Data.SqlClient.SNI.pdb
    ├───win-arm64/native
    │      Microsoft.Data.SqlClient.SNI.dll
    │      Microsoft.Data.SqlClient.SNI.pdb
    ├───win-x64/native
    │      Microsoft.Data.SqlClient.SNI.dll
    │      Microsoft.Data.SqlClient.SNI.pdb
    └───win-x86/native
           Microsoft.Data.SqlClient.SNI.dll
           Microsoft.Data.SqlClient.SNI.pdb
```


## dotnet-symbol required

To install the [dotnet-symbol tool](https://www.nuget.org/packages/dotnet-symbol), the recommended approach is to [install it as a local tool](https://docs.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use).

In the root of a repository execute:

```
dotnet new tool-manifest
dotnet tool install dotnet-symbol
```

This will result in a `.config/dotnet-tools.json` file:

<!-- snippet: src\.config\dotnet-tools.json -->
<a id='snippet-src\.config\dotnet-tools.json'></a>
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-symbol": {
      "version": "8.0.547301",
      "commands": [
        "dotnet-symbol"
      ]
    }
  }
}
```
<sup><a href='#snippet-src\.config\dotnet-tools.json' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[dotnet tool restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-restore) can then be run locally or in a build environment:

```
dotnet tool restore
```

Or to point to a nested directory: 

```
dotnet tool restore --tool-manifest src/.config/dotnet-tools.json
```


### Scanned symbol servers

 * https://symbols.nuget.org/download/symbols 
 * https://msdl.microsoft.com/download/symbols/ 

### Overriding symbol servers

Add the following to the project or `Directory.Build.props`:

<!-- snippet: SetSymbolServers -->
<a id='snippet-SetSymbolServers'></a>
```csproj
<ItemGroup>
  <SymbolServer Include="http://localhost:88/symbols" />
  <SymbolServer Include="http://localhost:89/symbols" />
</ItemGroup>
```
<sup><a href='/src/SampleWithSymbolServer/SampleWithSymbolServer.csproj#L9-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-SetSymbolServers' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


## Cache Directory

The cache directory can be controlled via either:

 * An environment variable `CymbalCacheDirectory`. Must contain a full path. Or:
 * An MSBuild property `CymbalCacheDirectory`. This can be passed into a `dotnet publish` using `-p:CymbalCacheDirectory=FullOrRelativePath`. `Path.GetFullPath()` will be used on the value.

The resolved directory will be created if it doesn't exist.

The MSBuild property take priority over the environment variable.


## Icon

[Cymbals](https://thenounproject.com/term/cymbals/4920970/) designed by [Eucalyp](https://thenounproject.com/eucalyp) from [The Noun Project](https://thenounproject.com).


