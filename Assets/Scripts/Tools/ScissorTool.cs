using UnityEngine;
using HairSalonSim.Core;

namespace HairSalonSim.Tools
{
    public class ScissorTool : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HairCutSystem cutSystem;
        [SerializeField] private HairCutDetector cutDetector;
        [SerializeField] private UnityEngine.Camera mainCamera;

        [Header("Cut Parameters")]
        [SerializeField] private float cutInterval = 0.05f;
        [SerializeField] private float cutAreaRadius = 0.01f;

        [Header("Visual")]
        [SerializeField] private GameObject scissorCursorPrefab;
        [SerializeField] private bool showDebugSphere = false;

        [Header("Feedback")]
        [SerializeField] private bool hapticFeedback = true;
        [SerializeField] private float shakeIntensity = 0.002f;
        [SerializeField] private float shakeDuration = 0.05f;

        private GameObject scissorCursor;
        private float lastCutTime;
        private bool isDragging;
        private bool isActive = true;
        private int totalCutsThisSession;

        // Events for UI/audio feedback
        public event System.Action<Vector3> OnCutPerformed;
        public event System.Action OnToolActivated;
        public event System.Action OnToolDeactivated;

        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                if (scissorCursor != null)
                    scissorCursor.SetActive(value);

                if (value) OnToolActivated?.Invoke();
                else OnToolDeactivated?.Invoke();
            }
        }

        public int TotalCuts => totalCutsThisSession;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = UnityEngine.Camera.main;

            if (scissorCursorPrefab != null)
            {
                scissorCursor = Instantiate(scissorCursorPrefab);
                scissorCursor.SetActive(isActive);
            }
        }

        private void Update()
        {
            if (!isActive || cutSystem == null || cutDetector == null || mainCamera == null) return;

            UpdateCursorPosition();
            HandleInput();

            if (showDebugSphere)
            {
                DrawDebugCutArea();
            }
        }

        private void UpdateCursorPosition()
        {
            if (scissorCursor == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // Position cursor at a fixed distance along the ray
            scissorCursor.transform.position = ray.GetPoint(0.3f);
            scissorCursor.transform.rotation = Quaternion.LookRotation(ray.direction);
        }

        private void HandleInput()
        {
            // Single click cut
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = false;
                PerformSingleCut();
            }
            // Drag cut
            else if (Input.GetMouseButton(0))
            {
                isDragging = true;
                if (Time.time - lastCutTime >= cutInterval)
                {
                    PerformDragCut();
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
        }

        private void PerformSingleCut()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var result = cutDetector.DetectCut(ray);

            if (result.hit)
            {
                cutSystem.CutStrand(result.strandIndex, result.particleIndex);
                lastCutTime = Time.time;
                totalCutsThisSession++;

                OnCutPerformed?.Invoke(result.worldPosition);

                if (hapticFeedback)
                {
                    StartCoroutine(CameraShake());
                }
            }
        }

        private void PerformDragCut()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var results = cutDetector.DetectCutsInArea(ray, cutAreaRadius);

            if (results.Length > 0)
            {
                int[] strandIndices = new int[results.Length];
                int[] particleIndices = new int[results.Length];

                for (int i = 0; i < results.Length; i++)
                {
                    strandIndices[i] = results[i].strandIndex;
                    particleIndices[i] = results[i].particleIndex;
                }

                cutSystem.CutStrands(strandIndices, particleIndices);
                lastCutTime = Time.time;
                totalCutsThisSession += results.Length;

                if (results.Length > 0)
                {
                    OnCutPerformed?.Invoke(results[0].worldPosition);
                }

                if (hapticFeedback)
                {
                    StartCoroutine(CameraShake());
                }
            }
        }

        private System.Collections.IEnumerator CameraShake()
        {
            if (mainCamera == null) yield break;

            Vector3 originalPos = mainCamera.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                float x = Random.Range(-shakeIntensity, shakeIntensity);
                float y = Random.Range(-shakeIntensity, shakeIntensity);
                mainCamera.transform.localPosition = originalPos + new Vector3(x, y, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            mainCamera.transform.localPosition = originalPos;
        }

        private void DrawDebugCutArea()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 point = ray.GetPoint(0.3f);

            // Debug visualization
            Debug.DrawRay(ray.origin, ray.direction * 2f, Color.red);

            int segments = 16;
            float radius = isDragging ? cutAreaRadius : cutDetector.CutRadius;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * Mathf.PI * 2f;
                float angle2 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 p1 = point + mainCamera.transform.right * Mathf.Cos(angle1) * radius
                           + mainCamera.transform.up * Mathf.Sin(angle1) * radius;
                Vector3 p2 = point + mainCamera.transform.right * Mathf.Cos(angle2) * radius
                           + mainCamera.transform.up * Mathf.Sin(angle2) * radius;
                Debug.DrawLine(p1, p2, Color.yellow);
            }
        }

        public void ResetCutCount()
        {
            totalCutsThisSession = 0;
        }
    }
}
