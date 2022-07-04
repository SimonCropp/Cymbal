# <img src='/src/icon.png' height='30px'> Cymbal

[![Build status](https://ci.appveyor.com/api/projects/status/gd7jvcs0nv8pawc8/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/cymbal)
[![NuGet Status](https://img.shields.io/nuget/v/Cymbal.svg)](https://www.nuget.org/packages/Cymbal/)

Cymbal is an MSBuild task that enables bundling dotnet symbols with a deployed app. The goal being to enable line numbers for exceptions in a production system.


## dotnet-symbol required

The [dotnet-symbol dotnet tool](https://www.nuget.org/packages/dotnet-symbol) is required to use this task.

```
dotnet tool install --global dotnet-symbol
```


## Cymbal performs two tasks


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
    <ReferenceCopyLocalPaths Remove="@(ReferenceCopyLocalPaths)" Condition="!Exists('%(FullPath)')" />
  </ItemGroup>
</Target>
```
<sup><a href='/src/Cymbal/build/Cymbal.targets#L18-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-includesymbolfromreferences' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This is done at Build time.


###  dotnet-symbol on Publish

On a [dotnet-publish](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) any missing symbols are attempted to be downloaded via the [dotnet-symbol dotnet tool](https://www.nuget.org/packages/dotnet-symbol) [Source](https://github.com/dotnet/symstore).

This is done at Publish time.
