using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TileController))]
public class TileControllerEditor : Editor {
    private Color32[] distributionColors;
    private Vector2 scrollPos;
    private Texture2D legendColorTex;

    private void OnEnable() {
        distributionColors = new Color32[TileEntity.TYPE_COUNT];
        distributionColors[(int)TileEntity.Type.LilyPad] = new Color32(40, 150, 40, 255);
        distributionColors[(int)TileEntity.Type.DriftingLog] = new Color32(160, 72, 45, 255);
        distributionColors[(int)TileEntity.Type.FloatingBox] = new Color32(185, 150, 115, 255);

        legendColorTex = new Texture2D(6, 6, TextureFormat.RGB24, false, true);
        Color[] white = new Color[36];

        for(int i = 0; i < white.Length; i++)
            white[i] = new Color(1f, 1f, 1f, 1f);

        legendColorTex.SetPixels(white);
        legendColorTex.Apply();
    }

    private void OnDisable() {
        if(legendColorTex != null)
            DestroyImmediate(legendColorTex);
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        TileController tc = (TileController)target;

        GUILayout.Space(10f);
        GUILayout.Label("Distribution", EditorStyles.boldLabel);

        bool mismatch = false;

        for(int i = 1; i < tc.tileTypeDistributions.Length; i++) {
            if(tc.tileTypeDistributions[i].difficultyProbabilities.Length != tc.tileTypeDistributions[0].difficultyProbabilities.Length)
                mismatch = true;
        }

        if(mismatch) {
            EditorGUILayout.HelpBox("Mismatching lengths of difficulty.", MessageType.Error);
            return;
        }
        
        if(GUILayout.Button("Normalize") && tc.tileTypeDistributions.Length > 0) {
            int difficultyLength = tc.tileTypeDistributions[0].difficultyProbabilities.Length;

            for(int i = 0; i < difficultyLength; i++) {
                float sum = 0f;

                for(int j = 0; j < tc.tileTypeDistributions.Length; j++) {
                    sum += tc.tileTypeDistributions[j].difficultyProbabilities[i];
                }

                if(sum > 0f) {
                    for(int j = 0; j < tc.tileTypeDistributions.Length; j++)
                        tc.tileTypeDistributions[j].difficultyProbabilities[i] /= sum;
                }
            }

            EditorUtility.SetDirty(tc);
        }

        for(int i = 0; i < TileEntity.TYPE_COUNT; i++) {
            EditorGUILayout.BeginHorizontal();
            GUI.color = distributionColors[i];
            GUILayout.Box(legendColorTex, GUILayout.Width(12f), GUILayout.Height(12f));
            GUI.color = Color.white;
            GUILayout.Label(((TileEntity.Type)i).ToString());
            EditorGUILayout.EndHorizontal();
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(140f));
        EditorGUILayout.BeginHorizontal();

        if(tc.tileTypeDistributions.Length > 0) {
            for(int i = 0; i < tc.tileTypeDistributions[0].difficultyProbabilities.Length; i++) {
                GUI.color = Color.white;
                GUI.Label(new Rect(i * 20f, 0f, 20f, 16f), (i + 1).ToString());
                float y = 0f;

                for(int x = 0; x < tc.tileTypeDistributions.Length; x++) {
                    float probability = tc.tileTypeDistributions[x].difficultyProbabilities[i];

                    GUI.color = distributionColors[x];
                    GUI.DrawTexture(new Rect(i * 20f, y + 20f, 15f, 100f * probability), Texture2D.whiteTexture);
                    y += probability * 100f;
                }

                GUILayout.Space(20f);
            }
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }
}