using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Daz3D
{
    /// <summary>
    /// Editor tool that converts all blend shapes found on a Daz character into
    /// individual Unity AnimationClips, plus a single "Reset All" clip.
    ///
    /// Each clip is 1 second long:
    ///   • frame 0  → blend shape weight = 0
    ///   • frame 1s → blend shape weight = 100
    ///
    /// Blend shapes that appear on multiple SkinnedMeshRenderers (e.g. body + eyelashes
    /// sharing the same morph name) are combined into one clip that drives all of them.
    ///
    /// Access via: Daz3D → Generate Morph Clips
    /// </summary>
    public static class DazMorphAnimationGenerator
    {
        // Duration of each generated clip (seconds)
        private const float k_ClipDuration = 1f;

        // ------------------------------------------------------------------ //
        //  Internal helpers
        // ------------------------------------------------------------------ //

        private struct MorphBinding
        {
            public string RelativePath;   // path from root to SMR
            public int    BlendShapeIndex;
        }

        // ------------------------------------------------------------------ //
        //  Menu entry
        // ------------------------------------------------------------------ //

        [MenuItem("Daz3D/Generate Morph Clips", false, 202)]
        public static void GenerateMorphClips()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Generate Morph Clips",
                    "Please select a Daz character GameObject first.", "OK");
                return;
            }

            // --- Collect SkinnedMeshRenderers with blend shapes ---
            SkinnedMeshRenderer[] allSmr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // morph name → list of (relativePath, blendShapeIndex) across all SMRs
            var morphMap = new Dictionary<string, List<MorphBinding>>(StringComparer.Ordinal);

            foreach (SkinnedMeshRenderer smr in allSmr)
            {
                Mesh mesh = smr.sharedMesh;
                if (mesh == null || mesh.blendShapeCount == 0) continue;

                string relativePath = AnimationUtility.CalculateTransformPath(
                    smr.transform, root.transform);

                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string shapeName = mesh.GetBlendShapeName(i);
                    if (!morphMap.ContainsKey(shapeName))
                        morphMap[shapeName] = new List<MorphBinding>();

                    morphMap[shapeName].Add(new MorphBinding
                    {
                        RelativePath    = relativePath,
                        BlendShapeIndex = i,
                    });
                }
            }

            if (morphMap.Count == 0)
            {
                EditorUtility.DisplayDialog("Generate Morph Clips",
                    "No blend shapes found on the selected character.", "OK");
                return;
            }

            // --- Prepare output folder ---
            string safeName = root.name.Replace(" ", "_");
            if (!AssetDatabase.IsValidFolder("Assets/MorphClips"))
                AssetDatabase.CreateFolder("Assets", "MorphClips");
            string charFolder = "Assets/MorphClips/" + safeName;
            if (!AssetDatabase.IsValidFolder(charFolder))
                AssetDatabase.CreateFolder("Assets/MorphClips", safeName);

            int created  = 0;
            int skipped  = 0;
            var errors   = new List<string>();

            EditorUtility.DisplayProgressBar("Generating Morph Clips", "Initialising…", 0f);

            try
            {
                int  total   = morphMap.Count + 1;  // +1 for the Reset clip
                int  current = 0;

                // --- One clip per morph name ---
                foreach (var kvp in morphMap)
                {
                    string morphName     = kvp.Key;
                    List<MorphBinding> bindings = kvp.Value;

                    EditorUtility.DisplayProgressBar(
                        "Generating Morph Clips",
                        morphName,
                        (float)current / total);

                    try
                    {
                        AnimationClip clip = BuildMorphClip(morphName, bindings,
                            startWeight: 0f, endWeight: 100f);

                        string path = AssetDatabase.GenerateUniqueAssetPath(
                            charFolder + "/" + SanitiseFileName(morphName) + ".anim");
                        AssetDatabase.CreateAsset(clip, path);
                        created++;
                    }
                    catch (Exception ex)
                    {
                        string err = $"{morphName}: {ex.Message}";
                        errors.Add(err);
                        Debug.LogWarning("[DazMorphAnimationGenerator] " + err);
                        skipped++;
                    }

                    current++;
                }

                // --- Reset All clip ---
                EditorUtility.DisplayProgressBar("Generating Morph Clips", "Reset All…", (float)current / total);
                try
                {
                    AnimationClip resetClip = BuildResetAllClip(morphMap);
                    string resetPath = AssetDatabase.GenerateUniqueAssetPath(
                        charFolder + "/_ResetAll.anim");
                    AssetDatabase.CreateAsset(resetClip, resetPath);
                    created++;
                }
                catch (Exception ex)
                {
                    errors.Add("_ResetAll: " + ex.Message);
                    Debug.LogWarning("[DazMorphAnimationGenerator] Reset clip failed: " + ex.Message);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // --- Report ---
                string report =
                    $"Done.\n\n" +
                    $"Clips created : {created}\n" +
                    $"Blend shapes  : {morphMap.Count}\n" +
                    $"Saved to      : {charFolder}";

                if (skipped > 0)
                    report += $"\n\nSkipped ({skipped}):\n\u2022 " + string.Join("\n\u2022 ", errors);

                EditorUtility.DisplayDialog("Generate Morph Clips", report, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[DazMorphAnimationGenerator] Unexpected error: " + ex);
                EditorUtility.DisplayDialog("Generate Morph Clips",
                    "An unexpected error occurred.\n\nDetails:\n" + ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Daz3D/Generate Morph Clips", true)]
        private static bool ValidateGenerateMorphClips()
        {
            return Selection.activeGameObject != null;
        }

        // ------------------------------------------------------------------ //
        //  Clip builders
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates a clip that drives a single morph from startWeight to endWeight over k_ClipDuration.
        /// When the morph appears on multiple SMRs all of them are included.
        /// </summary>
        private static AnimationClip BuildMorphClip(
            string morphName, List<MorphBinding> bindings,
            float startWeight, float endWeight)
        {
            var clip = new AnimationClip
            {
                name      = morphName,
                frameRate = 60f,
                wrapMode  = WrapMode.Once,
            };

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            foreach (MorphBinding b in bindings)
            {
                string property = "blendShape." + morphName;

                AnimationCurve curve = AnimationCurve.Linear(
                    0f,            startWeight,
                    k_ClipDuration, endWeight);

                clip.SetCurve(b.RelativePath, typeof(SkinnedMeshRenderer), property, curve);
            }

            return clip;
        }

        /// <summary>
        /// Creates a single clip that resets every morph in morphMap to 0 immediately (one keyframe at t=0).
        /// </summary>
        private static AnimationClip BuildResetAllClip(
            Dictionary<string, List<MorphBinding>> morphMap)
        {
            var clip = new AnimationClip
            {
                name      = "_ResetAll",
                frameRate = 60f,
                wrapMode  = WrapMode.Once,
            };

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            foreach (var kvp in morphMap)
            {
                string morphName = kvp.Key;
                foreach (MorphBinding b in kvp.Value)
                {
                    string property = "blendShape." + morphName;
                    // Single keyframe: weight = 0 at t=0
                    AnimationCurve curve = AnimationCurve.Constant(0f, 0f, 0f);
                    clip.SetCurve(b.RelativePath, typeof(SkinnedMeshRenderer), property, curve);
                }
            }

            return clip;
        }

        // ------------------------------------------------------------------ //
        //  Utilities
        // ------------------------------------------------------------------ //

        /// <summary>Strips characters that are illegal in Windows filenames.</summary>
        private static string SanitiseFileName(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
