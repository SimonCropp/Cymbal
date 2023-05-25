# <img src='/src/icon.png' height='30px'> Cymbal

[![Build status](https://ci.appveyor.com/api/projects/status/gd7jvcs0nv8pawc8/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/cymbal)
[![NuGet Status](https://img.shields.io/nuget/v/Cymbal.svg)](https://www.nuget.org/packages/Cymbal/)

Cymbal is an MSBuild task that enables bundling dotnet symbols for references with a deployed app. The goal being to enable line numbers for exceptions in a production system.


## Cymbal performs two operations


### 1. Copies symbols from references

Works around [symbols not being copied from references](https://github.com/dotnet/sdk/issues/1458). This is done via manipulating `ReferenceCopyLocalPaths`:

<!-- snippet: IncludeSymbolFromReferences -->
<a id='snippet-includesymbolfromreferences'></a>
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
<sup><a href='/src/Cymbal/build/Cymbal.targets#L19-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-includesymbolfromreferences' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This is done at Build time.


### 2. Run dotnet-symbol

On a [dotnet-publish](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) any missing symbols are attempted to be downloaded via the [dotnet-symbol tool](https://www.nuget.org/packages/dotnet-symbol) ([Source](https://github.com/dotnet/symstore)).

This is done at Publish time.


## Usage

Install the NuGet package in the top level project. i.e. the project that 'dotnet publish' is called on

https://nuget.org/packages/Cymbal/

```
Install-Package Cymbal
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
      "version": "1.0.415602",
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
<a id='snippet-setsymbolservers'></a>
```csproj
<ItemGroup>
  <SymbolServer Include="http://localhost:88/symbols" />
  <SymbolServer Include="http://localhost:89/symbols" />
</ItemGroup>
```
<sup><a href='/src/SampleWithSymbolServer/SampleWithSymbolServer.csproj#L9-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-setsymbolservers' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Cache Directory

The cache directory can be controlled via either:

 * An environment variable `CymbalCacheDirectory`. Must contain a full path. Or:
 * An MSBuild property `CymbalCacheDirectory`. This can be passed into a `dotnet publish` using `-p:CymbalCacheDirectory=FullOrRelativePath`. `Path.GetFullPath()` will be used on the value.

The resolved directory will be created if it doesn't exist.

The MSBuild property take priority over the environment variable.


## Icon

[Cymbals](https://thenounproject.com/term/cymbals/4920970/) designed by [Eucalyp](https://thenounproject.com/eucalyp) from [The Noun Project](https://thenounproject.com).
