# heic2jpeg

[![NuGet](https://img.shields.io/nuget/v/heic2jpeg)](https://www.nuget.org/packages/heic2jpeg)

**This app is 100% vibe-coded. If you don't like something, blame the robots!**

Converts HEIC/HEIF photos (the format iPhones shoot in) to JPEG. The photos can
come from a folder or a ZIP file, and the JPEGs can go to a folder or a ZIP file.
Files are converted in parallel with progress bars, orientation is baked in,
and EXIF metadata and color profiles are preserved.

```
heic2jpeg <SOURCE> <DESTINATION> [--quality 90]
```

Run `heic2jpeg` without arguments to choose the source and destination from a menu
instead (see [Interactive menu](#interactive-menu)).

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

Intel Macs are not supported.

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
DESCRIPTION:
Converts HEIC/HEIF photos to JPEG. Run it without arguments to choose the
folders from a menu

USAGE:
    heic2jpeg <SOURCE> <DESTINATION> [OPTIONS]

EXAMPLES:
    heic2jpeg ~/Pictures/heic ~/Pictures/jpeg
    heic2jpeg ~/Pictures/heic ~/Pictures/jpeg --quality 85
    heic2jpeg ~/Downloads/iCloud.zip ~/Pictures/jpeg
    heic2jpeg ~/Pictures/heic ~/Pictures/jpeg.zip

ARGUMENTS:
    <SOURCE>         Folder or .zip file containing .heic/.heif files
    <DESTINATION>    Folder or .zip file to write .jpg files to. Created if it
                     does not exist

OPTIONS:
                               DEFAULT
    -h, --help                            Prints help information
    -v, --version                         Prints version information
    -q, --quality <QUALITY>    90         JPEG quality, 1-100
```

- **Folder source:** every `.heic` and `.heif` file directly in the folder (not in
  subfolders) is converted to `<same name>.jpg`.
- **ZIP source:** every `.heic` and `.heif` file anywhere in the archive is converted,
  and the archive's folders are kept, so `Album/IMG_0001.HEIC` becomes
  `Album/IMG_0001.jpg`. The `__MACOSX` folder and `._*` files that macOS adds to ZIP
  files are skipped. Nothing is unpacked to disk.
- **Folder destination:** existing files with the same name are overwritten.
- **ZIP destination:** a path ending in `.zip` is written as a ZIP file (unless a
  folder with that name already exists). If the ZIP file exists, its other entries are
  kept and entries with the same name are replaced. The destination can even be the
  source ZIP file, to add the JPEGs next to the photos. A failed or cancelled run
  leaves an existing ZIP file untouched.
- Files that fail to decode are listed at the end and the exit code is 1.
  Everything else is still converted.
- Ctrl+C stops after the photos being converted finish (exit code 130). A ZIP
  destination is left as it was.
- JPEG quality 90 is a good default for photos. Lower it for smaller files.

## Interactive menu

Started without arguments in a terminal (or by double-clicking `heic2jpeg.exe` on
Windows), heic2jpeg shows a menu instead of the usage text:

```
── heic2jpeg ────────────────────────────────────────────────────────────
Converts HEIC/HEIF photos to JPEG. Run heic2jpeg --help for the command-line options.

  Source        ~/Downloads/iCloud Photos.zip (ZIP file, 312 HEIC files)
  Destination   ~/Downloads/iCloud Photos-jpeg (new folder)
  JPEG quality  90
> Convert
  Exit
```

Choosing **Source** or **Destination** opens a folder browser that lists folders and
`.zip` files. Use the arrow keys (also PageUp/PageDown and Home/End) and Enter; Esc
goes back.

```
── Choose the source ────────────────────────────────────────────────────
A folder with .heic/.heif photos, or a .zip file that contains them.
~/Downloads

> [Use this folder] (0 HEIC files)
  ..
  Holiday 2025/
  iCloud Photos.zip  (1.2 GB)
  [Type a path]
  [Back]
```

- **Source:** enter a folder and pick `[Use this folder]`, or pick a `.zip` file.
- **Destination:** pick a folder or an existing `.zip` file, or create a
  `[New folder]` or `[New ZIP file]` (nothing is created until you convert). The
  browser starts next to the source and suggests a name such as `heic-jpeg`.
- `[Type a path]` jumps anywhere, including `~` and paths pasted with quotes.
- On Windows, `..` from the root of a drive lists the drives.

After converting, the summary stays on screen and you can go back to the menu. When
the output is not a terminal (piped or redirected), running without arguments prints
the usage text instead.

## Language

heic2jpeg speaks English and Polish, and follows the system language. Any other
language gets English.

- **Windows:** the Windows display language.
- **Linux and macOS:** the `LC_ALL`, `LC_MESSAGES` or `LANG` environment variable,
  so a single run can be switched with `LANG=pl_PL.UTF-8 heic2jpeg --help`.

Error messages that come from the system (such as "Access denied") or from the
command-line parser (such as "Missing required argument") stay in English.

## License

[MIT](LICENSE)
