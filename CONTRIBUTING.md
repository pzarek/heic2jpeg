# Contributing

[![CI](https://github.com/pzarek/heic2jpeg/actions/workflows/ci.yml/badge.svg)](https://github.com/pzarek/heic2jpeg/actions/workflows/ci.yml)

Notes for building, testing, translating and releasing heic2jpeg. How to install and
use the app is in the [README](README.md), which is for end users only.

heic2jpeg is a .NET 10 console app built with
[Magick.NET](https://github.com/dlemstra/Magick.NET) and
[Spectre.Console](https://spectreconsole.net/).

## Build and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
```

The end-to-end tests run the real executable against a sample HEIC file. The menu
and folder browser are tested in-process with Spectre.Console's test console and
scripted key presses.

## Translations

The texts are in `src/Heic2Jpeg/Strings.resx` (English) and `Strings.pl.resx`
(Polish); the comments in `Strings.resx` say where each one is shown. A test fails if a
text is missing from either file or the two do not use the same `{0}` placeholders.

## Release

Push a tag of the form `vX.Y.Z` and GitHub Actions will run the tests, attach
self-contained binaries for all five platforms to a GitHub Release, push the
tool package to NuGet, and update the package managers:

- **Scoop and Homebrew:** the workflow rewrites `bucket/heic2jpeg.json` and
  `Formula/heic2jpeg.rb` with the new version and checksums and opens a pull
  request with them. It starts CI and the `Packaging` workflow (which installs the
  release through both on Windows, macOS and Linux) on that pull request, which
  merges itself into `master` once CI passes (pull afterwards). Opening the pull
  request needs "Allow GitHub Actions to create and approve pull requests" turned on
  under Settings → Actions → General.
- **winget:** the workflow opens a pull request against
  [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) with
  [WinGet Releaser](https://github.com/vedantmgoyal9/winget-releaser). It needs a
  `WINGET_TOKEN` repository secret (a classic personal access token with the
  `public_repo` scope) and a fork of winget-pkgs under the `pzarek` account. The
  step is skipped while the secret is missing. Microsoft reviews each pull request,
  so a new version can take a few days to show up in winget.

Pre-release tags (`v1.2.3-rc.1`) skip all three.

There is no Intel Mac (`osx-x64`) build because the bundled ImageMagick library
ships no `osx-x64` build.

NuGet publishing uses [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing),
so no API key is stored. It needs two things: a trusted publishing policy on
nuget.org for this repository and the `release.yml` workflow, and a `NUGET_USER`
repository secret holding the nuget.org profile name of the policy owner.
