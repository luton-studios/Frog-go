using UnityEngine;
using UnityEditor;
using System.IO;

public class SpriteGenerator : EditorWindow {
    public string directory = string.Empty;
    public string saveLocation = string.Empty;
    public GameObject prefabObject;
    public int imageResolutionX = 128;
    public int imageResolutionY = 128;
    public bool perspectiveMode = true;
    public float fieldOfView = 60f;
    public float orthoSize = 1f;

    private bool directoryExists;
    private Camera renderCamera;
    private GameObject modelInst;
    private GameObject oldPrefabObject;
    private RenderTexture previewTexture;
    private double lastRenderTime;
    private Vector3 lastPos;
    private Quaternion lastRot;
    private float lastFov;
    private int lastResX;
    private int lastResY;

    private const double MIN_UPDATE_INTERVAL = 0.5f; // Update at least twice every second.

    [MenuItem("Tools/Sprite Generator", false, 2)]
    public static void OpenGeneratorWindow() {
        SpriteGenerator newGen = GetWindow<SpriteGenerator>("Sprite Generator");
        newGen.directory = EditorPrefs.GetString("SG_Directory", newGen.directory);
        newGen.imageResolutionX = EditorPrefs.GetInt("SG_ResolutionX", newGen.imageResolutionX);
        newGen.imageResolutionY = EditorPrefs.GetInt("SG_ResolutionY", newGen.imageResolutionY);
        newGen.perspectiveMode = EditorPrefs.GetBool("SG_Perspective", newGen.perspectiveMode);
        newGen.fieldOfView = EditorPrefs.GetFloat("SG_FieldOfView", newGen.fieldOfView);
        newGen.orthoSize = EditorPrefs.GetFloat("SG_OrthoSize", newGen.orthoSize);
        newGen.lastRenderTime = EditorApplication.timeSinceStartup;

        newGen.Update();
    }

