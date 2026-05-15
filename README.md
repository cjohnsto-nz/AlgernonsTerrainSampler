Refer to https://mods.vintagestory.at/algernonsterrainsampler for info about this mod



You'll need to create a file called "Directory.Build.props" to link the project to your Vintage Story folder.

Copypaste this into it and put in your VS directory:

<Project>

&#x20; <PropertyGroup>

&#x20;   <!-- Set this to your Vintage Story installation folder -->

&#x20;   <GameDirectory>C:\\SomeFolder\\AnotherFolder\\VintageStoryFolderHere>

&#x20; </PropertyGroup>

</Project>



