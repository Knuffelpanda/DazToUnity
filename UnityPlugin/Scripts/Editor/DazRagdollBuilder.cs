using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Daz3D
{
    /// <summary>
    /// Editor tool that automatically sets up a Unity ragdoll on a Daz character
    /// by scanning the hierarchy for known Daz bone names, then:
    ///   1. Adds CapsuleCollider + Rigidbody to each bone
    ///   2. Adds CharacterJoint (with anatomical limits) to non-root bones
    ///   3. Adds DazRagdollBlender to the root GameObject
    ///
    /// Access via: Daz3D → Setup Ragdoll
    /// </summary>
    public static class DazRagdollBuilder
    {
        // ------------------------------------------------------------------ //
        //  Bone descriptor
        // ------------------------------------------------------------------ //

        private class BoneConfig
        {
            public string   Label;          // human-readable name for diagnostics
            public string[] SearchNames;    // Daz bone names to search (case-insensitive)
            public string   ParentLabel;    // label of the parent BoneConfig (null = root)
            public float    Mass;
            public float    RadiusFactor;   // collider radius as fraction of bone length

            // CharacterJoint limits (degrees)
            public float TwistLow, TwistHigh;
            public float Swing1, Swing2;
        }

        // ------------------------------------------------------------------ //
        //  Bone table — Daz Genesis 8 / Genesis 9 naming conventions
        // ------------------------------------------------------------------ //

        private static readonly BoneConfig[] k_Bones = new[]
        {
            new BoneConfig { Label = "Hips",         SearchNames = new[]{"hip","pelvis"},                          ParentLabel = null,         Mass = 15f, RadiusFactor = 0.35f, TwistLow = -20, TwistHigh = 20, Swing1 = 30, Swing2 = 20 },
            new BoneConfig { Label = "Spine",         SearchNames = new[]{"abdomen","abdomen2","spine"},            ParentLabel = "Hips",       Mass =  8f, RadiusFactor = 0.30f, TwistLow = -20, TwistHigh = 20, Swing1 = 30, Swing2 = 20 },
            new BoneConfig { Label = "Chest",         SearchNames = new[]{"chest","chest_upper"},                   ParentLabel = "Spine",      Mass = 10f, RadiusFactor = 0.30f, TwistLow = -20, TwistHigh = 20, Swing1 = 30, Swing2 = 20 },
            new BoneConfig { Label = "Head",          SearchNames = new[]{"head"},                                  ParentLabel = "Chest",      Mass =  5f, RadiusFactor = 0.50f, TwistLow = -45, TwistHigh = 45, Swing1 = 40, Swing2 = 30 },
            new BoneConfig { Label = "LeftUpperArm",  SearchNames = new[]{"lShldrBend","lShldr","lCollar"},        ParentLabel = "Chest",      Mass =  3f, RadiusFactor = 0.25f, TwistLow = -70, TwistHigh = 70, Swing1 = 80, Swing2 = 60 },
            new BoneConfig { Label = "LeftLowerArm",  SearchNames = new[]{"lForearmBend","lForeArm","lforearm"},   ParentLabel = "LeftUpperArm",  Mass = 2f, RadiusFactor = 0.20f, TwistLow = -10, TwistHigh = 10, Swing1 = 90, Swing2 =  0 },
            new BoneConfig { Label = "LeftHand",      SearchNames = new[]{"lHand"},                                ParentLabel = "LeftLowerArm",  Mass = 1f, RadiusFactor = 0.40f, TwistLow = -10, TwistHigh = 10, Swing1 = 45, Swing2 = 30 },
            new BoneConfig { Label = "RightUpperArm", SearchNames = new[]{"rShldrBend","rShldr","rCollar"},        ParentLabel = "Chest",      Mass =  3f, RadiusFactor = 0.25f, TwistLow = -70, TwistHigh = 70, Swing1 = 80, Swing2 = 60 },
            new BoneConfig { Label = "RightLowerArm", SearchNames = new[]{"rForearmBend","rForeArm","rforearm"},   ParentLabel = "RightUpperArm", Mass = 2f, RadiusFactor = 0.20f, TwistLow = -10, TwistHigh = 10, Swing1 = 90, Swing2 =  0 },
            new BoneConfig { Label = "RightHand",     SearchNames = new[]{"rHand"},                                ParentLabel = "RightLowerArm", Mass = 1f, RadiusFactor = 0.40f, TwistLow = -10, TwistHigh = 10, Swing1 = 45, Swing2 = 30 },
            new BoneConfig { Label = "LeftUpperLeg",  SearchNames = new[]{"lThighBend","lThigh","lUpLeg"},         ParentLabel = "Hips",       Mass =  7f, RadiusFactor = 0.25f, TwistLow = -60, TwistHigh = 60, Swing1 = 70, Swing2 = 40 },
            new BoneConfig { Label = "LeftLowerLeg",  SearchNames = new[]{"lShin","lLeg"},                         ParentLabel = "LeftUpperLeg",  Mass = 5f, RadiusFactor = 0.20f, TwistLow = -10, TwistHigh = 10, Swing1 = 90, Swing2 =  0 },
            new BoneConfig { Label = "LeftFoot",      SearchNames = new[]{"lFoot"},                                ParentLabel = "LeftLowerLeg",  Mass = 2f, RadiusFactor = 0.35f, TwistLow = -10, TwistHigh = 10, Swing1 = 40, Swing2 = 20 },
            new BoneConfig { Label = "RightUpperLeg", SearchNames = new[]{"rThighBend","rThigh","rUpLeg"},         ParentLabel = "Hips",       Mass =  7f, RadiusFactor = 0.25f, TwistLow = -60, TwistHigh = 60, Swing1 = 70, Swing2 = 40 },
            new BoneConfig { Label = "RightLowerLeg", SearchNames = new[]{"rShin","rLeg"},                         ParentLabel = "RightUpperLeg", Mass = 5f, RadiusFactor = 0.20f, TwistLow = -10, TwistHigh = 10, Swing1 = 90, Swing2 =  0 },
            new BoneConfig { Label = "RightFoot",     SearchNames = new[]{"rFoot"},                                ParentLabel = "RightLowerLeg", Mass = 2f, RadiusFactor = 0.35f, TwistLow = -10, TwistHigh = 10, Swing1 = 40, Swing2 = 20 },
        };

        // ------------------------------------------------------------------ //
        //  Menu entry
        // ------------------------------------------------------------------ //

        [MenuItem("Daz3D/Setup Ragdoll", false, 200)]
        public static void SetupRagdoll()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("Setup Ragdoll", "Please select a Daz character GameObject first.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Setup Daz Ragdoll");
            int undoGroup = Undo.GetCurrentGroup();

            // --- Phase 1: find bones ---
            var found   = new Dictionary<string, Transform>();  // label → Transform
            var missing = new List<string>();

            foreach (BoneConfig cfg in k_Bones)
            {
                Transform t = FindBone(root.transform, cfg.SearchNames);
                if (t != null)
                    found[cfg.Label] = t;
                else
                {
                    missing.Add(cfg.Label);
                    Debug.LogWarning($"[DazRagdollBuilder] Bone not found for '{cfg.Label}' (searched: {string.Join(", ", cfg.SearchNames)})");
                }
            }

            if (found.Count == 0)
            {
                EditorUtility.DisplayDialog("Setup Ragdoll",
                    "No Daz bones were found in the hierarchy.\n\nMake sure you select the root of a Daz character (the GameObject with the Animator).",
                    "OK");
                return;
            }

            // --- Phase 2: add Rigidbody + CapsuleCollider + CharacterJoint ---
            var rigidbodies = new Dictionary<string, Rigidbody>();

            foreach (BoneConfig cfg in k_Bones)
            {
                if (!found.ContainsKey(cfg.Label)) continue;

                Transform bone = found[cfg.Label];

                // Remove existing components so we can start fresh
                RemoveExisting<Rigidbody>(bone);
                RemoveExisting<CapsuleCollider>(bone);
                RemoveExisting<CharacterJoint>(bone);

                // Collider: size from distance to first child bone
                Transform childBone = found.ContainsKey(GetChildLabel(cfg.Label)) ? found[GetChildLabel(cfg.Label)] : FirstChildOrNull(bone);
                AddCapsuleCollider(bone, childBone, cfg.RadiusFactor);

                // Rigidbody
                Rigidbody rb = Undo.AddComponent<Rigidbody>(bone.gameObject);
                rb.mass           = cfg.Mass;
                rb.drag           = 0.05f;
                rb.angularDrag    = 0.05f;
                rb.interpolation  = RigidbodyInterpolation.Interpolate;
                rb.isKinematic    = true;   // starts animated
                rigidbodies[cfg.Label] = rb;

                // CharacterJoint (skip root / Hips)
                if (cfg.ParentLabel != null && rigidbodies.ContainsKey(cfg.ParentLabel))
                {
                    CharacterJoint joint = Undo.AddComponent<CharacterJoint>(bone.gameObject);
                    joint.connectedBody = rigidbodies[cfg.ParentLabel];
                    joint.enablePreprocessing = false;

                    // Twist limits
                    joint.lowTwistLimit  = new SoftJointLimit { limit =  cfg.TwistLow };
                    joint.highTwistLimit = new SoftJointLimit { limit =  cfg.TwistHigh };

                    // Swing limits
                    joint.swing1Limit = new SoftJointLimit { limit = cfg.Swing1 };
                    joint.swing2Limit = new SoftJointLimit { limit = cfg.Swing2 };
                }
            }

            // --- Phase 3: add DazRagdollBlender to root ---
            DazRagdollBlender existing = root.GetComponent<DazRagdollBlender>();
            if (existing == null)
                Undo.AddComponent<DazRagdollBlender>(root);

            Undo.CollapseUndoOperations(undoGroup);

            // --- Report ---
            string report = $"Ragdoll setup complete.\n\nBones configured: {found.Count} / {k_Bones.Length}";
            if (missing.Count > 0)
                report += $"\n\nMissing (skipped):\n• " + string.Join("\n• ", missing);

            EditorUtility.DisplayDialog("Setup Ragdoll", report, "OK");
        }

        [MenuItem("Daz3D/Setup Ragdoll", true)]
        private static bool ValidateSetupRagdoll()
        {
            return Selection.activeGameObject != null;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        /// <summary>Returns the expected child label for parent-child collider sizing.</summary>
        private static string GetChildLabel(string parentLabel)
        {
            switch (parentLabel)
            {
                case "Hips":         return "Spine";
                case "Spine":        return "Chest";
                case "Chest":        return "Head";
                case "LeftUpperArm": return "LeftLowerArm";
                case "LeftLowerArm": return "LeftHand";
                case "RightUpperArm":return "RightLowerArm";
                case "RightLowerArm":return "RightHand";
                case "LeftUpperLeg": return "LeftLowerLeg";
                case "LeftLowerLeg": return "LeftFoot";
                case "RightUpperLeg":return "RightLowerLeg";
                case "RightLowerLeg":return "RightFoot";
                default:             return null;
            }
        }

        private static void AddCapsuleCollider(Transform bone, Transform childBone, float radiusFactor)
        {
            CapsuleCollider col = Undo.AddComponent<CapsuleCollider>(bone.gameObject);

            if (childBone != null)
            {
                Vector3 localDir = bone.InverseTransformPoint(childBone.position);
                float   height   = localDir.magnitude;
                float   radius   = Mathf.Min(height * radiusFactor, height * 0.48f);

                // Dominant axis
                float ax = Mathf.Abs(localDir.x);
                float ay = Mathf.Abs(localDir.y);
                float az = Mathf.Abs(localDir.z);

                if (ay >= ax && ay >= az)       col.direction = 1;  // Y
                else if (ax >= ay && ax >= az)  col.direction = 0;  // X
                else                             col.direction = 2;  // Z

                col.height = height;
                col.radius = radius;
                col.center = bone.InverseTransformPoint(
                    Vector3.Lerp(bone.position, childBone.position, 0.5f));
            }
            else
            {
                // Leaf bone fallback
                col.direction = 1;
                col.height    = 0.1f;
                col.radius    = 0.05f;
            }
        }

        private static void RemoveExisting<T>(Transform bone) where T : Component
        {
            T comp = bone.GetComponent<T>();
            if (comp != null)
            {
                Undo.DestroyObjectImmediate(comp);
            }
        }

        private static Transform FirstChildOrNull(Transform t)
        {
            return t.childCount > 0 ? t.GetChild(0) : null;
        }

        /// <summary>Recursive case-insensitive bone search.</summary>
        private static Transform FindBone(Transform root, string[] names)
        {
            foreach (string name in names)
            {
                Transform t = FindByName(root, name);
                if (t != null) return t;
            }
            return null;
        }

        private static Transform FindByName(Transform parent, string name)
        {
            if (string.Equals(parent.name, name, System.StringComparison.OrdinalIgnoreCase))
                return parent;
            foreach (Transform child in parent)
            {
                Transform result = FindByName(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
