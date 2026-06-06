using UnityEngine;

namespace HairSalonSim.Core
{
    public class HairSetup : MonoBehaviour
    {
        [Header("Hair Configuration")]
        [SerializeField] private int strandCount = 5000;
        [SerializeField] private int particlesPerStrand = 32;
        [SerializeField] private float hairLength = 0.25f; // 25cm
        [SerializeField] private float hairWidth = 0.0005f; // 0.5mm

        [Header("Physics")]
        [SerializeField] private float stiffness = 0.8f;
        [SerializeField] private float damping = 0.5f;
        [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);

        [Header("References")]
        [SerializeField] private HairCutSystem cutSystem;
        [SerializeField] private HairCutDetector cutDetector;
        [SerializeField] private Material hairMaterial;
        [SerializeField] private Transform headTransform;

        // Simulated particle positions for development/testing
        // In production, these come from com.unity.demoteam.hair via AsyncGPUReadback
        private Vector3[] particlePositions;
        private bool initialized;

        public int StrandCount => strandCount;
        public int ParticlesPerStrand => particlesPerStrand;
        public Vector3[] ParticlePositions => particlePositions;
        public bool IsInitialized => initialized;

        private void Start()
        {
            InitializeHairSystem();
        }

        public void InitializeHairSystem()
        {
            // Initialize particle positions
            particlePositions = new Vector3[strandCount * particlesPerStrand];
            GenerateDefaultHairPositions();

            // Initialize cut system
            if (cutSystem != null)
            {
                cutSystem.Initialize(strandCount, particlesPerStrand);

                if (hairMaterial != null)
                {
                    cutSystem.BindToMaterial(hairMaterial);
                }
            }

            // Initialize cut detector
            if (cutDetector != null)
            {
                cutDetector.Initialize(strandCount, particlesPerStrand);
                cutDetector.UpdateParticlePositions(particlePositions);
            }

            initialized = true;
            Debug.Log($"[HairSetup] Hair system initialized: {strandCount} strands, {particlesPerStrand} particles/strand");
        }

        private void GenerateDefaultHairPositions()
        {
            if (headTransform == null)
            {
                Debug.LogWarning("[HairSetup] No head transform assigned, using origin");
            }

            Vector3 headCenter = headTransform != null ? headTransform.position : Vector3.zero;
            float headRadius = 0.1f; // approximate head radius in meters

            for (int s = 0; s < strandCount; s++)
            {
                // Distribute roots on upper hemisphere of head
                float phi = Random.Range(0f, Mathf.PI * 2f);
                float theta = Random.Range(0f, Mathf.PI * 0.6f); // upper 60% of sphere

                Vector3 rootDir = new Vector3(
                    Mathf.Sin(theta) * Mathf.Cos(phi),
                    Mathf.Cos(theta),
                    Mathf.Sin(theta) * Mathf.Sin(phi)
                );

                Vector3 rootPos = headCenter + rootDir * headRadius;

                // Add slight random variation to growth direction
                Vector3 growDir = (rootDir + Vector3.down * 0.5f).normalized;
                growDir += new Vector3(
                    Random.Range(-0.1f, 0.1f),
                    Random.Range(-0.05f, 0f),
                    Random.Range(-0.1f, 0.1f)
                );
                growDir.Normalize();

                float segmentLength = hairLength / (particlesPerStrand - 1);

                for (int p = 0; p < particlesPerStrand; p++)
                {
                    int idx = s * particlesPerStrand + p;

                    // Simulate natural hair draping with gravity influence
                    float t = (float)p / (particlesPerStrand - 1);
                    Vector3 gravityInfluence = gravity.normalized * t * t * 0.3f;

                    particlePositions[idx] = rootPos + (growDir + gravityInfluence).normalized * (segmentLength * p);
                }
            }
        }

        public void UpdateParticlePositionsFromGPU(Vector3[] gpuPositions)
        {
            if (gpuPositions == null || gpuPositions.Length != particlePositions.Length) return;

            System.Array.Copy(gpuPositions, particlePositions, particlePositions.Length);

            if (cutDetector != null)
            {
                cutDetector.UpdateParticlePositions(particlePositions);
            }
        }

        public void ResetHair()
        {
            if (cutSystem != null)
            {
                cutSystem.ResetAllCuts();
            }

            GenerateDefaultHairPositions();

            if (cutDetector != null)
            {
                cutDetector.UpdateParticlePositions(particlePositions);
            }

            Debug.Log("[HairSetup] Hair reset to initial state");
        }
    }
}
