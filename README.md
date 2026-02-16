# AppBundleCreator

A simple C# console application that automates the creation of macOS `.app` bundles from .NET project files (`.csproj`). This tool handles publishing the .NET application, organizing the output into the correct bundle structure, generating an `Info.plist`, and processing application icons.

## Features

- **Automated Publishing**: Runs `dotnet publish` with optimized settings for macOS.
- **Bundle Structure**: Creates the standard `.app/Contents/MacOS` and `.app/Contents/Resources` directory layout.
- **Info.plist Generation**: Automatically generates a valid `Info.plist` file pointing to your executable.
- **Icon Handling**: 
    - Supports `.icns`, `.png`, `.jpg`, and `.jpeg` formats.
    - Automatically converts image files to `.icns` using macOS native tools (`sips` and `iconutil`).
    - Generates a default placeholder icon if none is provided.
- **Permission Fixes**: Ensures the main executable has the correct Unix execute permissions (`chmod +x`).

## Prerequisites

- **.NET 9.0 SDK**: Required to build and run this tool, and to publish the target projects.
- **macOS**: Required for icon conversion features (relies on `/usr/bin/iconutil` and `sips`). The basic bundle creation *might* work on other platforms, but icon conversion is macOS-specific.

## Usage

Run the tool from the command line, providing the path to your `.csproj` file as the first argument.

```bash
AppBundleCreator <path-to-csproj> [output-dir] [app-name] [icon-path] [runtime]
```

### Arguments

1.  **path-to-csproj** (Required): Path to the .NET project file you want to bundle.
2.  **output-dir** (Optional): Directory where the `.app` bundle will be created. Defaults to the current directory (`./`).
3.  **app-name** (Optional): Name of the application bundle (e.g., `MyApp`). Defaults to the project filename.
4.  **icon-path** (Optional): Path to an icon file (`.icns`, `.png`, `.jpg`). If not provided, a default icon is generated.
5.  **runtime** (Optional): Runtime identifier to publish for. Defaults to `osx-arm64`.

### Examples

**Basic Usage:**
Generates `MyProject.app` in the current directory.
```bash
AppBundleCreator ./MyProject/MyProject.csproj
```

**Custom Output and Name:**
Generates `CoolApp.app` in the `Dist` folder.
```bash
AppBundleCreator ./MyProject/MyProject.csproj ./Dist CoolApp
```

**With Icon and specific Runtime:**
Generates `CoolApp.app` with a custom icon for Intel Macs.
```bash
AppBundleCreator ./MyProject/MyProject.csproj ./Dist CoolApp ./Assets/icon.png osx-x64
```

## How It Works

1.  **Publish**: Executes `dotnet publish` with the specified runtime (default `osx-arm64`), creating a self-contained release build.
2.  **Structure**: Creates the `.app` folder and the required `Contents/MacOS` and `Contents/Resources` subdirectories.
3.  **Copy**: Copies all published files to `Contents/MacOS`.
4.  **Clean**: Removes unnecessary file extensions (`.pdb`, `.xml`, etc.) if configured (currently copies all files).
5.  **Plist**: Writes an `Info.plist` file that defines the bundle identifier, executable name, and icon file.
6.  **Icons**:
    - If a source image is provided, it uses `sips` to generate an `.iconset` and `iconutil` to compile it into an `.icns` file in `Contents/Resources`.
    - If no icon is provided, a default base64-encoded icon is used.
7.  **Permissions**: Sets the executable bit on the main binary to ensure it can run on macOS.
