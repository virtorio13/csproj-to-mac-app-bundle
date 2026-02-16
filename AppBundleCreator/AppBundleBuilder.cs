using System.Diagnostics;

namespace AppBundleCreator;

public class AppBundleBuilder
{
    public static void Build(string projectPath, string outputDir, string appName, string? iconPath, string runtime)
    {
        Console.WriteLine($"Building App Bundle for: {projectPath}");
        Console.WriteLine($"Output Directory: {outputDir}");
        Console.WriteLine($"App Name: {appName}");
        Console.WriteLine($"Runtime: {runtime}");

        // 1. dotnet publish
        var publishDir = Path.Combine(Path.GetTempPath(), "AppBundleCreator", appName, "publish");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, true);
        Directory.CreateDirectory(publishDir);

        Console.WriteLine("Running dotnet publish...");
        var publishStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{projectPath}\" -r {runtime} -c Release --self-contained -o \"{publishDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(publishStartInfo);
        process?.WaitForExit();

        if (process == null || process.ExitCode != 0)
        {
            Console.WriteLine("Error: dotnet publish failed.");
            Console.WriteLine(process?.StandardError.ReadToEnd());
            return;
        }
        Console.WriteLine("dotnet publish successful.");

        // 2. Create .app structure
        var appBundlePath = Path.Combine(outputDir, $"{appName}.app");
        var contentsPath = Path.Combine(appBundlePath, "Contents");
        var macOsPath = Path.Combine(contentsPath, "MacOS");
        var resourcesPath = Path.Combine(contentsPath, "Resources");

        if (Directory.Exists(appBundlePath)) Directory.Delete(appBundlePath, true);
        Directory.CreateDirectory(macOsPath);
        Directory.CreateDirectory(resourcesPath);

        // 3. Copy files
        Console.WriteLine("Copying files...");
        var executableName = Path.GetFileNameWithoutExtension(projectPath); // Default exec name is usually project name
        
        // Find the actual executable in publish dir. It might be different if AssemblyName is set.
        // But usually it matches project name or we can look for the extensionless file with ‘x’ permission (hard on windows dev env but ok here)
        // A safer bet involves checking the project file, but for now let's assume standard behavior or try to find it.
        // Actually, dotnet publish output usually has the main binary named after the project.
        // Let's copy EVERYTHING.
        
