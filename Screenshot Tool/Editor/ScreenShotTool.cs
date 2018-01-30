using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class ScreenShotTool : EditorWindow {
    private int widthSize = 1024;
    private int heightSize = 768;
    private int AA = 1;
    private bool useGameCamera = false;
    private bool useAlphaChannel = false;
    private bool inProcess = false;
    //
    private float secs = 10f;
    private float startVal = 0f;
    private float progress = 0f;
    private string LoadTitle = "Загрузка..";
    private Camera ScreenShotCamera;

    private enum SettingsAct { Load, Save }
    private enum Menu { Main, Settings }

    Texture2D settings_Image;
    Texture2D capture_Image;
    bool reset;
    [MenuItem("Tools/Screeshot Tool")]
    static void OpenScreenShotTool() {
        ScreenShotTool _SST = EditorWindow.GetWindow<ScreenShotTool>();
        _SST.Init();
    }
    GUISkin skin;
    void Init() {
        this.minSize = new Vector2(240, 145);
        this.position = new Rect(300, 300, minSize.x, minSize.y);
        this.titleContent = new GUIContent("ScreenShot Tool");
        Settings(SettingsAct.Load);
        skin = Resources.Load("stool", typeof(GUISkin)) as GUISkin;
    }

    void OnGUI() {
        GUI.skin = skin;
        if (inProcess) {
            GUILayout.Label("in Progress: " + (progress * 100).ToString());
            EditorUtility.DisplayProgressBar("ScreenShot", LoadTitle, progress);
            
        }
        else {
            GUILayout.BeginHorizontal();
            widthSize = int.Parse(GUILayout.TextField(widthSize + ""));
            GUILayout.Label("x", GUILayout.Width(10));
            heightSize = int.Parse(GUILayout.TextField(heightSize + ""));
            GUI.color = Color.red;
            if (GUILayout.Button("r", GUILayout.MaxWidth(20))) {
                reset = true;
            }
            if (reset) {
                if (EditorUtility.DisplayDialog("Reset settings", "Choose an action!", "Reset", "Cancel")) {
                    widthSize = 1024; heightSize = 768;
                    useAlphaChannel = false; useGameCamera = false; AA = 0;
                    reset = false;
                }
                else { reset = false; }
            }

            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            useGameCamera = GUILayout.Toggle(useGameCamera, "Use game camera");
            useAlphaChannel = GUILayout.Toggle(useAlphaChannel, "Use alpha channel");
            GUILayout.BeginHorizontal();
            GUILayout.Label("AntiAliasing: ");

            if (GUILayout.Button("-", GUILayout.MaxWidth(20)))
                AA = Mathf.Clamp(AA - (AA / 2 > 1 ? AA / 2 : 1), 0, 8);

            GUILayout.Label(AA > 0 ? ("x" + AA) : "off", GUILayout.MaxWidth(30));

            if (GUILayout.Button("+", GUILayout.MaxWidth(20)))
                AA = Mathf.Clamp(AA + (AA > 0 ? AA : 1), 0, 8);

            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUI.color = new Color(0, 0.9F, 0.9F, 1);
            if (GUILayout.Button(inProcess?"Wait..":"Take ScreenShot",GUILayout.MinHeight(40)) && !inProcess) {
                Settings(SettingsAct.Save);
                secs = widthSize;
                startVal = (float)EditorApplication.timeSinceStartup;
                ScreenshotProcess();
            }
            GUI.color = Color.white;
        }
    }

    void OnInspectorUpdate() {
        Repaint();
    }

    void SaveScreenShot(string path, Texture2D screenShot) {
        File.WriteAllBytes(path, screenShot.EncodeToPNG());
    }

    private Texture2D CaptureTexture() {
        ScreenShotCamera.Render();
        TextureFormat CurImageFormat = useAlphaChannel ? TextureFormat.ARGB32 : TextureFormat.RGB24;
        Texture2D tex = new Texture2D(widthSize, heightSize, CurImageFormat, false);
        tex.ReadPixels(new Rect(0, 0, widthSize, heightSize), 0, 0, false);
        return tex;
    }

    void ScreenshotProcess() {
        inProcess = true;
        progress = 0;
        this.Repaint();

        LoadTitle = "Load Camera.."; this.Repaint();
        ScreenShotCamera = GetCamera();
        if (ScreenShotCamera == null) {
            Debug.LogError("ScreenShotTool Error: can't get camera!");
            EndProcess();
            return;
        }

        CameraClearFlags TEMP_camCCF = ScreenShotCamera.clearFlags;
        if (useAlphaChannel)
            ScreenShotCamera.clearFlags = CameraClearFlags.Color;

        string path = Application.dataPath + "/Screenshots/";
        if (!AssetDatabase.IsValidFolder("Assets/Screenshots"))
            AssetDatabase.CreateFolder("Assets", "Screenshots");

        string curscene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string filename = "screenshot_" + curscene + "_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";

        RenderTextureFormat format = useAlphaChannel ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;
        RenderTexture rt = new RenderTexture(widthSize, heightSize, 24, format, RenderTextureReadWrite.Default);
        if (AA > 0) rt.antiAliasing = AA;
        ScreenShotCamera.targetTexture = rt;
        RenderTexture.active = rt;

        Texture2D final = null;
        LoadTitle = "Capture image.."; this.Repaint();

        try {
            if (useAlphaChannel) {
                ScreenShotCamera.clearFlags = CameraClearFlags.Depth;
                progress = 0.5f; this.Repaint();
                final = CaptureTexture();
                final.alphaIsTransparency = true;
                final.Apply();
            }
            else {
                ScreenShotCamera.clearFlags = CameraClearFlags.Skybox;
                progress = 0.5f; this.Repaint();
                final = CaptureTexture();
            }
        }
        catch (System.Exception) {
            Debug.LogWarning("ScreenShot error");
            EndProcess();
            return;
            throw;
        }

        LoadTitle = "Saving.."; this.Repaint();
        SaveScreenShot(path + filename, final);

        RenderTexture.active = null;
        ScreenShotCamera.targetTexture = null;
        if (!useGameCamera)
            GameObject.DestroyImmediate(ScreenShotCamera.gameObject);
        else
            ScreenShotCamera.clearFlags = TEMP_camCCF;

        Debug.Log("ScreenShot complete! \n path:" + path + filename);
        LoadTitle = "ScreenShot complete!"; this.Repaint();

        Application.OpenURL(path + filename);
        progress = 1.0f;
        EndProcess();
    }
    void EndProcess() {
        RenderTexture.active = null;
        AssetDatabase.Refresh();
        inProcess = false;
        EditorUtility.ClearProgressBar();
        this.Repaint();
    }

    Camera GetCamera() {
        Camera cam = null;
        if (!useGameCamera) {
            SceneView viev = null;
            if (SceneView.currentDrawingSceneView != null) {
                viev = SceneView.currentDrawingSceneView;
            }
            else if (SceneView.lastActiveSceneView != null) {
                viev = SceneView.lastActiveSceneView;
            }

            Quaternion ECRot = viev.camera.transform.localRotation;
            Vector3 ECPos = viev.camera.transform.localPosition;

            GameObject sc = GameObject.Instantiate(
                Resources.Load("EdtCam", typeof(GameObject)),
                ECPos,
                ECRot) as GameObject;

            cam = sc.GetComponent<Camera>();
        }
        else {
            cam = Camera.main;
            if (cam == null)
                cam = GameObject.Find("Camera").GetComponent<Camera>();
            if (cam == null)
                cam = GameObject.Find("PlayerCamera").GetComponent<Camera>();
        }
        return cam;
    }

    void Settings(SettingsAct action) {
        if (action == SettingsAct.Save) {
            EditorPrefs.SetInt("AA", AA);
            EditorPrefs.SetInt("W", widthSize);
            EditorPrefs.SetInt("H", heightSize);
            EditorPrefs.SetBool("UGC", useGameCamera);
            EditorPrefs.SetBool("UAC", useAlphaChannel);
        }
        else if (action == SettingsAct.Load) {
            AA = EditorPrefs.GetInt("AA", AA);
            widthSize = EditorPrefs.GetInt("W", widthSize);
            heightSize = EditorPrefs.GetInt("H", heightSize);
            useGameCamera = EditorPrefs.GetBool("UGC", useGameCamera);
            useAlphaChannel = EditorPrefs.GetBool("UAC", useAlphaChannel);
        }
    }
}
