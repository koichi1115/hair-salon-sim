using UnityEngine;

namespace HairSalonSim.Camera
{
    public class OrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Distance")]
        [SerializeField] private float distance = 0.5f;
        [SerializeField] private float minDistance = 0.2f;
        [SerializeField] private float maxDistance = 1.5f;

        [Header("Rotation")]
        [SerializeField] private float rotateSpeed = 5f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float zoomSmoothing = 10f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 0.005f;

        private float yaw;
        private float pitch = 20f;
        private float targetDistance;
        private Vector3 panOffset;

        private void Start()
        {
            targetDistance = distance;
            if (target != null)
            {
                Vector3 dir = transform.position - target.position;
                yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                pitch = Mathf.Asin(dir.y / dir.magnitude) * Mathf.Rad2Deg;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleRotation();
            HandleZoom();
            HandlePan();
            ApplyTransform();
        }

        private void HandleRotation()
        {
            if (Input.GetMouseButton(1)) // Right drag = rotate
            {
                yaw += Input.GetAxis("Mouse X") * rotateSpeed;
                pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                targetDistance -= scroll * zoomSpeed * 10f;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }
            distance = Mathf.Lerp(distance, targetDistance, Time.deltaTime * zoomSmoothing);
        }

        private void HandlePan()
        {
            if (Input.GetMouseButton(2)) // Middle drag = pan
            {
                Vector3 right = transform.right * (-Input.GetAxis("Mouse X") * panSpeed * distance);
                Vector3 up = transform.up * (-Input.GetAxis("Mouse Y") * panSpeed * distance);
                panOffset += right + up;
            }
        }

        private void ApplyTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = target.position + panOffset + rotation * new Vector3(0f, 0f, -distance);

            transform.position = position;
            transform.LookAt(target.position + panOffset);
        }

        public void ResetView()
        {
            yaw = 0f;
            pitch = 20f;
            targetDistance = 0.5f;
            panOffset = Vector3.zero;
        }
    }
}
