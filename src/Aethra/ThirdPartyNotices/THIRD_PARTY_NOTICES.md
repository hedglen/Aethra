# Aethra Third-Party Notices

This file tracks native dependencies that may ship with Aethra. Keep it current whenever binaries are added, replaced, or removed.

## Open Source Distribution Rules

- Aethra is intended to be free and published on GitHub.
- Aethra is dual-licensed at the repository level (**MIT OR Apache-2.0**); keep all redistribution obligations explicit in release notes, license files, and notices.
- Prioritize best playback and GPU renderer quality over proprietary/commercial constraints, while maintaining clean provenance and compliance.
- Do not ship FFmpeg `--enable-nonfree`, opaque binaries, or binaries with unclear provenance without an explicit owner decision.
- Keep native media/rendering libraries as separate DLLs.
- Preserve license text, notices, source links, exact versions/commits, and build provenance for redistributed native binaries.
- Do not obscure third-party DLL names.

## Course Correction - Free GitHub Distribution

The earlier LGPL-clean source-build work below is retained as useful provenance and as a working runtime baseline, but it is no longer the only product constraint. Future native-runtime work should choose the best open-source mpv/libplacebo/FFmpeg/ANGLE renderer stack for playback quality, shader support, HDR/tone mapping, and configurability, then document the resulting redistribution obligations.

Current renderer evidence:

- The current source-built runtime exposes mpv OpenGL rendering and does not expose mpv's D3D11 render API.
- ANGLE initializes successfully over D3D11 on this machine.
- Offscreen mpv OpenGL rendering through ANGLE succeeds.
- The visible app still uses the temporary software renderer until the WinUI visible GPU bridge is completed.

Next native-binary decision:

- Decide whether to rebuild the runtime with fuller mpv/FFmpeg/libplacebo features before finishing the visible GPU bridge.
- Continue to avoid FFmpeg `--enable-nonfree` unless the owner explicitly chooses that licensing posture.
- Keep this file updated with every binary copied into `NativeRuntime\x64`.

## Historical Native Binaries And Build Notes

These notes include historical LGPL-clean build work plus the current runtime-bundle baseline.

### libmpv

- Required file: `libmpv-2.dll`
- Architecture: Windows x64
- Historical license posture: LGPL-clean baseline, now superseded by the free/GitHub direction above.
- Source: `https://github.com/mpv-player/mpv`
- Local source checkout: `C:\Users\rjh\source\native-deps\mpv`
- Current source commit: `e046cd0736b7e651bb74fc577596a33fe9635468`
- Commit date: `2026-04-24 20:19:50 +0200`
- Commit subject: `context_menu.lua: add background_alpha script-opt`
- Historical LGPL-clean build requirements:
  - Build from official mpv source, not an opaque third-party prebuilt.
  - mpv configured with `-Dlibmpv=true`.
  - mpv configured with `-Dgpl=false`.
  - FFmpeg built without `--enable-gpl`.
  - FFmpeg built without `--enable-nonfree`.
  - OpenGL render API enabled.
  - Matching source/build configuration available.
- Official build-doc reference: `https://github.com/mpv-player/mpv/blob/master/DOCS/compile-windows.md`
- Official license reference: `https://github.com/mpv-player/mpv/blob/master/Copyright`
- Current status: the existing source-built runtime works for prototyping and as a fallback baseline, but future builds may use fuller GPL-compatible features to improve playback and rendering quality.
- Local build status: several source-built `libmpv-2.dll` variants were produced while exploring a tighter LGPL baseline; the active product direction now allows replacing that baseline with a fuller open-source runtime when useful.

#### Historical Source-Build Route

Use MSYS2 CLANG64 on Windows for reproducible local builds. MSYS2 is not part of the app; it is only a local build environment.

1. Install MSYS2.
2. Open the `CLANG64` environment.
3. Install build tools and dependency packages.
4. Clone `https://github.com/mpv-player/mpv`.
5. Configure with Meson using `-Dlibmpv=true` and `-Dgpl=false`.
6. Ensure FFmpeg dependency provenance confirms no GPL or nonfree options.
7. Build `libmpv-2.dll`.
8. Copy the DLL and matching notices/source-build notes into Aethra only after provenance is recorded here.

