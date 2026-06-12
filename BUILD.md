# Build DDNetNW

This guide is for building a local release package from the source code.

## Requirements

- Windows 10 or Windows 11
- Visual Studio 2022
- `.NET desktop development` workload
- .NET 8 SDK

## Build in Visual Studio

1. Open `DDNetNW.csproj`.
2. Wait until Visual Studio restores the project.
3. At the top toolbar, select `Release`.
4. Select `Any CPU` or `x64`.
5. Click `Build` → `Build Solution`.

## Publish in Visual Studio

1. Right-click the project in Solution Explorer.
2. Click `Publish`.
3. Choose `Folder`.
4. Choose a local output folder.
5. Use a Windows x64 release profile.
6. Publish the app.
7. Zip the published output folder.

## Publish from terminal

Open a terminal in the project folder and run:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```

Output folder:

```text
bin/Release/net8.0-windows/win-x64/publish/
```

Zip the files from that folder for a GitHub Release.

## What to attach to GitHub Releases

Attach the compiled user package, for example:

```text
DDNetNW-v1.35-win-x64.zip
```

Do not commit this archive into the repository. Attach it to the release page instead.

## What not to upload

Do not upload these folders as source files:

```text
bin/
obj/
.vs/
publish/
```

They are build outputs or local IDE files.
