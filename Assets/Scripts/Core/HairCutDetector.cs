using UnityEngine;

namespace HairSalonSim.Core
{
    public class HairCutDetector : MonoBehaviour
    {
        [SerializeField] private float cutRadius = 0.005f;

        private Vector3[] particlePositions;
        private int strandCount;
        private int particlesPerStrand;
        private SpatialHashGrid spatialHash;
        private bool initialized;

        public float CutRadius
        {
            get => cutRadius;
            set => cutRadius = value;
        }

        public struct CutResult
        {
            public bool hit;
            public int strandIndex;
            public int particleIndex;
            public Vector3 worldPosition;
            public float distance;
        }

        public void Initialize(int strandCount, int particlesPerStrand)
        {
            this.strandCount = strandCount;
            this.particlesPerStrand = particlesPerStrand;
            this.particlePositions = new Vector3[strandCount * particlesPerStrand];

            spatialHash = new SpatialHashGrid(cutRadius * 4f);
            initialized = true;
        }

        public void UpdateParticlePositions(Vector3[] positions)
        {
            if (!initialized) return;

            System.Array.Copy(positions, particlePositions, Mathf.Min(positions.Length, particlePositions.Length));
            RebuildSpatialHash();
        }

        private void RebuildSpatialHash()
        {
            spatialHash.Clear();

            for (int s = 0; s < strandCount; s++)
            {
                for (int p = 0; p < particlesPerStrand; p++)
                {
                    int idx = s * particlesPerStrand + p;
                    spatialHash.Insert(particlePositions[idx], s, p);
                }
            }
        }

        public CutResult DetectCut(Ray ray)
        {
            CutResult best = new CutResult { hit = false, distance = float.MaxValue };
            if (!initialized) return best;

            // Sample points along the ray within a reasonable range
            float rayLength = 2f;
            int raySamples = 20;

            for (int i = 0; i < raySamples; i++)
            {
                float t = (float)i / raySamples * rayLength;
                Vector3 samplePoint = ray.GetPoint(t);

                var nearby = spatialHash.Query(samplePoint, cutRadius);
                foreach (var entry in nearby)
                {
                    int idx = entry.strandIndex * particlesPerStrand + entry.particleIndex;
                    Vector3 particlePos = particlePositions[idx];

                    // Calculate distance from ray to particle
                    float dist = DistancePointToRay(particlePos, ray);

                    if (dist < cutRadius && dist < best.distance)
                    {
                        best.hit = true;
                        best.strandIndex = entry.strandIndex;
                        best.particleIndex = entry.particleIndex;
                        best.worldPosition = particlePos;
                        best.distance = dist;
                    }
                }
            }

            return best;
        }

        public CutResult[] DetectCutsInArea(Ray ray, float areaRadius)
        {
            if (!initialized) return System.Array.Empty<CutResult>();

            var results = new System.Collections.Generic.List<CutResult>();
            float effectiveRadius = Mathf.Max(cutRadius, areaRadius);

            float rayLength = 2f;
            int raySamples = 20;
            var processedStrands = new System.Collections.Generic.HashSet<int>();

            for (int i = 0; i < raySamples; i++)
            {
                float t = (float)i / raySamples * rayLength;
                Vector3 samplePoint = ray.GetPoint(t);

                var nearby = spatialHash.Query(samplePoint, effectiveRadius);
                foreach (var entry in nearby)
                {
                    if (processedStrands.Contains(entry.strandIndex)) continue;

                    int idx = entry.strandIndex * particlesPerStrand + entry.particleIndex;
                    Vector3 particlePos = particlePositions[idx];

                    float dist = DistancePointToRay(particlePos, ray);
                    if (dist < effectiveRadius)
                    {
                        results.Add(new CutResult
                        {
                            hit = true,
                            strandIndex = entry.strandIndex,
                            particleIndex = entry.particleIndex,
                            worldPosition = particlePos,
                            distance = dist
                        });
                        processedStrands.Add(entry.strandIndex);
                    }
                }
            }

            return results.ToArray();
        }

        private static float DistancePointToRay(Vector3 point, Ray ray)
        {
            Vector3 v = point - ray.origin;
            float t = Vector3.Dot(v, ray.direction);
            if (t < 0f) return v.magnitude;
            Vector3 closest = ray.origin + ray.direction * t;
            return Vector3.Distance(point, closest);
        }
    }
}
