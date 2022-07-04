# <img src='/src/icon.png' height='30px'> Cymbal

[![Build status](https://ci.appveyor.com/api/projects/status/gd7jvcs0nv8pawc8/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/cymbal)
[![NuGet Status](https://img.shields.io/nuget/v/Cymbal.svg)](https://www.nuget.org/packages/Cymbal/)

Cymbal is an MSBuild task that enables bundling dotnet symbols with a deployed app. The goal being to enable line numbers for exceptions in a production system.


## dotnet-symbol required

The [dotnet-symbol dotnet tool](https://www.nuget.org/packages/dotnet-symbol) is required to use this task.

```
dotnet tool install --global dotnet-symbol
```


## Cymbal performs two operations


### Copies symbols from references

Works around [symbols not being copied from references](https://github.com/dotnet/sdk/issues/1458). This is done via manipulating `ReferenceCopyLocalPaths`:

<!-- snippet: IncludeSymbolFromReferences -->
<a id='snippet-includesymbolfromreferences'></a>
```targets
<Target Name="IncludeSymbolFromReferences"
        AfterTargets="ResolveAssemblyReferences"
        Condition="@(ReferenceCopyLocalPaths) != ''">
  <ItemGroup>
    <ReferenceCopyLocalPaths Include="%(ReferenceCopyLocalPaths.RelativeDir)%(ReferenceCopyLocalPaths.Filename).pdb" />
    <ReferenceCopyLocalPaths Remove="@(ReferenceCopyLocalPaths)"
                             Condition="!Exists('%(FullPath)')" />
  </ItemGroup>
</Target>
```
<sup><a href='/src/Cymbal/build/Cymbal.targets#L19-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-includesymbolfromreferences' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This is done at Build time.


### dotnet-symbol on Publish

On a [dotnet-publish](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) any missing symbols are attempted to be downloaded via the [dotnet-symbol tool](https://www.nuget.org/packages/dotnet-symbol) [Source](https://github.com/dotnet/symstore).

This is done at Publish time.


## Usage

Install the NuGet package in the top level project. i.e. the project that 'dotnet publish' is called on

https://nuget.org/packages/Cymbal/

```
Install-Package Cymbal
```


## Build Server integration

To enable the [dotnet-symbol tool](https://www.nuget.org/packages/dotnet-symbol) in a build environmens, the recomended approach is [install it as a local tool](https://docs.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use).

This will result in a `.config/dotnet-tools.json` file:

<!-- snippet: src\.config\dotnet-tools.json -->
<a id='snippet-src\.config\dotnet-tools.json'></a>
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-symbol": {
      "version": "1.0.321201",
      "commands": [
        "dotnet-symbol"
      ]
    }
  }
}
```
<sup><a href='#snippet-src\.config\dotnet-tools.json' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[dotnet tool restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-restore) can then be added to the build script:

```
dotnet tool restore --configfile src/.config/dotnet-tools.json
```


## Icon

[Cymbals](https://thenounproject.com/term/cymbals/4920970/) designed by [Eucalyp](https://thenounproject.com/eucalyp) from [The Noun Project](https://thenounproject.com).
