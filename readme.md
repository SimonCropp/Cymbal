# <img src='/src/icon.png' height='30px'> Cymbal

[![Build status](https://ci.appveyor.com/api/projects/status/s3agb6fiax7pgwls/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/cymbal)
[![NuGet Status](https://img.shields.io/nuget/v/Cymbal.svg)](https://www.nuget.org/packages/Cymbal/)


  <Target Name="IncludeSymbolFiles" AfterTargets="ResolveAssemblyReferences" Condition="@(ReferenceCopyLocalPaths) != ''">
    <ItemGroup>
      <ReferenceCopyLocalPaths Include="%(ReferenceCopyLocalPaths.RelativeDir)%(ReferenceCopyLocalPaths.Filename).pdb;
                                          %(ReferenceCopyLocalPaths.RelativeDir)%(ReferenceCopyLocalPaths.Filename).xml" />
      <ReferenceCopyLocalPaths Remove="@(ReferenceCopyLocalPaths)" Condition="!Exists('%(FullPath)')" />
    </ItemGroup>
  </Target>


  https://github.com/dotnet/sdk/issues/1458
  https://github.com/loic-sharma/symbols


  https://github.com/dotnet/symstore

  https://www.nuget.org/packages/dotnet-symbol
  
  dotnet tool install --global dotnet-symbol 
  
  dotnet-symbol
  
  dotnet dotnet-symbol

  https://github.com/dotnet/symstore/blob/main/src/Microsoft.SymbolStore/Microsoft.SymbolStore.csproj


  https://github.com/loic-sharma/symbols


  dotnet tool install --global dotnet-symbol --version 1.0.321201
