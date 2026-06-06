using UnityEngine;

namespace HairSalonSim.Core
{
    public class HairCutSystem : MonoBehaviour
    {
        [SerializeField] private int strandParticleCount = 32;

        private int[] cutIndices;
        private ComputeBuffer cutBuffer;
        private int strandCount;
        private bool initialized;

        public int StrandCount => strandCount;
        public int StrandParticleCount => strandParticleCount;
        public bool Initialized => initialized;

        public void Initialize(int strandCount, int particlesPerStrand)
        {
            this.strandCount = strandCount;
            this.strandParticleCount = particlesPerStrand;

            cutIndices = new int[strandCount];
            ResetAllCuts();

            cutBuffer = new ComputeBuffer(strandCount, sizeof(int));
            PushToGPU();

            initialized = true;
            Debug.Log($"[HairCutSystem] Initialized: {strandCount} strands, {particlesPerStrand} particles/strand");
        }

        public void CutStrand(int strandIndex, int particleIndex)
        {
            if (!initialized || strandIndex < 0 || strandIndex >= strandCount) return;

            particleIndex = Mathf.Clamp(particleIndex, 1, strandParticleCount);

            // Keep the shorter cut (closer to root)
            if (particleIndex < cutIndices[strandIndex])
            {
                cutIndices[strandIndex] = particleIndex;
                PushToGPU();
            }
        }

        public void CutStrands(int[] strandIndicesArr, int[] particleIndicesArr)
        {
            if (!initialized) return;

            bool changed = false;
            for (int i = 0; i < strandIndicesArr.Length; i++)
            {
                int si = strandIndicesArr[i];
                int pi = Mathf.Clamp(particleIndicesArr[i], 1, strandParticleCount);

                if (si < 0 || si >= strandCount) continue;

                if (pi < cutIndices[si])
                {
                    cutIndices[si] = pi;
                    changed = true;
                }
            }

            if (changed)
            {
                PushToGPU();
            }
        }

        public int GetCutIndex(int strandIndex)
        {
            if (!initialized || strandIndex < 0 || strandIndex >= strandCount)
                return strandParticleCount;
            return cutIndices[strandIndex];
        }

        public bool IsStrandCut(int strandIndex)
        {
            return GetCutIndex(strandIndex) < strandParticleCount;
        }

        public float GetCutRatio(int strandIndex)
        {
            return (float)GetCutIndex(strandIndex) / strandParticleCount;
        }

        public void ResetAllCuts()
        {
            if (cutIndices == null) return;

            for (int i = 0; i < cutIndices.Length; i++)
            {
                cutIndices[i] = strandParticleCount;
            }

            if (cutBuffer != null)
            {
                PushToGPU();
            }
        }

        public void BindToMaterial(Material material)
        {
            if (cutBuffer == null || material == null) return;
            material.SetBuffer("_CutIndices", cutBuffer);
            material.SetInt("_StrandParticleCount", strandParticleCount);
            material.SetInt("_StrandCount", strandCount);
        }

        public ComputeBuffer GetCutBuffer()
        {
            return cutBuffer;
        }

        private void PushToGPU()
        {
            if (cutBuffer != null && cutIndices != null)
            {
                cutBuffer.SetData(cutIndices);
            }
        }

        private void OnDestroy()
        {
            if (cutBuffer != null)
            {
                cutBuffer.Release();
                cutBuffer = null;
            }
            initialized = false;
        }
    }
}
