# Test fixtures

| File | Source | License |
|------|--------|---------|
| `example.heic` | [libheif](https://github.com/strukturag/libheif) repository, `examples/example.heic` (1280x854) | MIT (libheif distributes its sample applications and their files under the MIT License) |

HEIC cannot be generated at test time: Magick.NET bundles a HEIC decoder but no encoder.
The tests therefore copy this one file under different names to build their input folders.
