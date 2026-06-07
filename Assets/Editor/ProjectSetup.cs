using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class ProjectSetup
{
    [MenuItem("HairSalonSim/Setup Demo Scene")]
    public static void SetupDemoScene()
    {
        CreateDemoScene();
    }

    public static void SetupFromCommandLine()
    {
        Debug.Log("[ProjectSetup] Starting batch setup...");
        CreateDemoScene();
        Debug.Log("[ProjectSetup] Batch setup complete.");
    }

    private static void CreateDemoScene()
    {
        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // --- Mannequin Head (placeholder sphere) ---
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "MannequinHead";
        head.transform.position = Vector3.zero;
        head.transform.localScale = new Vector3(0.2f, 0.24f, 0.22f); // head-like proportions
        var headRenderer = head.GetComponent<Renderer>();
        if (headRenderer != null)
        {
            headRenderer.material.color = new Color(0.9f, 0.8f, 0.7f); // skin tone
        }

        // --- Hair System Root ---
        var hairRoot = new GameObject("HairSystem");
        hairRoot.transform.position = Vector3.zero;

        // Add core components
        var hairSetup = hairRoot.AddComponent<HairSalonSim.Core.HairSetup>();
        var cutSystem = hairRoot.AddComponent<HairSalonSim.Core.HairCutSystem>();
        var cutDetector = hairRoot.AddComponent<HairSalonSim.Core.HairCutDetector>();

        // Use SerializedObject to set private serialized fields
        SetSerializedField(hairSetup, "headTransform", head.transform);
        SetSerializedField(hairSetup, "cutSystem", cutSystem);
        SetSerializedField(hairSetup, "cutDetector", cutDetector);

        // --- Tools ---
        var toolsRoot = new GameObject("Tools");
        var toolManager = toolsRoot.AddComponent<HairSalonSim.Tools.ToolManager>();
        var scissorTool = toolsRoot.AddComponent<HairSalonSim.Tools.ScissorTool>();

        SetSerializedField(toolManager, "scissorTool", scissorTool);
        SetSerializedField(scissorTool, "cutSystem", cutSystem);
        SetSerializedField(scissorTool, "cutDetector", cutDetector);

        // --- Camera setup ---
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0f, 0.1f, 0.5f);
            mainCam.transform.LookAt(head.transform);
            mainCam.nearClipPlane = 0.01f;
            mainCam.fieldOfView = 45f;
            mainCam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);

            var orbitCam = mainCam.gameObject.AddComponent<HairSalonSim.Camera.OrbitCamera>();
            SetSerializedField(orbitCam, "target", head.transform);

            SetSerializedField(scissorTool, "mainCamera", mainCam);
        }

        // --- UI ---
        var uiRoot = new GameObject("DemoUI");
        var demoUI = uiRoot.AddComponent<HairSalonSim.UI.DemoUI>();
        SetSerializedField(demoUI, "hairSetup", hairSetup);
        SetSerializedField(demoUI, "cutSystem", cutSystem);
        SetSerializedField(demoUI, "scissorTool", scissorTool);
        SetSerializedField(demoUI, "toolManager", toolManager);
        if (mainCam != null)
        {
            var orbitCam = mainCam.GetComponent<HairSalonSim.Camera.OrbitCamera>();
            SetSerializedField(demoUI, "orbitCamera", orbitCam);
        }

        // --- Directional Light adjustment ---
        var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.2f;
                light.color = new Color(1f, 0.96f, 0.9f);
            }
        }

        // Save scene
        string scenePath = "Assets/Scenes/DemoScene.unity";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
            System.IO.Path.Combine(Application.dataPath, "../", scenePath)));
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[ProjectSetup] Scene saved to {scenePath}");

        // Add to build settings
        var buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = buildScenes;
        Debug.Log("[ProjectSetup] Build settings updated.");
    }

    private static void SetSerializedField(Component component, string fieldName, Object value)
    {
        var so = new SerializedObject(component);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning($"[ProjectSetup] Field '{fieldName}' not found on {component.GetType().Name}");
        }
    }
}
