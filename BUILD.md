# Build instructions

## Development run

```bash
dotnet run
```

## Release build

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

## Portable single-file build

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output executable is `DDNetNW.exe`.
