Refer to https://mods.vintagestory.at/algernonsterrainsampler for info about this mod

You'll need to create a file called "Directory.Build.props" to link the project to your Vintage Story folder.

Copypaste this into it and put in your VS directory:

```xml
<Project>
  <PropertyGroup>
    <!-- Set this to your Vintage Story installation folder -->
    <GameDirectory>C:\SomeFolder\AnotherFolder\VintageStoryFolderHere</GameDirectory>
  </PropertyGroup>
</Project>
```