        // Copy all files
        foreach (var file in Directory.GetFiles(publishDir))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(macOsPath, fileName), true);
        }

        // Detect executable
        // Try to find the executable that matches the project name (without extension)
        var potentialExec = Path.Combine(publishDir, executableName);
        if (File.Exists(potentialExec))
        {
             // This is the best guess, executableName is already set correctly
        }
        else
        {
            // Fallback: look for other executable files
            foreach (var file in Directory.GetFiles(publishDir))
            {
                if (file.EndsWith(".dll") || file.EndsWith(".pdb") || file.EndsWith(".json") || file.EndsWith(".xml") || file.EndsWith(".config")) continue;

                if (File.GetUnixFileMode(file) != 0 && 
                    (File.GetUnixFileMode(file) & UnixFileMode.UserExecute) != 0)
                {
                    executableName = Path.GetFileName(file);
                    break; // Found one
                }
            }
        }

        // Also if we passed appName, the bundle is named appName, but the inner executable is still what dotnet produced.
        // The Info.plist must point to that inner executable.
        
        // However, standard practice often has the inner executable match the bundle name for clarity, 
        // but it's NOT required. maintaining original name is safer to avoid breaking internal paths if any.

        // 4. Create Info.plist
        Console.WriteLine("Generating Info.plist...");
        var infoPlistPath = Path.Combine(contentsPath, "Info.plist");
        var iconFileEntry = "<key>CFBundleIconFile</key>\n\t<string>AppIcon</string>";
        
        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleExecutable</key>
    <string>{executableName}</string>
    <key>CFBundleIdentifier</key>
    <string>com.example.{appName.Replace(" ", "-")}</string>
    <key>CFBundleName</key>
    <string>{appName}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.12</string>
    {iconFileEntry}
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
</dict>
</plist>";
        File.WriteAllText(infoPlistPath, plistContent);

        // 5. Handle Icon
        string iconDestPath = Path.Combine(resourcesPath, "AppIcon.icns");
        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            Console.WriteLine($"Processing icon: {iconPath}");
            var ext = Path.GetExtension(iconPath).ToLower();
            if (ext == ".icns")
            {
                File.Copy(iconPath, iconDestPath, true);
            }
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                ConvertImageToIcns(iconPath, iconDestPath);
            }
            else
            {
                Console.WriteLine("Warning: Unsupported icon format. Using default.");
                CreateDefaultIcon(iconDestPath);
            }
        }
        else
        {
            Console.WriteLine("No icon provided. creating default.");
            CreateDefaultIcon(iconDestPath);
        }

        // 6. Set Metadata / Permissions (chmod +x)
        // .NET 7+ has File.SetUnixFileMode, we are on .NET 9.
        var execPath = Path.Combine(macOsPath, executableName);
        if (File.Exists(execPath))
        {
            // Ensure executable permission
            // 0x755 = rwxr-xr-x = S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH
            // Create a mode.
             try
            {
                 var currentMode = File.GetUnixFileMode(execPath);
                 File.SetUnixFileMode(execPath, currentMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                 Console.WriteLine($"Set executable permissions for {executableName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not set unix file permissions: {ex.Message}");
            }
        }
        
        Console.WriteLine($"App Bundle created successfully at: {appBundlePath}");
    }

    private static void ConvertImageToIcns(string sourceImage, string destinationIcns)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AppBundleCreator_Icon_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            var iconSetDir = Path.Combine(tempDir, "AppIcon.iconset");
            Directory.CreateDirectory(iconSetDir);

            // Define required sizes for .iconset
            // Format: icon_{size}x{size}{@2x}.png
            var sizes = new[] { 16, 32, 128, 256, 512 };
            
            foreach (var size in sizes)
            {
                RunSips(sourceImage, Path.Combine(iconSetDir, $"icon_{size}x{size}.png"), size, size);
                RunSips(sourceImage, Path.Combine(iconSetDir, $"icon_{size}x{size}@2x.png"), size * 2, size * 2);
            }

            // Run iconutil
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/iconutil",
                Arguments = $"-c icns \"{iconSetDir}\" -o \"{destinationIcns}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process == null || process.ExitCode != 0)
            {
                Console.WriteLine("Error running iconutil:");
                Console.WriteLine(process?.StandardError.ReadToEnd());
            }

            // Cleanup
            Directory.Delete(tempDir, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error converting icon: {ex.Message}");
        }
    }

    private static void RunSips(string input, string output, int width, int height)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sips",
            Arguments = $"-z {height} {width} \"{input}\" --out \"{output}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(startInfo);
        p?.WaitForExit();
    }

    private static void CreateDefaultIcon(string destinationIcns)
    {
        // Embed a simple base64 1024x1024 PNG (Solid Blue with a white 'A' or just a solid color)
        // This is a 1x1 pixel transparent PNG scaled up or similar? No sips needs a real image.
        // Let's use a small 512x512 solid blue png base64.
        // Actually, creating a 512x512 png via base64 in code is verbose.
        // I'll create a temporary png file from base64, then convert it.
        
        // Base64 for a simple 512x512 blue box PNG.
        var base64Png = "iVBORw0KGgoAAAANSUhEUgAAAgAAAAIACAYAAAD0eNT6AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAOxAAADsQBlSsOGwAAABl0RVh0U29mdHdhcmUAd3d3Lmlua3NjYXBlLm9yZ5vuPBoAAAKZSURBVHic7dkxAcAgAMPAgV/w72WBrzQdbC/2TKv99wcA/== "; 
        // Wait, that base64 is way too short for 512x512. It's likely invalid or 1x1.
        // Let's try to just make a 1x1 pixel and sips will scale it? Sips -z scales.
        // A 1x1 pixel PNG is tiny.
        // iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPj/HwADBwIAMCb5mQAAAABJRU5ErkJggg== is a 1x1 transparent pixel.
        // Let's do a blue pixel: iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==
        
        try 
        {
            var tempPng = Path.GetTempFileName() + ".png";
            // Blue pixel
            var data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            File.WriteAllBytes(tempPng, data);
            
            ConvertImageToIcns(tempPng, destinationIcns);
            
            if (File.Exists(tempPng)) File.Delete(tempPng);
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error creating default icon: {ex.Message}");
        }
    }
}
