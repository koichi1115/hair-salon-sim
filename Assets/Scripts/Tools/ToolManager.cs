using UnityEngine;

namespace HairSalonSim.Tools
{
    public enum ToolType
    {
        Scissors,
        // Future: Dryer, Color, Clipper, Perm
    }

    public class ToolManager : MonoBehaviour
    {
        [SerializeField] private ScissorTool scissorTool;

        private ToolType currentTool = ToolType.Scissors;

        public ToolType CurrentTool => currentTool;

        public event System.Action<ToolType> OnToolChanged;

        private void Start()
        {
            SetTool(ToolType.Scissors);
        }

        private void Update()
        {
            // Tool switching via number keys (for future expansion)
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetTool(ToolType.Scissors);
            }
        }

        public void SetTool(ToolType tool)
        {
            // Deactivate all tools
            if (scissorTool != null) scissorTool.IsActive = false;

            // Activate selected tool
            currentTool = tool;
            switch (tool)
            {
                case ToolType.Scissors:
                    if (scissorTool != null) scissorTool.IsActive = true;
                    break;
            }

            OnToolChanged?.Invoke(tool);
        }
    }
}
