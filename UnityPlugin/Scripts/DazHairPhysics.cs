using System.Collections.Generic;
using UnityEngine;

namespace Daz3D
{
    /// <summary>
    /// Spring-based hair physics simulation for Daz hair assets.
    /// Automatically discovers bone chains from the SkinnedMeshRenderer on the same
    /// GameObject and applies Verlet integration with a distance constraint each frame.
    ///
    /// Added automatically by Daz3DDTUImporter when dForce hair is detected.
    /// Parameters are mapped from the dForce material properties in the .dtu file.
    /// </summary>
    [AddComponentMenu("Daz3D/Hair Physics")]
    public class DazHairPhysics : MonoBehaviour
    {
        [Header("Physics Parameters")]
        [Tooltip("Overall influence of physics simulation (0 = fully animated, 1 = full simulation).")]
        [Range(0f, 1f)] public float dynamicsStrength = 0.8f;

        [Tooltip("Spring stiffness — higher values keep hair closer to its rest pose.")]
        [Range(0f, 1f)] public float stiffness = 0.1f;

        [Tooltip("Velocity damping per frame — higher values stop motion faster.")]
        [Range(0f, 1f)] public float damping = 0.85f;

        [Tooltip("Gravity scale applied to simulated strands (uses Physics.gravity direction).")]
        public float gravity = 0.3f;

        // ------------------------------------------------------------------ //
        //  Internal types
        // ------------------------------------------------------------------ //

        private class Particle
        {
            public Transform  transform;
            public Vector3    position;       // current simulated world position
            public Vector3    prevPosition;   // previous frame simulated position (Verlet)
            public Vector3    localRestPos;   // local rest position relative to parent
            public float      boneLength;     // maintained constraint length from parent
            public bool       isRoot;         // root particles track the transform exactly
        }

        private List<Particle[]> m_Chains = new List<Particle[]>();
        private bool             m_Initialized;

        // ------------------------------------------------------------------ //
        //  Unity messages
        // ------------------------------------------------------------------ //

        private void Awake()   => Initialize();
        private void OnEnable() { if (!m_Initialized) Initialize(); }

        private void LateUpdate()
        {
            if (!m_Initialized || dynamicsStrength <= 0f) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 gravityStep = Physics.gravity * gravity * dt * dt;

            foreach (Particle[] chain in m_Chains)
            {
                // Root always tracks the bone transform
                Particle root = chain[0];
                root.prevPosition = root.position;
                root.position     = root.transform.position;

                for (int i = 1; i < chain.Length; i++)
                {
                    Particle p = chain[i];

                    // World-space rest position = parent transform * local rest pos
                    Vector3 worldRest = p.transform.parent.TransformPoint(p.localRestPos);

                    // Verlet integration with damping
                    Vector3 velocity = (p.position - p.prevPosition) * (1f - damping);
                    velocity += gravityStep;

                    // Spring force toward rest position
                    Vector3 spring = (worldRest - p.position) * stiffness;

                    p.prevPosition = p.position;
                    p.position    += velocity + spring;

                    // Distance constraint: maintain bone length from parent particle
                    if (p.boneLength > 0f)
                    {
                        Vector3 toParent = chain[i - 1].position - p.position;
                        float   dist     = toParent.magnitude;
                        if (dist > 0f)
                            p.position = chain[i - 1].position - toParent / dist * p.boneLength;
                    }

                    // Write back to transform, blended by dynamicsStrength
                    p.transform.position = Vector3.Lerp(
                        p.transform.position,
                        p.position,
                        dynamicsStrength
                    );
                }
            }
        }

        private void OnDisable() => ResetSimulation();

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Snap all particles to their current transform positions.
        /// Call this after teleporting the character to prevent spring overshooting.
        /// </summary>
        public void ResetSimulation()
        {
            foreach (Particle[] chain in m_Chains)
                foreach (Particle p in chain)
                {
                    p.position     = p.transform.position;
                    p.prevPosition = p.transform.position;
                }
        }

        // ------------------------------------------------------------------ //
        //  Initialization
        // ------------------------------------------------------------------ //

        private void Initialize()
        {
            m_Chains.Clear();
            m_Initialized = false;

            SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
            if (smr == null || smr.bones == null || smr.bones.Length == 0)
            {
                Debug.LogWarning("[DazHairPhysics] No SkinnedMeshRenderer or bones found on " + gameObject.name);
                return;
            }

            BuildChains(smr.bones);
            m_Initialized = m_Chains.Count > 0;

            if (!m_Initialized)
                Debug.LogWarning("[DazHairPhysics] Could not build any bone chains on " + gameObject.name);
        }

        private void BuildChains(Transform[] bones)
        {
            var boneSet = new HashSet<Transform>(bones);

            // Roots are bones whose parent is not in the bone set
            foreach (Transform bone in bones)
            {
                if (bone == null) continue;
                if (bone.parent == null || !boneSet.Contains(bone.parent))
                {
                    var chain = new List<Particle>();
                    BuildChainRecursive(bone, boneSet, chain);
                    if (chain.Count >= 2)
                        m_Chains.Add(chain.ToArray());
                }
            }
        }

        private void BuildChainRecursive(Transform bone, HashSet<Transform> boneSet, List<Particle> chain)
        {
            bool isRoot = chain.Count == 0;

            chain.Add(new Particle
            {
                transform    = bone,
                position     = bone.position,
                prevPosition = bone.position,
                localRestPos = bone.localPosition,
                boneLength   = isRoot ? 0f : bone.localPosition.magnitude,
                isRoot       = isRoot
            });

            // Follow the first child that belongs to the same bone set
            foreach (Transform child in bone)
            {
                if (boneSet.Contains(child))
                {
                    BuildChainRecursive(child, boneSet, chain);
                    break;
                }
            }
        }
    }
}