#### Local LGPL-Oriented Configure

- Build directory: `C:\Users\rjh\source\native-deps\mpv\build-aethra-lgpl`
- FFmpeg pkg-config path: `C:\Users\rjh\source\native-deps\ffmpeg-lgpl-install\lib\pkgconfig`
- Configure command:

```text
meson setup build-aethra-lgpl -Dlibmpv=true -Dgpl=false -Ddefault_library=shared -Dbuild-date=false -Dlua=disabled -Djavascript=disabled -Ddvdnav=disabled -Dcdda=disabled -Dlibbluray=disabled -Drubberband=disabled -Duchardet=disabled -Dzimg=disabled -Dlcms2=disabled -Dvulkan=disabled -Dd3d11=disabled -Degl=disabled -Degl-angle=enabled -Degl-angle-lib=enabled -Degl-angle-win32=enabled -Dgl=enabled -Dplain-gl=enabled -Dgl-win32=enabled -Dgl-dxinterop=disabled -Dd3d-hwaccel=enabled -Dwasapi=enabled
```

- Meson summary:
  - `libmpv: YES`
  - `opengl: YES`
  - `d3d11: NO`
  - `vulkan: NO`
  - `lua: NO`
  - `javascript: NO`
  - `gpl: false`
- Enabled feature list reported by Meson includes: `egl-angle`, `egl-angle-lib`, `egl-angle-win32`, `ffmpeg`, `gl`, `gl-win32`, `libass`, `libplacebo`, `wasapi`, and `win32-desktop`.
- Local build dependencies installed through MSYS2 CLANG64 for this configure step:
  - `mingw-w64-clang-x86_64-libplacebo 7.360.1-1` (`LGPL2.1`)
  - `mingw-w64-clang-x86_64-libass 0.17.4-3` (`ISC`)
- Important: when `libmpv-2.dll` is built, run dependency inspection before copying binaries into Aethra. Any dynamically linked MSYS2 DLLs that ship with the app need notices and source/provenance review.

#### Local Build Output And Dependency Inspection

- Build command:

```text
ninja -C /c/Users/rjh/source/native-deps/mpv/build-aethra-lgpl libmpv-2.dll
```

- Build result: succeeded.
- Output DLL: `C:\Users\rjh\source\native-deps\mpv\build-aethra-lgpl\libmpv-2.dll`
- Output size: `12,350,976` bytes.
- Import library: `C:\Users\rjh\source\native-deps\mpv\build-aethra-lgpl\libmpv.dll.a`
- Direct import inspection tool: `C:\msys64\clang64\bin\llvm-objdump.exe -p`.
- Recursive dependency inspection result: do not copy this binary set into Aethra yet.

Direct non-system imports from `libmpv-2.dll`:

- Aethra-built FFmpeg DLLs: `avcodec-62.dll`, `avdevice-62.dll`, `avfilter-11.dll`, `avformat-62.dll`, `avutil-60.dll`, `swresample-6.dll`, `swscale-9.dll`.
- MSYS2/ANGLE/media DLLs: `libass-9.dll`, `libplacebo-360.dll`, `libiconv-2.dll`, `libarchive-13.dll`, `zlib1.dll`, `libjpeg-8.dll`, `libshaderc_shared.dll`, `libEGL.dll`.

Recursive non-system DLLs found by walking imports from `libmpv-2.dll`:

