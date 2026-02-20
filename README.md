# DazToUnity Bridge

A modernized fork of the [Daz3D DazToUnity Bridge](https://github.com/daz3d/DazToUnity), updated for Unity 6 and all three render pipelines — HDRP, URP, and Built-in.

> **Based on the original work by [Daz 3D](https://github.com/daz3d/DazToUnity).**
> This fork focuses on Unity 6 compatibility, runtime render pipeline detection, UPM packaging, and a unified uDTU shader family for all pipelines.

---

## Table of Contents

1. [What's Different in This Fork](#1-whats-different-in-this-fork)
2. [How to Install](#2-how-to-install)
3. [How to Use](#3-how-to-use)
4. [How to Build](#4-how-to-build)
5. [How to Test](#5-how-to-test)
6. [How to Develop](#6-how-to-develop)

---

## 1. What's Different in This Fork

This fork modernizes the original DazToUnity Bridge with a focus on clean, forward-compatible code:

**Unity 6 compatibility**
- Replaced deprecated `FindObjectsOfType` with `FindObjectsByType`
- Switched to `EditorSceneManager` for scene operations
- Removed Unity 2019 dead code paths
- Added safety around `AvatarSetupTool`

**Runtime render pipeline detection**
- New `RenderPipelineHelper` replaces all `#if USING_HDRP / URP / BUILTIN` preprocessor directives
- Pipeline detection is cached at runtime via `GraphicsSettings.currentRenderPipeline`
- Shader names are runtime properties, not compile-time constants

**UPM package structure**
- Installable via Unity Package Manager from a git URL — no file copying needed
- Proper Runtime and Editor assembly definitions
- SimpleJSON vendor dependency isolated with `autoReferenced: false`

**Unified uDTU shader family for all pipelines**
- HDRP and URP: existing Shader Graph shaders (Hair, Metallic, Specular, SSS, Transparent)
- Built-in RP: new ShaderLab surface shaders matching the same property interface
- All three pipelines use the same material conversion path — no legacy fallback

---

## 2. How to Install

### Daz Studio Plugin

1. Go to the [Releases page](https://github.com/Knuffelpanda/DazToUnity/releases)
2. Download **dzunitybridge.dll**
3. Copy the DLL into your Daz Studio plugins folder:
   ```
   C:\Daz 3D\Applications\64-bit\DAZ 3D\DAZStudio4\plugins\
   ```
4. Restart Daz Studio
5. Verify: **File → Send To → Daz To Unity** appears in the menu

---

### Unity Plugin

#### Option A — UPM / Unity Package Manager (Recommended, Unity 2022.3+)

The cleanest install. No files are copied into your Assets folder.

**From Unity:**
1. Open your project
2. Go to **Window → Package Manager**
3. Click **+** → **Add package from git URL...**
4. Paste:
   ```
   https://github.com/Knuffelpanda/DazToUnity.git?path=UnityPlugin
   ```
5. Click **Add**

**Or edit `Packages/manifest.json` directly:**
```json
"com.daz3d.daz-to-unity": "https://github.com/Knuffelpanda/DazToUnity.git?path=UnityPlugin"
```

**From Daz Studio (automatic):**
1. Open **File → Send To → Daz To Unity**
2. Enable **Advanced Settings**
3. Select **UPM Package (Unity 2022.3+)** from the dropdown
4. Click **Install Plugin** and select your Unity project root
5. Switch to Unity — the Package Manager resolves the package automatically

---

#### Option B — .unitypackage (Classic, Unity 2019–2021)

Use this if you can't use UPM or prefer a self-contained install.

1. Open your Unity project and leave it running
2. In Daz Studio: **File → Send To → Daz To Unity**
3. Enable **Advanced Settings**
4. Select your Unity version and render pipeline from the dropdown
5. Click **Install Plugin** and select your Unity project root
6. A Unity Import Package dialog should appear — click **Import**
7. If it doesn't appear automatically, go to `Assets/Daz3D/Support/` and double-click the matching package:
   - `DazToUnity HDRP.unitypackage`
   - `DazToUnity URP.unitypackage`
   - `DazToUnity Standard Shader.unitypackage`
8. For HDRP: add the Daz diffusion profile to your HDRP asset's diffusion profile list (Project Settings → HDRP Default Settings)

---

## 3. How to Use

1. Open Daz Studio and add a Figure or Prop to your scene
2. Select the top node of your Figure or Prop in the Scene pane
3. Go to **File → Send To → Daz To Unity**
4. Select the Asset Folder for your Unity project
5. Choose the Asset Type:
   - **Skeletal Mesh** — for rigged figures
   - **Animation** — for animation clips
   - **Static Mesh** — for props and environments
6. Optionally enable Morphs or Subdivision levels via the checkboxes
7. Click **Accept** and wait for the confirmation dialog before switching to Unity

Unity will automatically import the FBX and `.dtu` file, apply materials, and set up the prefab.

---

## 4. How to Build

**Requirements:** Daz Studio 4.5+ SDK · Qt 4.8.1 · Autodesk FBX SDK 2020+ · Pixar OpenSubdiv · CMake 3.4+ · Visual Studio 2017+

```bash
git clone --recurse-submodules https://github.com/Knuffelpanda/DazToUnity.git
cmake -B build \
  -DDAZ_SDK_DIR=<path> \
  -DFBX_SDK_DIR=<path> \
  -DOPENSUBDIV_DIR=<path>
cmake --build build --config Release
```

The build output is `dzunitybridge.dll` (Windows) or `dzunitybridge.dylib` (macOS).
Set `-DDAZ_STUDIO_EXE_DIR=<path>` to auto-deploy the plugin to your Daz Studio plugins folder after build.

---

## 5. How to Test

**Unit tests (Windows, C++):**
- Build in Debug mode with `/EHa` (C++ exceptions with SEH) enabled
- In Daz Studio, load `Test/UnitTests/RunUnitTests.dsa`
- Set `sIncludePath` and `sOutputPath` on lines 4–5, then run
- Click through any dialog prompts that appear — they are part of the tests

**Automated test cases (Daz Script):**
- In Daz Studio, load `Test/TestCases/test_runner.dsa`
- Set `sIncludePath` on line 4, then run
- Results are written to `Test/Reports/` as JSON and TXT

**Manual QA:**
- See `Test/QA Manual Test Cases.md` for 16 test scenarios

---

## 6. How to Develop

**C++ plugin (`DazStudioPlugin/`):**
- `DzUnityAction` — triggers the export workflow via File → Send To → Daz To Unity
- `DzUnityDialog` — export configuration UI
- The `DzUnityNS` namespace prevents collisions when multiple bridge plugins are loaded simultaneously
- Uses `CPP_PLUGIN_DEFINITION()` instead of `DZ_PLUGIN_DEFINITION` to enable C++ class export across the bridge library boundary

**C# Unity plugin (`UnityPlugin/`):**
- `Daz3DDTUImporter` — ScriptedImporter for `.dtu` files, drives the entire import pipeline
- `DTUConverter` — material and shader assignment
- `RenderPipelineHelper` — cached runtime pipeline detection, single source of truth for all pipeline branching
- `DetectRenderPipeline` — manages scripting defines for backwards compatibility

**Modifying shaders:**
- HDRP/URP shaders are Shader Graph assets in `UnityPlugin/Shaders/uDTU/`
- Built-in shaders are ShaderLab files in the same folder (`uDTU BuiltIn.*.shader`)
- All shaders share the same `_`-prefixed property names so `DTUConverter` works identically across pipelines

**Updating the .unitypackage files:**
- Make changes inside Unity
- Export a new `.unitypackage` from Unity
- Replace the matching file in `DazStudioPlugin/Resources/`
