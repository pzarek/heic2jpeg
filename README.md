# heic2jpeg

[![CI](https://github.com/pzarek/heic2jpeg/actions/workflows/ci.yml/badge.svg)](https://github.com/pzarek/heic2jpeg/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/heic2jpeg)](https://www.nuget.org/packages/heic2jpeg)

**This app is 100% vibe-coded. If you don't like something, blame the robots!**

Converts a folder of HEIC/HEIF photos (the format iPhones shoot in) to JPEG.
Files are converted in parallel with progress bars, orientation is baked in,
and EXIF metadata and color profiles are preserved.

```
heic2jpeg <SOURCE> <DESTINATION> [--quality 90]
```

## Install

### Option 1: download a binary (no .NET needed)

Grab the file for your platform from the
[latest release](https://github.com/pzarek/heic2jpeg/releases/latest), unpack it,
and run it from a terminal.

| Platform | File |
|---|---|
| Windows x64 | `heic2jpeg-<version>-win-x64.zip` |
| Windows ARM64 | `heic2jpeg-<version>-win-arm64.zip` |
| Linux x64 | `heic2jpeg-<version>-linux-x64.tar.gz` |
| Linux ARM64 | `heic2jpeg-<version>-linux-arm64.tar.gz` |
| macOS Apple Silicon | `heic2jpeg-<version>-osx-arm64.tar.gz` |

Intel Macs are not supported because the bundled ImageMagick library ships no
`osx-x64` build.

The binaries are not code-signed. Windows SmartScreen may warn on first run
(choose "More info" then "Run anyway"). macOS quarantines downloaded executables;
allow it with:

```
xattr -d com.apple.quarantine ./heic2jpeg
```

### Option 2: .NET tool (requires the .NET 10 SDK)

```
dotnet tool install -g heic2jpeg
```

Or run it once without installing:

```
dnx heic2jpeg ~/Pictures/heic ~/Pictures/jpeg
```

## Usage

```
USAGE:
    heic2jpeg <SOURCE> <DESTINATION> [OPTIONS]

EXAMPLES:
    heic2jpeg ~/Pictures/heic ~/Pictures/jpeg
    heic2jpeg ~/Pictures/heic ~/Pictures/jpeg --quality 85

ARGUMENTS:
    <SOURCE>         Folder containing .heic/.heif files
    <DESTINATION>    Folder to write .jpg files to. Created if it does not exist

OPTIONS:
                               DEFAULT
    -h, --help                            Prints help information
    -q, --quality <QUALITY>    90         JPEG quality, 1-100
```

- Every `.heic` and `.heif` file in the source folder (not subfolders) is converted
  to `<same name>.jpg` in the destination folder. Existing files are overwritten.
- Files that fail to decode are listed at the end and the exit code is 1.
  Everything else is still converted.
- JPEG quality 90 is a good default for photos. Lower it for smaller files.

## Build and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
```

The end-to-end tests run the real executable against a sample HEIC file.

## Release

Push a tag of the form `vX.Y.Z` and GitHub Actions will run the tests, attach
self-contained binaries for all five platforms to a GitHub Release, and push the
tool package to NuGet.

## License

[MIT](LICENSE). Built with [Magick.NET](https://github.com/dlemstra/Magick.NET)
and [Spectre.Console](https://spectreconsole.net/).
