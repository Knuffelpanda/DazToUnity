# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DazToUnity is a bridge plugin that transfers Daz Studio characters and props to the Unity game engine. It consists of two parts:
- **DazStudioPlugin/** — C++ Daz Studio plugin (exports FBX + DTU JSON metadata)
- **UnityPlugin/** — C# Unity Editor scripts (imports and converts assets, sets up materials/shaders)
- **dzbridge-common/** — Git submodule (DazBridgeUtils) providing the shared Daz Bridge Library

## Build System

**Requirements:** Daz Studio 4.5+ SDK, Qt 4.8.1, Autodesk FBX SDK (2020+), Pixar OpenSubdiv, CMake 3.4+, C++ compiler (VS2017+)

**CMake configure:**
```bash
git submodule init && git submodule update
cmake -B build -DDAZ_SDK_DIR=<path> -DFBX_SDK_DIR=<path> -DOPENSUBDIV_DIR=<path>
```
Key CMake variables: `DAZ_SDK_DIR`, `FBX_SDK_DIR`, `OPENSUBDIV_DIR`, `DAZ_STUDIO_EXE_DIR` (optional, auto-deploys built plugin to Daz plugins folder).

**Build target:** `dzunitybridge` (shared library — .dll on Windows, .dylib on macOS)

The dzbridge-common submodule is statically linked (`USE_DZBRIDGE_STATIC=ON`). Unit tests are only built on Windows.

## Testing

**Unit tests** (Windows only, C++):
- Build Debug with SEH exceptions enabled (`/EHa` in C++ Code Generation)
- Run via Daz Studio: load `Test/UnitTests/RunUnitTests.dsa`, configure `sIncludePath`/`sOutputPath` on lines 4-5
- Dialog prompts appear during tests — click OK/Cancel to advance

**Automated test cases** (Daz Script):
- Run via Daz Studio: load `Test/TestCases/test_runner.dsa`, configure `sIncludePath` on line 4
- Results go to `Test/Reports/` as JSON/TXT (formatted to only change when test results change, for clean git diffs)

**Manual QA:** See `Test/QA Manual Test Cases.md` for 16 test scenarios.

## Architecture

**Data flow:** Daz Studio → (FBX + .dtu JSON) → Unity importer

C++ side (`DazStudioPlugin/`):
- `DzUnityAction` extends `DzBridgeAction` — triggers export workflow via File → Send To → Daz To Unity
- `DzUnityDialog` extends `DzBasicDialog` — configuration UI for export parameters
- `pluginmain.cpp` — plugin registration using custom `CPP_PLUGIN_DEFINITION()` macro (NOT `DZ_PLUGIN_DEFINITION`) to enable C++ class export across the bridge library boundary

C# side (`UnityPlugin/`):
- `Daz3DDTUImporter` — ScriptedImporter for `.dtu` files (core import logic, ~1400 LOC)
- `Daz3DBridge` — EditorWindow, auto-initializes via `[InitializeOnLoad]`
- `DTUConverter` — material conversion and shader assignment
- `DetectRenderPipeline` — auto-detects HDRP/URP/Built-in and selects appropriate shaders
- Pre-built `.unitypackage` files in `DazStudioPlugin/Resources/` for each pipeline variant

## Key Conventions

- The bridge library uses `DzUnityNS` namespace to avoid collisions with other Daz bridge plugins loaded simultaneously.
- Unity packages for different render pipelines (HDRP, URP, Standard) and Unity versions (2019, 2020+) are stored as binary `.unitypackage` files in `DazStudioPlugin/Resources/`.
- Debug builds automatically define `UNITTEST_DZBRIDGE` on Windows (see `DazStudioPlugin/CMakeLists.txt`).
- Modifying Unity plugin files requires exporting a `.unitypackage` from Unity and replacing the corresponding file in `DazStudioPlugin/Resources/`.
