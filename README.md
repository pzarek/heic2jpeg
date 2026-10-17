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

### Package managers

Scoop (Windows x64, ARM64) and Homebrew (macOS Apple Silicon, Linux x64, ARM64)
install from this repository, so add it as a bucket or tap first:

```
scoop bucket add heic2jpeg https://github.com/pzarek/heic2jpeg
scoop install heic2jpeg/heic2jpeg
```

```
brew tap pzarek/heic2jpeg https://github.com/pzarek/heic2jpeg
brew install pzarek/heic2jpeg/heic2jpeg
```

Upgrade later with `scoop update heic2jpeg` or `brew upgrade heic2jpeg`. Neither
needs .NET installed, and Homebrew skips the macOS quarantine prompt described below.

### Download a binary (no .NET needed)

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

### .NET tool (requires the .NET 10 SDK)

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
self-contained binaries for all five platforms to a GitHub Release, push the
tool package to NuGet, and update the package managers:

- **Scoop and Homebrew:** the workflow rewrites `bucket/heic2jpeg.json` and
  `Formula/heic2jpeg.rb` with the new version and checksums, commits them to
  `master` (pull afterwards), and starts the `Packaging` workflow, which installs
  the release through both on Windows, macOS and Linux.
- **winget:** the workflow opens a pull request against
  [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) with
  [WinGet Releaser](https://github.com/vedantmgoyal9/winget-releaser). It needs a
  `WINGET_TOKEN` repository secret (a classic personal access token with the
  `public_repo` scope) and a fork of winget-pkgs under the `pzarek` account. The
  step is skipped while the secret is missing. Microsoft reviews each pull request,
  so a new version can take a few days to show up in winget.

Pre-release tags (`v1.2.3-rc.1`) skip all three.

NuGet publishing uses [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing),
so no API key is stored. It needs two things: a trusted publishing policy on
nuget.org for this repository and the `release.yml` workflow, and a `NUGET_USER`
repository secret holding the nuget.org profile name of the policy owner.

## License

[MIT](LICENSE). Built with [Magick.NET](https://github.com/dlemstra/Magick.NET)
and [Spectre.Console](https://spectreconsole.net/).