- `libmpv-2.dll`
- FFmpeg: `avcodec-62.dll`, `avdevice-62.dll`, `avfilter-11.dll`, `avformat-62.dll`, `avutil-60.dll`, `swresample-6.dll`, `swscale-9.dll`
- MSYS2/third-party: `libarchive-13.dll`, `libass-9.dll`, `libb2-1.dll`, `libbrotlicommon.dll`, `libbrotlidec.dll`, `libbz2-1.dll`, `libc++.dll`, `libcrypto-3-x64.dll`, `libdovi.dll`, `libEGL.dll`, `libexpat-1.dll`, `libfontconfig-1.dll`, `libfreetype-6.dll`, `libfribidi-0.dll`, `libglib-2.0-0.dll`, `libgraphite2.dll`, `libharfbuzz-0.dll`, `libiconv-2.dll`, `libintl-8.dll`, `libjpeg-8.dll`, `liblcms2-2.dll`, `liblz4.dll`, `liblzma-5.dll`, `libpcre2-8-0.dll`, `libplacebo-360.dll`, `libpng16-16.dll`, `libshaderc_shared.dll`, `libspirv-cross-c-shared.dll`, `libunibreak-7.dll`, `libzstd.dll`, `vulkan-1.dll`, `zlib1.dll`.

Dependency concerns before shipping:

- `libdovi.dll` imports `dovi.dll`, which was unresolved by the recursive dependency walker.
- `libplacebo-360.dll` from MSYS2 pulls `libdovi.dll`, `liblcms2-2.dll`, `libshaderc_shared.dll`, `libspirv-cross-c-shared.dll`, and `vulkan-1.dll` even though the mpv Meson configuration disabled Vulkan at mpv level.
- Several package metadata entries are dual-license or mixed-license and need package-level review before redistribution, including `freetype`, `gettext-runtime`, `lcms2`, `lz4`, `xz`, and `zstd`.
- Next likely cleanup: build a smaller local `libplacebo` or reconfigure dependencies so `libdovi`, Vulkan, shaderc/SPIR-V, archive, and unused image-writing support are not dragged into the first app bundle unless we choose those features deliberately.

#### Local Trimmed mpv Build

- Build directory: `C:\Users\rjh\source\native-deps\mpv\build-aethra-trimmed`
- Purpose: reduce the first app-bundle dependency graph while preserving `libmpv`, OpenGL/ANGLE, WASAPI, subtitles, and the local LGPL-clean FFmpeg build.
- Additional disabled features compared with `build-aethra-lgpl`: `libarchive`, `jpeg`, `shaderc`, `spirv-cross`, `d3d-hwaccel`, `d3d9-hwaccel`, and `gl-dxinterop-d3d9`.
- Configure command:

```text
meson setup build-aethra-trimmed -Dlibmpv=true -Dgpl=false -Ddefault_library=shared -Dbuild-date=false -Dlua=disabled -Djavascript=disabled -Ddvdnav=disabled -Dcdda=disabled -Dlibbluray=disabled -Drubberband=disabled -Duchardet=disabled -Dzimg=disabled -Dlcms2=disabled -Dlibarchive=disabled -Djpeg=disabled -Dshaderc=disabled -Dspirv-cross=disabled -Dvulkan=disabled -Dd3d11=disabled -Dd3d-hwaccel=disabled -Dd3d9-hwaccel=disabled -Degl=disabled -Degl-angle=enabled -Degl-angle-lib=enabled -Degl-angle-win32=enabled -Dgl=enabled -Dplain-gl=enabled -Dgl-win32=enabled -Dgl-dxinterop=disabled -Dgl-dxinterop-d3d9=disabled -Dwasapi=enabled
```

- Build command:

```text
ninja -C /c/Users/rjh/source/native-deps/mpv/build-aethra-trimmed libmpv-2.dll
```

- Build result: succeeded.
- Output DLL: `C:\Users\rjh\source\native-deps\mpv\build-aethra-trimmed\libmpv-2.dll`
- Output size: `11,670,016` bytes.
- Direct non-system import improvement: `libarchive-13.dll`, `libjpeg-8.dll`, and direct `libshaderc_shared.dll` imports were removed from `libmpv-2.dll`.
- Remaining direct non-system imports from trimmed `libmpv-2.dll`:
  - Aethra-built FFmpeg DLLs: `avcodec-62.dll`, `avdevice-62.dll`, `avfilter-11.dll`, `avformat-62.dll`, `avutil-60.dll`, `swresample-6.dll`, `swscale-9.dll`.
  - MSYS2/ANGLE/media DLLs: `libass-9.dll`, `libplacebo-360.dll`, `libiconv-2.dll`, `zlib1.dll`, `libEGL.dll`.