    private void OnGUI() {
        GUILayout.Space(5f);

        EditorGUILayout.BeginHorizontal();

        string dispDir = directory;
        int startIndex = dispDir.LastIndexOf("Assets/");
        if(startIndex > -1) {
            dispDir = dispDir.Substring(startIndex);
        }

        EditorGUIUtility.labelWidth = 110f;
        EditorGUILayout.TextField("Target Directory:", dispDir);
        EditorGUIUtility.labelWidth = 0f;

        bool dirIsInvalid = string.IsNullOrEmpty(directory) || !directoryExists;

        if(dirIsInvalid) {
            GUI.color = new Color(1f, 1f, 0.4f);
        }

        if(GUILayout.Button("[Select New Directory]", GUILayout.MaxWidth(150f))) {
            string oldDir = directory;
            directory = EditorUtility.OpenFolderPanel("Select a directory to save icon in...", directory, string.Empty);

            if(string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(oldDir)) {
                directory = oldDir;
            }

            RefreshSaveLocation();
            GUI.FocusControl("PrefabObject");
            EditorPrefs.SetString("SG_Directory", directory);
        }
        
        EditorGUILayout.EndHorizontal();

        if(dirIsInvalid) {
            GUI.color = Color.white;
            EditorGUILayout.HelpBox("Please select a valid directory to continue...", MessageType.Error);
            return;
        }

        if(SceneView.lastActiveSceneView == null) {
            EditorGUILayout.HelpBox("Unable to retrieve scene view camera!", MessageType.Error);
            return;
        }

        GUILayout.Space(5f);

        GUI.SetNextControlName("PrefabObject");
        prefabObject = (GameObject)EditorGUILayout.ObjectField("Target Object:", prefabObject, typeof(GameObject), true);
        if(prefabObject == null) {
            EditorGUILayout.HelpBox("Please assign a prefab to continue...", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        imageResolutionX = EditorGUILayout.DelayedIntField("Image Resolution:", imageResolutionX, GUILayout.MaxWidth(60f + EditorGUIUtility.labelWidth));
        EditorGUILayout.LabelField("x", GUILayout.MaxWidth(11f));
        imageResolutionY = EditorGUILayout.DelayedIntField(imageResolutionY, GUILayout.MaxWidth(60f));
        EditorGUILayout.EndHorizontal();

        perspectiveMode = EditorGUILayout.Toggle("Perspective:", perspectiveMode);

        if(perspectiveMode)
            fieldOfView = EditorGUILayout.Slider("Field of View:", fieldOfView, 10f, 120f);
        else
            orthoSize = EditorGUILayout.Slider("Orthographic Size:", orthoSize, 0.01f, 4f);
        
        if(modelInst != null && previewTexture != null) {
            if(GUILayout.Button("Focus on Object", GUILayout.MaxWidth(150f))) {
                FocusOnObject();
            }

            GUILayout.Space(8f);

            EditorGUILayout.BeginHorizontal();
            saveLocation = EditorGUILayout.TextField("Save Location:", saveLocation);
            GUILayout.Space(-3f);
            EditorGUILayout.LabelField(".png", GUILayout.Width(30f));
            EditorGUILayout.EndHorizontal();

            GUI.color = new Color(1f, 0.8f, 0.5f);
            GUILayout.Space(6f);

            if(GUILayout.Button("Generate Icon")) {
                RenderAndFinalizeIcon();
            }

            GUI.color = Color.white;
            
            float width = Mathf.Min(previewTexture.width, Screen.width - 10f);
            float aspect = previewTexture.height / (float)previewTexture.width;
            float height = Mathf.Min(width * aspect, Screen.height - 210f);
            float actualWidth = Mathf.Min(width, height / aspect);

            EditorGUI.DrawTextureTransparent(new Rect(2f, 185f, actualWidth, actualWidth * aspect), previewTexture);
        }
        else {
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox("Something went wrong with the preview render!", MessageType.Error);
        }
    }

    public void Update() {
        directoryExists = Directory.Exists(directory);
        ValidatePreview();

        Repaint();
    }

    private void RefreshSaveLocation() {
        if(prefabObject != null) {
            saveLocation = directory + "/icon_" + prefabObject.name.ToLowerInvariant().Replace(' ', '_');
            int startIndex = saveLocation.LastIndexOf("Assets/");

            if(startIndex > -1)
                saveLocation = saveLocation.Substring(startIndex);
        }
    }

    private void ValidatePreview() {
        if(prefabObject != oldPrefabObject) {
            if(modelInst != null) {
                DestroyImmediate(modelInst);
            }

            if(prefabObject != null) {
                modelInst = Instantiate(prefabObject, Vector3.up * 50f, Quaternion.identity);
                modelInst.hideFlags = HideFlags.DontSave;
                LutonUtils.SetLayerRecursive(modelInst, 15);
                Selection.activeGameObject = modelInst;

                RefreshSaveLocation();
                FocusOnObject();
            }

            oldPrefabObject = prefabObject;
        }
        
        if(renderCamera == null) {
            GameObject renderCamGo = new GameObject("RenderCam");
            renderCamGo.hideFlags = HideFlags.HideAndDontSave;
            renderCamera = renderCamGo.AddComponent<Camera>();
            renderCamera.depth = -5;
            renderCamera.orthographic = !perspectiveMode;
            renderCamera.cullingMask = 1 << 15;
            renderCamera.allowHDR = true;
            renderCamera.allowMSAA = false;

            if(renderCamera.orthographic) {
                renderCamera.nearClipPlane = 0f;
                renderCamera.farClipPlane = 200f;
                renderCamera.orthographicSize = orthoSize;
            }
            else {
                renderCamera.nearClipPlane = 0.04f;
                renderCamera.farClipPlane = 200f;
                renderCamera.fieldOfView = fieldOfView;
            }

            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.clear;

            /*
            SMAA smaa = renderCamGo.AddComponent<SMAA>();
            PostProcessMaster ppm = renderCamGo.AddComponent<PostProcessMaster>();
            ppm.tonemapping = true;
            ppm.exposure = 2.75f;

            smaa.luminosityAdaptation = true;
            smaa.threshold = 0.0105f;
            smaa.depthThreshold = 0.5f;
            smaa.maxSearchSteps = 12;
            smaa.maxSearchStepsDiag = 24;
            smaa.cornerRounding = 16;
            smaa.localContrastAdaptationFactor = 2.5f;*/
        }
        else {
            renderCamera.orthographic = !perspectiveMode;

            if(renderCamera.orthographic)
                renderCamera.orthographicSize = orthoSize;
            else
                renderCamera.fieldOfView = fieldOfView;

            if(modelInst != null) {
                SceneView view = SceneView.lastActiveSceneView;

                if(view != null) {
                    double curTime = EditorApplication.timeSinceStartup;
                    Vector3 addPos = view.camera.transform.position + modelInst.transform.position;
                    Quaternion addRot = view.camera.transform.rotation * modelInst.transform.rotation;

                    if(curTime - lastRenderTime >= MIN_UPDATE_INTERVAL || (addPos - lastPos).sqrMagnitude > 0f || Quaternion.Angle(addRot, lastRot) > 0f || Mathf.Abs(((renderCamera.orthographic) ? orthoSize : fieldOfView) - lastFov) > 0f) {
                        renderCamera.transform.position = view.camera.transform.position;
                        renderCamera.transform.rotation = view.camera.transform.rotation;

                        if(renderCamera.orthographic) {
                            renderCamera.orthographicSize = orthoSize;
                            lastFov = orthoSize;
                        }
                        else {
                            renderCamera.fieldOfView = fieldOfView;
                            lastFov = fieldOfView;
                        }

                        renderCamera.Render();

                        lastRenderTime = curTime;
                        lastPos = addPos;
                        lastRot = addRot;
                    }
                }
            }
        }

        if(previewTexture == null) {
            imageResolutionX = Mathf.Clamp(imageResolutionX, 16, 4096);
            imageResolutionY = Mathf.Clamp(imageResolutionY, 16, 4096);
            previewTexture = new RenderTexture(imageResolutionX, imageResolutionY, 24, RenderTextureFormat.ARGB32);
            previewTexture.hideFlags = HideFlags.HideAndDontSave;
            lastResX = imageResolutionX;
            lastResY = imageResolutionY;

            renderCamera.targetTexture = previewTexture;
            renderCamera.Render();
        }
        else if(imageResolutionX != lastResX || imageResolutionY != lastResY) {
            renderCamera.targetTexture = null;
            DestroyImmediate(previewTexture);
        }
    }

    private void FocusOnObject() {
        SceneView view = SceneView.lastActiveSceneView;

        if(view != null) {
            view.LookAt(modelInst.transform.position);
            view.Repaint();
        }
    }

    private void RenderAndFinalizeIcon() {
        if(renderCamera != null && modelInst != null && Directory.Exists(directory)) {
            renderCamera.Render();

            Texture2D capture = new Texture2D(imageResolutionX, imageResolutionY, TextureFormat.RGBA32, true);
            RenderTexture.active = previewTexture;
            capture.ReadPixels(new Rect(0f, 0f, imageResolutionX, imageResolutionY), 0, 0);
            RenderTexture.active = null;

            byte[] data = capture.EncodeToPNG();
            string finalSaveLoc = saveLocation + ".png";
            File.WriteAllBytes(finalSaveLoc, data);
            
            EditorUtility.DisplayDialog("Icon generation successful!", "Saved at: " + finalSaveLoc, "Ok");
            AssetDatabase.Refresh();
        }
    }

    private void OnDestroy() {
        if(renderCamera != null) {
            DestroyImmediate(renderCamera.gameObject);
        }

        if(modelInst != null) {
            DestroyImmediate(modelInst);
        }

        if(previewTexture != null) {
            DestroyImmediate(previewTexture);
        }

        EditorPrefs.SetString("SG_Directory", directory);
        EditorPrefs.SetInt("SG_ResolutionX", imageResolutionX);
        EditorPrefs.SetInt("SG_ResolutionY", imageResolutionY);
        EditorPrefs.SetBool("SG_Perspective", perspectiveMode);
        EditorPrefs.SetFloat("SG_FieldOfView", fieldOfView);
        EditorPrefs.SetFloat("SG_OrthoSize", orthoSize);
    }
}