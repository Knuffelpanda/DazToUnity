# Daz To Unity Bridge — User Manual

**Version 2023.1**

---

## Table of Contents

1. [Introduction](#introduction)
2. [Installation](#installation)
3. [Quick Start](#quick-start)
4. [Daz Studio — Export Dialog](#daz-studio--export-dialog)
5. [Unity — Import Workflow](#unity--import-workflow)
6. [Auto-Setup Features](#auto-setup-features)
7. [Animation Workflow](#animation-workflow)
8. [Material System](#material-system)
9. [Post-Import Tools](#post-import-tools)
10. [The Daz3D Bridge Window](#the-daz3d-bridge-window)
11. [Troubleshooting](#troubleshooting)

---

## 1. Introduction

The **Daz To Unity Bridge** is a plugin that lets you send characters, props, and animations from Daz Studio directly into Unity. It handles the full pipeline: FBX export, material conversion, prefab creation, and optional auto-setup of LOD, ragdoll physics, morph animation clips, and hair simulation.

**Supported render pipelines:**
- High Definition Render Pipeline (HDRP)
- Universal Render Pipeline (URP)
- Built-In (Standard Shader)

**Supported Unity versions:** 2019, 2020 and newer (2022.3+ via UPM)

---

## 2. Installation

### 2.1 Installing the Daz Studio Plugin

1. Place `dzunitybridge.dll` (Windows) or `dzunitybridge.dylib` (macOS) in your Daz Studio plugins folder.
2. Restart Daz Studio.
3. Verify the plugin is loaded via **Help → About Installed Plug-ins**.

### 2.2 Installing the Unity Package

The Unity-side package must be installed once per Unity project.

**Option A — From the Daz Studio export dialog (recommended):**

1. Open the export dialog via **File → Send To → Daz To Unity**.
2. In the **Unity Plugin Installer** section, select your render pipeline and Unity version.
3. Click **Install Plugin** and select your Unity project root folder.
4. The correct `.unitypackage` is automatically copied and imported.

**Option B — UPM (Unity 2022.3+):**

1. In Unity, open **Window → Package Manager**.
2. Click **+** → **Add package from git URL**.
3. Enter:
   ```
   https://github.com/Knuffelpanda/DazToUnity.git?path=UnityPlugin
   ```

**Option C — Manual:**

1. In Unity, go to `Assets/Daz3D/Support/`.
2. Double-click the `.unitypackage` that matches your render pipeline.

---

## 3. Quick Start

### Exporting a Character

1. In Daz Studio, load your character and select its root node.
2. Go to **File → Send To → Daz To Unity**.
3. Set **Asset Type** to **Skeletal Mesh**.
4. Set the **Unity Assets Folder** to the `Assets/` folder of your Unity project.
5. Enable any auto-setup options you want (LOD, ragdoll, morphs, hair physics).
6. Click **Accept**.

### Receiving in Unity

1. Unity detects the incoming `.dtu` file automatically.
2. The **Daz3D Bridge** window opens and shows import progress.
3. Once finished, find your character prefab in `Assets/Daz3D/[AssetName]/`.
4. Drag the prefab into your scene.

---

## 4. Daz Studio — Export Dialog

Open via **File → Send To → Daz To Unity**.

### 4.1 Basic Settings

| Field | Description |
|-------|-------------|
| **Asset Name** | The name the asset will use in Unity. Auto-filled from the selected node. |
| **Asset Type** | What kind of asset to export (see below). |
| **Unity Assets Folder** | Path to the `Assets/` folder of your Unity project. |

### 4.2 Asset Types

| Type | Use For |
|------|---------|
| **Skeletal Mesh** | Rigged characters and creatures. Exports bones, weights, and optionally morphs. |
| **Static Mesh** | Props and non-rigged objects. Exports geometry and materials only. |
| **Animation** | A skeletal animation clip. Automatically named `CharacterName@anim0000`, `@anim0001`, etc. |

> **Note:** *Environment* and *Pose* are not yet supported and are greyed out.

### 4.3 Auto-Setup Options

These checkboxes control what is automatically configured in Unity after import.

| Option | What It Does |
|--------|-------------|
| **Auto Generate LOD** | Creates 4 levels of detail (100%, 50%, 25%, 10%). Requires UnityMeshSimplifier. |
| **Auto Setup Ragdoll** | Adds colliders, rigidbodies, and joints for physics ragdoll. |
| **Auto Morph Clips** | Generates one AnimationClip per blend shape/morph. |
| **Auto Hair Physics** | Enables dForce spring-based hair simulation on hair meshes. |

### 4.4 Export glTF

When **Export glTF (.glb)** is checked, a binary glTF 2.0 file is exported alongside the FBX. Useful for web or other real-time engines.

### 4.5 Unity Plugin Installer

Use this section to install or update the Unity plugin package in your project.

1. Select your **Render Pipeline** and **Unity Version** from the dropdown.
2. Click **Install Plugin**.
3. Select your Unity project root folder.

---

## 5. Unity — Import Workflow

### 5.1 What Happens on Import

When a `.dtu` file arrives in your Unity project, the bridge automatically:

1. **Parses** the DTU file for materials, morphs, bones, and settings.
2. **Converts materials** — each Daz material is mapped to the correct Unity shader for your render pipeline.
3. **Imports the FBX** — mesh, skeleton, and animation data.
4. **Creates a prefab** with all materials applied.
5. **Runs auto-setup** steps (LOD, ragdoll, morphs, hair) if enabled.

### 5.2 Output Folder Structure

```
Assets/
└── Daz3D/
    └── [AssetName]/
        ├── [AssetName].fbx
        ├── [AssetName].dtu
        ├── [AssetName].prefab       ← ready to use in your scene
        ├── [AssetName].glb           ← only if glTF export was enabled
        ├── Textures/
        │   └── *.png / *.jpg
        └── Materials/
            └── *.mat
```

Animation clips from morph generation are stored separately:

```
Assets/
└── MorphClips/
    └── [AssetName]/
        ├── [AssetName]_[MorphName].anim
        └── [AssetName]_ResetAll.anim
```

### 5.3 Mecanim Avatar (Humanoid Rig)

When **Automatically setup the Mecanim Avatar** is enabled in the Daz3D Bridge options, the FBX is configured as a **Humanoid** rig. This means:

- Daz bone names are automatically mapped to Unity's humanoid bone structure.
- Animations can be **retargeted** — play an animation from one character on a different character.
- Standard Unity Humanoid animations (from the Asset Store or Mixamo) can be used directly.

> If you turn this option off, the character is imported as a **Generic** rig. Animations will only work on the exact same skeleton.

---

## 6. Auto-Setup Features

### 6.1 LOD Generator

Generates 4 levels of detail to improve performance at a distance.

| Level | Vertices | Screen Height Threshold |
|-------|---------|------------------------|
| LOD0  | 100%    | > 60%                  |
| LOD1  | 50%     | > 30%                  |
| LOD2  | 25%     | > 10%                  |
| LOD3  | 10%     | > 2%                   |

**Requirement:** Install **UnityMeshSimplifier** via **Window → Package Manager → Add package from git URL**:
```
https://github.com/Whinarn/UnityMeshSimplifier.git
```

### 6.2 Ragdoll Setup

Automatically configures a physics ragdoll on the imported character.

- **CapsuleColliders** are added to each bone (sized by bone length).
- **Rigidbodies** are added with anatomically appropriate masses (e.g., hips = 15 kg, head = 5 kg, hands = 1 kg).
- **CharacterJoints** are added with realistic rotation limits.
- A **DazRagdollBlender** component is added to the root, allowing you to smoothly blend between animation and ragdoll physics at runtime.

**Supported bones:** hips, spine, abdomen, chest, head, upper/lower arms, hands, upper/lower legs, feet.

### 6.3 Morph Animation Clips

Generates an `AnimationClip` for every blend shape on the character.

- Each clip is **1 second long**: weight goes from 0 (frame 0) to 100 (frame 60).
- A **Reset All** clip is also created to return all morphs to zero.
- Clips can be used directly in an Animator Controller for facial animation, expressions, and body morphs.

### 6.4 Hair Physics (dForce)

Adds spring-based hair simulation to hair meshes exported with dForce properties.

- Uses **Verlet integration** — each bone in the hair chain is treated as a particle.
- Parameters (stiffness, damping, gravity) are read directly from the dForce material settings in Daz Studio.
- The **DazHairPhysics** component can be adjusted in the Inspector after import.

| Parameter | Description |
|-----------|-------------|
| **Dynamics Strength** | How much physics overrides the original animation (0 = none, 1 = full). |
| **Stiffness** | How strongly hair springs back to its rest pose. |
| **Damping** | How quickly hair velocity slows down. |
| **Gravity** | Gravity influence multiplier. |

---

## 7. Animation Workflow

### 7.1 Exporting an Animation

1. In Daz Studio, apply or create your animation on the character timeline.
2. Select the character root node.
3. Open **File → Send To → Daz To Unity**.
4. Set **Asset Type** to **Animation**.
5. The asset name is automatically set to `CharacterName@anim0000` (increments automatically for each new export).
6. Click **Accept**.

The animation is exported as a separate FBX and stored in the same folder as the base character.

### 7.2 Using Animations in Unity

After import, the animation clip is available in `Assets/Daz3D/[CharacterName]/`.

- Assign it to an **Animator Controller** state.
- If the character uses a **Humanoid** rig, the clip can be used on any other Humanoid character.

### 7.3 Multiple Animations

Each animation export creates a new numbered file:
```
Assets/Daz3D/Genesis8Female/
├── Genesis8Female.fbx              ← base character
├── Genesis8Female@anim0000.fbx     ← first animation
├── Genesis8Female@anim0001.fbx     ← second animation
└── Genesis8Female@anim0002.fbx     ← third animation
```

---

## 8. Material System

### 8.1 Render Pipeline Detection

The bridge automatically detects your render pipeline on import and selects the correct shaders. No manual configuration is needed.

### 8.2 Shader Mapping

| Daz Material Type | Unity Shader |
|------------------|-------------|
| IrayUber (Metal/Roughness) | `Daz3D/IrayUberMetal` / `uDTU URP.Metallic` |
| IrayUber (Specular/Glossiness) | `Daz3D/IrayUberSpec` / `uDTU URP.Specular` |
| IrayUber (Skin / SSS) | `Daz3D/IrayUberSkin` / `uDTU URP.SSS` |
| Hair materials | `Daz3D/Hair` / `uDTU URP.Hair` |
| DazStudioDefault (Skin) | `Daz3D/IrayUberSkin` |
| DazStudioDefault (Plastic) | Standard shader |
| DazStudioDefault (Metallic) | `Daz3D/IrayUberMetal` |
| Unknown / fallback | Standard shader |

### 8.3 Texture Maps

The following texture maps are transferred from Daz Studio:

- Base Color / Diffuse
- Normal Map
- Roughness / Glossiness
- Metallic
- Ambient Occlusion
- Emission
- Opacity / Alpha Cutoff
- Subsurface Scattering color and intensity (skin)
- Displacement (on shaders that support it)
- Makeup layers (PBRSkin shader)

### 8.4 HDRP — Diffusion Profile

When using HDRP, the skin shaders require a **Daz Diffusion Profile** to be registered in your HDRP settings. A prompt will appear after the first import. Follow the on-screen instructions to add the profile to your **HDRP Global Settings**.

---

## 9. Post-Import Tools

All tools are accessible via the **Daz3D** menu in the Unity Editor, or from the **Commands** tab in the Daz3D Bridge window. They can be run on any previously imported character.

| Tool | Menu Path | Description |
|------|-----------|-------------|
| Generate LOD Group | `Daz3D → Generate LOD Group` | Add LOD levels to selected character. |
| Setup Ragdoll | `Daz3D → Setup Ragdoll` | Add ragdoll physics to selected character. |
| Generate Morph Clips | `Daz3D → Generate Morph Clips` | Create AnimationClips from blend shapes. |
| Re-import Materials | `Daz3D → Extract materials from selected DTU` | Reprocess materials from the original DTU file. |

> Select the character's root **GameObject** in the Hierarchy before running any of these tools.

---

## 10. The Daz3D Bridge Window

Open via **Window → Daz3D Bridge** or **Daz3D → Open DazToUnity Bridge window**.

### Tabs

| Tab | Description |
|-----|-------------|
| **ReadMe** | Links to FAQ, manual, forums, and bug reporting. |
| **History** | Log of all past imports with timestamps. Click an entry to select that asset. |
| **Options** | Configure import behavior (see below). |
| **Commands** | Shortcuts to post-import tools. |

### Options

| Option | Default | Description |
|--------|---------|-------------|
| Auto Import DTU Changes | On | Automatically start import when a new DTU file is detected. |
| Generate Unity Prefab | On | Create a `.prefab` file after import. |
| Replace Materials | On | Apply converted Daz materials to the imported mesh. |
| Replace Scene Instances | On | Update any existing scene instances with the new import. |
| Automatically setup the Mecanim Avatar | On | Configure the FBX as a Humanoid rig for animation retargeting. |

---

## 11. Troubleshooting

| Problem | Likely Cause | Solution |
|---------|-------------|----------|
| Import dialog does not appear in Unity | Unity package not installed | Go to `Assets/Daz3D/Support/` and import the `.unitypackage` for your pipeline. |
| Shaders compile for a long time on first import | Normal on first use | Wait for compilation to finish. Subsequent imports are faster. |
| Materials appear pink or white | Render pipeline mismatch | Verify the correct package is installed for your pipeline (HDRP/URP/Built-In). |
| Ragdoll bones are missing | Character does not use Genesis naming | Ragdoll setup requires standard Genesis 3/8 bone names (hip, spine, chest, etc.). |
| LOD generation fails | UnityMeshSimplifier not installed | Install via Package Manager (see Section 6.1). |
| Hair does not simulate | dForce not enabled in Daz or checkbox not ticked | Re-export with **Auto Hair Physics** checked, and verify dForce is active on hair in Daz Studio. |
| Morphs missing in Unity | Morphs not exported from Daz | In the export dialog, make sure the **Morphs** option is enabled before clicking Accept. |
| Animation only works on original character | Character imported as Generic rig | Enable **Automatically setup the Mecanim Avatar** in Bridge Options and re-import. |
| Report a bug | — | Visit [github.com/Knuffelpanda/DazToUnity/issues](https://github.com/Knuffelpanda/DazToUnity/issues) |

---

*For community support, visit the [Daz Forums — Unity Discussion](https://www.daz3d.com/forums/categories/unity-discussion).*