- Recursive dependency inspection still found `libdovi.dll`, `libshaderc_shared.dll`, `libspirv-cross-c-shared.dll`, `vulkan-1.dll`, and unresolved `dovi.dll` through MSYS2 `libplacebo-360.dll`.
- Current decision: the trimmed mpv build is a better candidate than `build-aethra-lgpl`, but still should not be copied into Aethra until `libplacebo` is rebuilt or otherwise sourced without the unwanted transitive dependencies.

#### Local Minimal libplacebo Build

- Source: `https://github.com/haasn/libplacebo`
- Local source checkout: `C:\Users\rjh\source\native-deps\libplacebo`
- Current source commit: `409c9a822527693dcb5f60c5e37a74a85fae7204`
- Commit date: `2026-04-15T22:26:55Z`
- Commit subject: `vulkan/context: use `VK_KHR_internally_synchronized_queues``
- License file: LGPL 2.1.
- Submodules initialized through `git submodule update --init` for the source tree's declared dependencies.
- Build directory: `C:\Users\rjh\source\native-deps\libplacebo\build-aethra-minimal`
- Install prefix: `C:\Users\rjh\source\native-deps\libplacebo-minimal-install`
- Configure command:

```text
meson setup build-aethra-minimal --prefix=/c/Users/rjh/source/native-deps/libplacebo-minimal-install -Ddefault_library=shared -Ddemos=false -Dtests=false -Dbench=false -Dfuzz=false -Dvulkan=disabled -Dvk-proc-addr=disabled -Dd3d11=disabled -Dglslang=disabled -Dshaderc=disabled -Dlcms=disabled -Ddovi=disabled -Dlibdovi=disabled -Dunwind=disabled -Dxxhash=disabled -Dopengl=enabled -Dgl-proc-addr=enabled
```

- Build/install command:

```text
ninja -C /c/Users/rjh/source/native-deps/libplacebo/build-aethra-minimal install
```

- Build result: succeeded.
- Output DLL: `C:\Users\rjh\source\native-deps\libplacebo-minimal-install\bin\libplacebo-362.dll`
- Output size: `4,184,064` bytes.
- `libplacebo.pc` reports:
  - `Version: 7.362.0`
  - `pl_has_opengl=1`
  - `pl_has_gl_proc_addr=1`
  - `pl_has_vulkan=0`
  - `pl_has_d3d11=0`
  - `pl_has_shaderc=0`
  - `pl_has_glslang=0`
  - `pl_has_lcms=0`
  - `pl_has_dovi=0`
  - `pl_has_libdovi=0`
- Direct imports from `libplacebo-362.dll`: `libc++.dll` plus Windows runtime/system DLLs only.

#### Local mpv Build Against Minimal libplacebo

- Build directory: `C:\Users\rjh\source\native-deps\mpv\build-aethra-localdeps`
- `PKG_CONFIG_PATH` order: local minimal `libplacebo` first, then local LGPL-clean FFmpeg.
- Configure command: same as the trimmed mpv build, with `PKG_CONFIG_PATH` set to:

```text
/c/Users/rjh/source/native-deps/libplacebo-minimal-install/lib/pkgconfig:/c/Users/rjh/source/native-deps/ffmpeg-lgpl-install/lib/pkgconfig
```

- Meson confirmed `Run-time dependency libplacebo found: YES 7.362.0`.
- Build command:

```text
ninja -C /c/Users/rjh/source/native-deps/mpv/build-aethra-localdeps libmpv-2.dll
```

- Build result: succeeded.
- Output DLL: `C:\Users\rjh\source\native-deps\mpv\build-aethra-localdeps\libmpv-2.dll`
- Output size: `11,649,024` bytes.
- Direct non-system imports from this `libmpv-2.dll`:
  - Aethra-built FFmpeg DLLs: `avcodec-62.dll`, `avdevice-62.dll`, `avfilter-11.dll`, `avformat-62.dll`, `avutil-60.dll`, `swresample-6.dll`, `swscale-9.dll`.
  - Other runtime/media DLLs: `libass-9.dll`, `libplacebo-362.dll`, `libiconv-2.dll`, `zlib1.dll`, `libEGL.dll`.
