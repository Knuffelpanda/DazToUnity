using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Daz3D
{
    /// <summary>
    /// Editor tool that automatically generates an LOD Group on a Daz character by:
    ///   1. Checking that UnityMeshSimplifier is installed (via reflection — no compile-time dep)
    ///   2. Collecting all SkinnedMeshRenderers with ≥ 100 vertices
    ///   3. Building LOD1/2/3 child GameObjects with simplified copies of each mesh
    ///   4. Adding a LODGroup to the root and configuring screen-height thresholds
    ///
    /// Access via: Daz3D → Generate LOD Group
    /// </summary>
    public static class DazLODGenerator
    {
        // ------------------------------------------------------------------ //
        //  LOD level definitions
        // ------------------------------------------------------------------ //

        private struct LODLevel
        {
            public float Quality;    // 1.0 = original, 0.5 = 50 %, etc.
            public float Threshold;  // screen-relative-height at which this LOD activates
        }

        private static readonly LODLevel[] k_Levels = new[]
        {
            new LODLevel { Quality = 1.00f, Threshold = 0.60f },  // LOD0 — original
            new LODLevel { Quality = 0.50f, Threshold = 0.30f },  // LOD1 — 50 %
            new LODLevel { Quality = 0.25f, Threshold = 0.10f },  // LOD2 — 25 %
            new LODLevel { Quality = 0.10f, Threshold = 0.02f },  // LOD3 — 10 %
        };

        // ------------------------------------------------------------------ //
        //  Menu entry
        // ------------------------------------------------------------------ //

        [MenuItem("Daz3D/Generate LOD Group", false, 201)]
        public static void GenerateLODGroup()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Generate LOD Group",
                    "Please select a Daz character GameObject first.", "OK");
                return;
            }

            // --- Step 1: dependency check ---
            Type simplifierType = Type.GetType("UnityMeshSimplifier.MeshSimplifier, UnityMeshSimplifier");
            if (simplifierType == null)
            {
                EditorUtility.DisplayDialog("Generate LOD Group",
                    "UnityMeshSimplifier is required.\n\n" +
                    "Install via:\n" +
                    "Window > Package Manager > + > Add package from git URL:\n" +
                    "https://github.com/Whinarn/UnityMeshSimplifier.git",
                    "OK");
                return;
            }

            MethodInfo mInit     = simplifierType.GetMethod("Initialize",   new[] { typeof(Mesh) });
            MethodInfo mSimplify = simplifierType.GetMethod("SimplifyMesh", new[] { typeof(float) });
            MethodInfo mToMesh   = simplifierType.GetMethod("ToMesh",       Type.EmptyTypes);

            if (mInit == null || mSimplify == null || mToMesh == null)
            {
                EditorUtility.DisplayDialog("Generate LOD Group",
                    "UnityMeshSimplifier API mismatch.\n\n" +
                    "Expected methods: Initialize(Mesh), SimplifyMesh(float), ToMesh().\n" +
                    "Please check your installed version.", "OK");
                return;
            }

            // --- Step 2: collect SkinnedMeshRenderers ---
            SkinnedMeshRenderer[] all = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var renderers = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in all)
            {
                if (smr.sharedMesh != null && smr.sharedMesh.vertexCount >= 100)
                    renderers.Add(smr);
            }
            renderers.Sort((a, b) => b.sharedMesh.vertexCount.CompareTo(a.sharedMesh.vertexCount));

            if (renderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Generate LOD Group",
                    "No suitable SkinnedMeshRenderers found on the selected object.\n" +
                    "(All meshes have fewer than 100 vertices.)", "OK");
                return;
            }

            // Guard: already has a LODGroup
            if (root.GetComponent<LODGroup>() != null)
            {
                if (!EditorUtility.DisplayDialog("Generate LOD Group",
                    "This GameObject already has a LODGroup.\n\nReplace it?", "Replace", "Cancel"))
                    return;
            }

            Undo.SetCurrentGroupName("Generate Daz LOD Group");
            int undoGroup = Undo.GetCurrentGroup();

            // Total steps for the progress bar (LOD levels 1–3, one step per renderer)
            int totalSteps  = (k_Levels.Length - 1) * renderers.Count;
            int currentStep = 0;
            var warnings    = new List<string>();

            try
            {
                // --- Prepare asset folder ---
                string safeName = root.name.Replace(" ", "_");
                if (!AssetDatabase.IsValidFolder("Assets/LODMeshes"))
                    AssetDatabase.CreateFolder("Assets", "LODMeshes");
                string charFolder = "Assets/LODMeshes/" + safeName;
                if (!AssetDatabase.IsValidFolder(charFolder))
                    AssetDatabase.CreateFolder("Assets/LODMeshes", safeName);

                // --- Step 3: build LOD structure ---
                int lod0Verts = 0;
                foreach (SkinnedMeshRenderer smr in renderers)
                    lod0Verts += smr.sharedMesh.vertexCount;

                Renderer[][] lodRenderers = new Renderer[k_Levels.Length][];
                int[]        lodVerts     = new int[k_Levels.Length];

                lodRenderers[0] = renderers.ToArray();
                lodVerts[0]     = lod0Verts;

                for (int lvl = 1; lvl < k_Levels.Length; lvl++)
                {
                    float  quality = k_Levels[lvl].Quality;
                    string lodName = "LOD" + lvl;

                    GameObject lodParent = new GameObject(lodName);
                    Undo.RegisterCreatedObjectUndo(lodParent, "Create " + lodName);
                    lodParent.transform.SetParent(root.transform, false);

                    var levelRenderers = new List<Renderer>();
                    int levelVerts     = 0;

                    foreach (SkinnedMeshRenderer src in renderers)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Generating LOD Group",
                            $"{lodName}  \u2014  {src.sharedMesh.name}  ({quality * 100:0}\u00a0%)",
                            (float)currentStep / totalSteps);

                        // --- Simplify (with per-mesh error recovery) ---
                        Mesh simplified = null;
                        try
                        {
                            object simplifier = Activator.CreateInstance(simplifierType);
                            mInit.Invoke(simplifier,     new object[] { src.sharedMesh });
                            mSimplify.Invoke(simplifier, new object[] { quality });
                            simplified = (Mesh)mToMesh.Invoke(simplifier, null);

                            if (simplified == null || simplified.vertexCount == 0)
                                throw new InvalidOperationException("Simplified mesh has zero vertices.");
                        }
                        catch (Exception ex)
                        {
                            // Unwrap TargetInvocationException for a readable message
                            string msg = ex is TargetInvocationException tie && tie.InnerException != null
                                ? tie.InnerException.Message
                                : ex.Message;

                            string warning = $"{lodName} / {src.sharedMesh.name}: {msg} — original mesh used as fallback.";
                            warnings.Add(warning);
                            Debug.LogWarning("[DazLODGenerator] " + warning);

                            simplified = src.sharedMesh;  // fallback: keep full-res for this slot
                        }

                        simplified.name = src.sharedMesh.name + "_" + lodName;
                        levelVerts     += simplified.vertexCount;

                        // Avoid overwriting an existing asset
                        string desiredPath = charFolder + "/" + simplified.name + ".mesh";
                        string meshPath    = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
                        AssetDatabase.CreateAsset(simplified, meshPath);

                        // Child GO + SkinnedMeshRenderer
                        GameObject childGO = new GameObject(src.gameObject.name);
                        Undo.RegisterCreatedObjectUndo(childGO, "Create " + lodName + " mesh GO");
                        childGO.transform.SetParent(lodParent.transform, false);

                        SkinnedMeshRenderer dst = Undo.AddComponent<SkinnedMeshRenderer>(childGO);
                        dst.sharedMesh      = simplified;
                        dst.sharedMaterials = src.sharedMaterials;
                        dst.bones           = src.bones;
                        dst.rootBone        = src.rootBone;
                        dst.localBounds     = src.localBounds;

                        levelRenderers.Add(dst);
                        currentStep++;
                    }

                    lodRenderers[lvl] = levelRenderers.ToArray();
                    lodVerts[lvl]     = levelVerts;
                }

                // --- Step 4: configure LODGroup ---
                LODGroup lodGroup = root.GetComponent<LODGroup>();
                if (lodGroup != null)
                    Undo.DestroyObjectImmediate(lodGroup);
                lodGroup = Undo.AddComponent<LODGroup>(root);

                var lods = new LOD[k_Levels.Length];
                for (int i = 0; i < k_Levels.Length; i++)
                    lods[i] = new LOD(k_Levels[i].Threshold, lodRenderers[i]);

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);

                // --- Step 5: report ---
                string report =
                    "LOD Group created.\n\n" +
                    $"LOD0: {lodVerts[0]:N0} verts\n" +
                    $"LOD1: {lodVerts[1]:N0} verts\n" +
                    $"LOD2: {lodVerts[2]:N0} verts\n" +
                    $"LOD3: {lodVerts[3]:N0} verts\n\n" +
                    $"Meshes saved to:\n{charFolder}";

                if (warnings.Count > 0)
                    report += $"\n\nWarnings ({warnings.Count}):\n\u2022 " + string.Join("\n\u2022 ", warnings);

                EditorUtility.DisplayDialog("Generate LOD Group", report, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[DazLODGenerator] Unexpected error: " + ex);
                EditorUtility.DisplayDialog("Generate LOD Group",
                    "An unexpected error occurred. The operation was stopped.\n\n" +
                    "Use Edit > Undo to revert any partial changes.\n\n" +
                    "Details:\n" + ex.Message,
                    "OK");
            }
            finally
            {
                // Always clear the progress bar, even on exception
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Daz3D/Generate LOD Group", true)]
        private static bool ValidateGenerateLODGroup()
        {
            return Selection.activeGameObject != null;
        }
    }
}
