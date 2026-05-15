using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public const string ProjectName = "AlgernonsTerrainSampler";

    /// <summary>
    /// Root directory of the repo (parent of CakeBuild/).
    /// modinfo.json lives here directly; the .csproj lives in {RootDir}/{ProjectName}/.
    /// </summary>
    public const string RootDir = "..";

    /// <summary>
    /// Directory containing the mod's .csproj and source files.
    /// </summary>
    public static string ProjectDir => $"{RootDir}/{ProjectName}";

    public string BuildConfiguration { get; set; }
    public string Version { get; }
    public string Name { get; }
    public bool SkipJsonValidation { get; set; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        SkipJsonValidation = context.Argument("skipJsonValidation", false);
        var modInfo = context.DeserializeJsonFromFile<ModInfo>($"{RootDir}/modinfo.json");
        Version = modInfo.Version;
        Name = modInfo.ModID;
    }
}

[TaskName("ValidateJson")]
public sealed class ValidateJsonTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.SkipJsonValidation)
        {
            return;
        }

        var jsonFiles = context.GetFiles($"{BuildContext.RootDir}/{BuildContext.ProjectName}/assets/**/*.json");
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file.FullPath);
                JToken.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
            }
        }
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(ValidateJsonTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetClean($"{BuildContext.ProjectDir}/{BuildContext.ProjectName}.csproj",
            new DotNetCleanSettings
            {
                Configuration = context.BuildConfiguration
            });

        context.DotNetPublish($"{BuildContext.ProjectDir}/{BuildContext.ProjectName}.csproj",
            new DotNetPublishSettings
            {
                Configuration = context.BuildConfiguration
            });
    }
}

[TaskName("FlattenPublishFolder")]
[IsDependentOn(typeof(BuildTask))]
public sealed class FlattenPublishFolderTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var modDir = $"{BuildContext.ProjectDir}/bin/{context.BuildConfiguration}/Mods/{context.Name}";
        var publishDir = $"{modDir}/publish";

        if (!Directory.Exists(publishDir))
        {
            context.Log.Warning($"Publish directory not found: {publishDir}. Skipping flatten.");
            return;
        }

        context.Log.Information("Flattening publish folder structure...");

        foreach (var file in Directory.GetFiles(publishDir))
        {
            var fileName = System.IO.Path.GetFileName(file);
            var destPath = System.IO.Path.Combine(modDir, fileName);

            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }

            File.Copy(file, destPath);
        }

        context.DeleteDirectory(publishDir, new DeleteDirectorySettings { Recursive = true });

        context.Log.Information("Publish folder flattened successfully.");
    }
}

[TaskName("Package")]
[IsDependentOn(typeof(FlattenPublishFolderTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var modDir = $"{BuildContext.ProjectDir}/bin/{context.BuildConfiguration}/Mods/{context.Name}";
        var releaseDir = $"{BuildContext.RootDir}/Releases/{context.Name}";

        context.EnsureDirectoryExists($"{BuildContext.RootDir}/Releases");
        context.CleanDirectory($"{BuildContext.RootDir}/Releases");
        context.EnsureDirectoryExists(releaseDir);

        context.CopyFiles($"{modDir}/*.dll", releaseDir);
        context.CopyFiles($"{modDir}/*.json", releaseDir);
        context.CopyFiles($"{modDir}/*.png", releaseDir);

        // Dotfiles aren't matched by the globs above; copy .ignore explicitly so VS
        // honours its assembly-load filtering inside the released zip.
        var ignoreFile = $"{modDir}/.ignore";
        if (File.Exists(ignoreFile))
        {
            File.Copy(ignoreFile, $"{releaseDir}/.ignore", overwrite: true);
        }

        if (context.DirectoryExists($"{modDir}/assets"))
        {
            context.CopyDirectory($"{modDir}/assets", $"{releaseDir}/assets");
        }

        if (context.DirectoryExists($"{modDir}/native"))
        {
            context.CopyDirectory($"{modDir}/native", $"{releaseDir}/native");
        }

        context.Zip(releaseDir, $"{BuildContext.RootDir}/Releases/{context.Name}_{context.Version}.zip");
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask
{
}