- Recursive dependency inspection found no unresolved DLLs.
- Recursive dependency inspection no longer includes `libdovi.dll`, `libshaderc_shared.dll`, `libspirv-cross-c-shared.dll`, `vulkan-1.dll`, or `liblcms2-2.dll`.
- Remaining non-system DLLs are now mostly FFmpeg, ANGLE, `libplacebo`, `libass` and its subtitle/font shaping stack, `libc++`, and compression/text runtime dependencies. These still need notices/provenance before copying into Aethra, but the previous blocker is resolved.

#### Aethra Native Runtime Bundle

- Project folder: `NativeRuntime\x64`
- Status: copied into the project as the side-by-side runtime bundle. The old root-level prototype `libmpv-2.dll` is removed; runtime resolution should come from `NativeRuntime\x64`.
- Project output behavior: `NativeRuntime\x64\*.dll` is included as content with `CopyToOutputDirectory=PreserveNewest`.
- The app is not yet wired to prefer this folder; that should be done in a separate reviewed step by adding a native DLL search path/resolver before the first mpv P/Invoke.

Copied runtime DLLs:

- `avcodec-62.dll`
- `avdevice-62.dll`
- `avfilter-11.dll`
- `avformat-62.dll`
- `avutil-60.dll`
- `libass-9.dll`
- `libbrotlicommon.dll`
- `libbrotlidec.dll`
- `libbz2-1.dll`
- `libc++.dll`
- `libEGL.dll`
- `libexpat-1.dll`
- `libfontconfig-1.dll`
- `libfreetype-6.dll`
- `libfribidi-0.dll`
- `libGLESv2.dll`
- `libglib-2.0-0.dll`
- `libgraphite2.dll`
- `libharfbuzz-0.dll`
- `libiconv-2.dll`
- `libintl-8.dll`
- `liblzma-5.dll`
- `libmpv-2.dll`
- `libpcre2-8-0.dll`
- `libplacebo-362.dll`
- `libpng16-16.dll`
- `libunibreak-7.dll`
- `swresample-6.dll`
- `swscale-9.dll`
- `zlib1.dll`

Source paths:

- `libmpv-2.dll`: `C:\Users\rjh\source\native-deps\mpv\build-aethra-localdeps`
- FFmpeg DLLs: `C:\Users\rjh\source\native-deps\ffmpeg-lgpl-install\bin`
- `libplacebo-362.dll`: `C:\Users\rjh\source\native-deps\libplacebo-minimal-install\bin`
- ANGLE/MSYS2 runtime DLLs: `C:\msys64\clang64\bin`

Important notice/provenance gap before distribution:

- This bundle is present for local development and integration.
- Before any public binary distribution, add/verify license text and source/provenance coverage for every non-system DLL in this bundle, especially the MSYS2-provided subtitle/font/text/compression stack and ANGLE.

#### Dependency Licensing Checkpoints

- mpv `Copyright` confirms `-Dgpl=false` disables GPL-only mpv source files, but linked libraries can still affect the final license.
- MSYS2 CLANG64 `mingw-w64-clang-x86_64-ffmpeg 8.1-3` is explicitly `GPL-3.0-or-later`; do not use it for Aethra's commercial distribution build.
- MSYS2 CLANG64 `mingw-w64-clang-x86_64-libplacebo 7.360.1-1` reports `LGPL2.1` and is installed locally as an mpv build dependency.
- MSYS2 CLANG64 `mingw-w64-clang-x86_64-libass 0.17.4-3` reports `ISC` and is installed locally as an mpv build dependency.
- MSYS2 CLANG64 `mingw-w64-clang-x86_64-luajit 2.1.1774896198-1` reports `MIT`.
- Next native dependency decision: use the local LGPL-clean FFmpeg source build below when configuring distribution `libmpv-2.dll`.

## FFmpeg LGPL Compliance Checklist

Source: `https://www.ffmpeg.org/legal.html`

For any FFmpeg binaries or FFmpeg-derived libraries distributed with Aethra:

