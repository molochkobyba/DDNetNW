# Build DDNetNW

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with `.NET desktop development`

## Build in Visual Studio

1. Open `DDNetNW.csproj`.
2. Select `Release`.
3. Build the project.

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
