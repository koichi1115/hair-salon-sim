using UnityEngine;
using HairSalonSim.Core;
using HairSalonSim.Tools;

namespace HairSalonSim.UI
{
    public class DemoUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HairSetup hairSetup;
        [SerializeField] private HairCutSystem cutSystem;
        [SerializeField] private ScissorTool scissorTool;
        [SerializeField] private ToolManager toolManager;
        [SerializeField] private Camera.OrbitCamera orbitCamera;

        [Header("UI Settings")]
        [SerializeField] private bool showDebugInfo = true;

        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized;

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fixedHeight = 40,
                fixedWidth = 200
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // Main panel
            GUILayout.BeginArea(new Rect(10, 10, 220, 400));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Hair Salon Sim", headerStyle);
            GUILayout.Label("Phase 1 Demo", labelStyle);
            GUILayout.Space(10);

            // Tool info
            if (toolManager != null)
            {
                GUILayout.Label($"Tool: {toolManager.CurrentTool}", labelStyle);
            }

            if (scissorTool != null)
            {
                GUILayout.Label($"Cuts: {scissorTool.TotalCuts}", labelStyle);
            }

            GUILayout.Space(10);

            // Reset button
            if (GUILayout.Button("Reset Hair (R)", buttonStyle))
            {
                ResetHair();
            }

            // Reset camera
            if (GUILayout.Button("Reset Camera (C)", buttonStyle))
            {
                ResetCamera();
            }

            GUILayout.Space(10);

            // Debug toggle
            showDebugInfo = GUILayout.Toggle(showDebugInfo, " Show Debug Info");

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Debug info panel
            if (showDebugInfo)
            {
                DrawDebugPanel();
            }

            // Controls help
            DrawControlsHelp();
        }

        private void Update()
        {
            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetHair();
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                ResetCamera();
            }
        }

        private void ResetHair()
        {
            if (hairSetup != null)
            {
                hairSetup.ResetHair();
            }
            if (scissorTool != null)
            {
                scissorTool.ResetCutCount();
            }
            Debug.Log("[DemoUI] Hair reset");
        }

        private void ResetCamera()
        {
            if (orbitCamera != null)
            {
                orbitCamera.ResetView();
            }
            Debug.Log("[DemoUI] Camera reset");
        }

        private void DrawDebugPanel()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 230, 10, 220, 200));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Debug Info", headerStyle);

            if (cutSystem != null && cutSystem.Initialized)
            {
                GUILayout.Label($"Strands: {cutSystem.StrandCount}", labelStyle);
                GUILayout.Label($"Particles/Strand: {cutSystem.StrandParticleCount}", labelStyle);

                int cutCount = 0;
                for (int i = 0; i < cutSystem.StrandCount; i++)
                {
                    if (cutSystem.IsStrandCut(i)) cutCount++;
                }
                GUILayout.Label($"Cut Strands: {cutCount}/{cutSystem.StrandCount}", labelStyle);
            }

            GUILayout.Label($"FPS: {1f / Time.smoothDeltaTime:F0}", labelStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawControlsHelp()
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - 120, 300, 110));
            GUILayout.BeginVertical("box");

            GUILayout.Label("Controls", labelStyle);
            GUILayout.Label("Left Click/Drag: Cut hair", labelStyle);
            GUILayout.Label("Right Drag: Rotate camera", labelStyle);
            GUILayout.Label("Scroll: Zoom", labelStyle);
            GUILayout.Label("Middle Drag: Pan", labelStyle);
            GUILayout.Label("R: Reset hair  C: Reset camera", labelStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