- Compile FFmpeg without `--enable-gpl`.
- Compile FFmpeg without `--enable-nonfree`.
- Use dynamic linking on Windows through FFmpeg DLLs.
- Distribute the exact matching FFmpeg source code for the binaries.
- Record any local source changes with `git diff > changes.diff`.
- Record the exact FFmpeg configure/build command line in the source bundle.
- Distribute the FFmpeg source as a tarball or zip.
- Host the FFmpeg source on the same server/location family as the Aethra binary download.
- Mention FFmpeg and LGPLv2.1 on every website/download page that offers Aethra binaries.
- Mention FFmpeg LGPL use in the app About surface.
- Mention FFmpeg LGPL use in the EULA, if Aethra has one.
- Ensure any EULA does not claim ownership of FFmpeg.
- Remove any EULA prohibition on reverse engineering that conflicts with LGPL.
- Apply the same EULA changes to translations.
- Spell `FFmpeg` correctly.
- Do not obfuscate or disguise FFmpeg DLL names.
- Repeat this checklist for LGPL external libraries compiled into FFmpeg.
- Ensure Aethra's distribution build does not use GPL libraries, notably GPL `libx264`.

### Local LGPL-Clean FFmpeg Build

- Source: `https://github.com/FFmpeg/FFmpeg`
- Local source checkout: `C:\Users\rjh\source\native-deps\ffmpeg`
- Current source commit: `45fe315cf02c2ebb334b5320a3c0dd4df301bad6`
- Commit date: `2026-04-24 16:04:48 -0300`
- Commit subject: `tests/fate/mpegts: add tests for LCEVC samples`
- Install prefix: `C:\Users\rjh\source\native-deps\ffmpeg-lgpl-install`
- Configure command:

```text
./configure --prefix=/c/Users/rjh/source/native-deps/ffmpeg-lgpl-install --pkg-config=pkg-config --cc=clang --cxx=clang++ --ar=llvm-ar --ranlib=llvm-ranlib --nm=llvm-nm --enable-shared --disable-static --disable-programs --disable-doc --disable-debug --enable-ffprobe
```

- Configure summary reported: `License: LGPL version 2.1 or later`.
- Verified `ffbuild/config.mak` values:
  - `CONFIG_SHARED=yes`
  - `!CONFIG_STATIC=yes`
  - `!CONFIG_GPL=yes`
  - `!CONFIG_NONFREE=yes`
- Verified `ffprobe.exe -hide_banner -L` reports LGPL redistribution terms.
- Installed DLLs:
  - `avcodec-62.dll`
  - `avdevice-62.dll`
  - `avfilter-11.dll`
  - `avformat-62.dll`
  - `avutil-60.dll`
  - `swresample-6.dll`
  - `swscale-9.dll`
- Build status: built and installed locally from source; no FFmpeg binaries have been copied into the Aethra project yet.

### ANGLE

- Required files: `libEGL.dll`, `libGLESv2.dll`
- Architecture: Windows x64
- License posture: BSD-style permissive
- Required notice: include ANGLE license text and source link with distributed binaries.
- Current status: installed locally through MSYS2 CLANG64, not yet copied into the Aethra project.
- Local package: `mingw-w64-clang-x86_64-angleproject 2.1.r25748.890b5d8f-1`
- Local DLLs:
  - `C:\msys64\clang64\bin\libEGL.dll`
  - `C:\msys64\clang64\bin\libGLESv2.dll`
- Local license file: `C:\msys64\clang64\share\licenses\angleproject\LICENSE`

## Local Native Build Environment

MSYS2 is installed locally at `C:\msys64`. This is a build environment only and is not part of the app distribution.

Verified CLANG64 tools:

- `clang 22.1.4`
- `meson 1.11.1`
- `ninja 1.13.2`
- `cmake 4.3.2`
- `git 2.54.0`

## Current Managed Dependencies

- Microsoft Windows App SDK: MIT licensed.
- Microsoft Windows SDK Build Tools: Microsoft SDK tooling.
- Endpne.LibMPV.Windows: NuGet metadata declares `LGPL-2.1-or-later`; bundled native binary needs provenance verification before commercial distribution.
