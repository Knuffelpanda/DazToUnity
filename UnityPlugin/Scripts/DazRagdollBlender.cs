using System.Collections;
using UnityEngine;

namespace Daz3D
{
    /// <summary>
    /// Attach to a Daz character root to blend smoothly between
    /// Animator-driven pose and full ragdoll physics.
    ///
    /// Usage:
    ///   GetComponent&lt;DazRagdollBlender&gt;().BlendToRagdoll();
    ///   GetComponent&lt;DazRagdollBlender&gt;().BlendToAnimation();
    ///
    /// If the Animator Controller has a Trigger parameter named "GetUp",
    /// it is automatically set when BlendToAnimation() finishes.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Daz3D/Ragdoll Blender")]
    public class DazRagdollBlender : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector
        // ------------------------------------------------------------------ //

        [Header("Blend Settings")]
        [Tooltip("Default blend duration in seconds when no duration is passed explicitly.")]
        public float blendDuration = 0.3f;

        [Header("State (read-only at runtime)")]
        [Range(0f, 1f)]
        [Tooltip("0 = fully animated, 1 = fully ragdoll. Shown for debugging.")]
        public float blendWeight = 0f;

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>True while in ragdoll state or blending toward ragdoll.</summary>
        public bool IsRagdoll => m_State == State.Ragdoll || m_State == State.BlendingToRagdoll;

        /// <summary>Smoothly switch from animation to ragdoll physics.</summary>
        public void BlendToRagdoll(float duration = -1f)
        {
            if (duration < 0f) duration = blendDuration;
            StopBlend();
            m_BlendRoutine = StartCoroutine(BlendRoutine(toRagdoll: true, duration));
        }

        /// <summary>Smoothly switch from ragdoll back to animation.</summary>
        public void BlendToAnimation(float duration = -1f)
        {
            if (duration < 0f) duration = blendDuration;
            StopBlend();
            m_BlendRoutine = StartCoroutine(BlendRoutine(toRagdoll: false, duration));
        }

        /// <summary>Instant switch with no blending.</summary>
        public void SetRagdollInstant(bool ragdoll)
        {
            StopBlend();
            blendWeight = ragdoll ? 1f : 0f;
            ApplyPhysics(ragdoll);
            m_State = ragdoll ? State.Ragdoll : State.Animated;
        }

        // ------------------------------------------------------------------ //
        //  Internal types
        // ------------------------------------------------------------------ //

        private enum State { Animated, BlendingToRagdoll, Ragdoll, BlendingToAnimated }

        private struct BoneData
        {
            public Transform  t;
            public Rigidbody  rb;
            public Vector3    snapPos;   // world position at blend start
            public Quaternion snapRot;   // world rotation at blend start
        }

        // ------------------------------------------------------------------ //
        //  Private state
        // ------------------------------------------------------------------ //

        private BoneData[] m_Bones;
        private Animator   m_Animator;
        private State      m_State = State.Animated;
        private Coroutine  m_BlendRoutine;

        private static readonly int k_GetUpHash = Animator.StringToHash("GetUp");

        // ------------------------------------------------------------------ //
        //  Unity messages
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            m_Animator = GetComponent<Animator>();
            CollectBones();
            ApplyPhysics(false);   // start fully animated
        }

        /// <summary>
        /// During a blend, override each bone's world transform with a lerp
        /// between the snapshot (start of blend) and the current physics/animator output.
        /// Called after physics and after the Animator has written its output.
        /// </summary>
        private void LateUpdate()
        {
            if (m_State == State.Animated || m_State == State.Ragdoll || m_Bones == null)
                return;

            for (int i = 0; i < m_Bones.Length; i++)
            {
                // 'current' is whatever physics or the Animator placed the bone at this frame
                Vector3    current    = m_Bones[i].t.position;
                Quaternion currentRot = m_Bones[i].t.rotation;

                m_Bones[i].t.position = Vector3.Lerp(m_Bones[i].snapPos, current, blendWeight);
                m_Bones[i].t.rotation = Quaternion.Slerp(m_Bones[i].snapRot, currentRot, blendWeight);
            }
        }

        // ------------------------------------------------------------------ //
        //  Blend coroutine
        // ------------------------------------------------------------------ //

        private IEnumerator BlendRoutine(bool toRagdoll, float duration)
        {
            SnapshotPose();

            if (toRagdoll)
            {
                // Snapshot holds the animated pose. Now enable physics.
                ApplyPhysics(true);
                m_State = State.BlendingToRagdoll;
            }
            else
            {
                // Snapshot holds the ragdoll pose. Now re-enable Animator.
                ApplyPhysics(false);
                m_State = State.BlendingToAnimated;
            }

            float startWeight = blendWeight;
            float target      = toRagdoll ? 1f : 0f;
            float elapsed     = 0f;

            while (elapsed < duration)
            {
                elapsed    += Time.deltaTime;
                blendWeight = Mathf.Lerp(startWeight, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            blendWeight     = target;
            m_State         = toRagdoll ? State.Ragdoll : State.Animated;
            m_BlendRoutine  = null;

            if (!toRagdoll)
                TryTriggerGetUp();
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private void CollectBones()
        {
            Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
            m_Bones = new BoneData[rbs.Length];
            for (int i = 0; i < rbs.Length; i++)
            {
                m_Bones[i].t  = rbs[i].transform;
                m_Bones[i].rb = rbs[i];
            }
        }

        private void ApplyPhysics(bool ragdoll)
        {
            if (m_Bones == null) return;
            foreach (BoneData b in m_Bones)
                b.rb.isKinematic = !ragdoll;
            if (m_Animator != null)
                m_Animator.enabled = !ragdoll;
        }

        private void SnapshotPose()
        {
            if (m_Bones == null) return;
            for (int i = 0; i < m_Bones.Length; i++)
            {
                m_Bones[i].snapPos = m_Bones[i].t.position;
                m_Bones[i].snapRot = m_Bones[i].t.rotation;
            }
        }

        private void StopBlend()
        {
            if (m_BlendRoutine != null)
            {
                StopCoroutine(m_BlendRoutine);
                m_BlendRoutine = null;
            }
        }

        private void TryTriggerGetUp()
        {
            if (m_Animator == null || m_Animator.runtimeAnimatorController == null)
                return;

            foreach (AnimatorControllerParameter p in m_Animator.parameters)
            {
                if (p.nameHash == k_GetUpHash && p.type == AnimatorControllerParameterType.Trigger)
                {
                    m_Animator.SetTrigger(k_GetUpHash);
                    return;
                }
            }
        }
    }
}
