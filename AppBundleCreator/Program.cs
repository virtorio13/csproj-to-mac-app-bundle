using AppBundleCreator;

if (args.Length == 0)
{
    Console.WriteLine("Usage: AppBundleCreator <path-to-csproj> [output-dir] [app-name] [icon-path] [runtime]");
    Console.WriteLine("Defaults:");
    Console.WriteLine("  output-dir: ./");
    Console.WriteLine("  app-name: Project Name (from filename)");
    Console.WriteLine("  icon-path: null");
    Console.WriteLine("  runtime: osx-arm64");
    return;
}

string projectPath = Path.GetFullPath(args[0]);
if (!File.Exists(projectPath))
{
    Console.WriteLine($"Error: Project file not found at {projectPath}");
    return;
}

string appName = Path.GetFileNameWithoutExtension(projectPath);
string outputDir = Directory.GetCurrentDirectory();
string? iconPath = null;
string runtime = "osx-arm64";

if (args.Length > 1) outputDir = args[1];
if (args.Length > 2) appName = args[2];
if (args.Length > 3) iconPath = args[3];
if (args.Length > 4) runtime = args[4];

// Ensure output dir exists
Directory.CreateDirectory(outputDir);

try 
{
    AppBundleBuilder.Build(projectPath, outputDir, appName, iconPath, runtime);
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
