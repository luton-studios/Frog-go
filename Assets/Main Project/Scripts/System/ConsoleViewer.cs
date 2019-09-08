using UnityEngine;
using System.Collections.Generic;

public class ConsoleViewer : MonoBehaviour {
    public static ConsoleViewer instance { get; private set; }
    
    private List<string> outputMessages;
    
    private void Awake() {
        instance = this;
        outputMessages = new List<string>();
    }

    private void OnEnable() {
        Application.logMessageReceived += HandleUnityLogging;
    }

    private void OnDisable() {
        Application.logMessageReceived -= HandleUnityLogging;
    }

    private void OnGUI() {
        for(int i = 0; i < outputMessages.Count; i++) {
            GUILayout.Label(outputMessages[i]);
        }
    }

    public void ClearConsoleOutput() {
        outputMessages.Clear();
    }
    
    private void HandleUnityLogging(string message, string stacktrace, LogType type) {
        switch(type) {
            case LogType.Warning:
                outputMessages.Add("WARNING: " + message);
                break;
            case LogType.Error:
                outputMessages.Add("ERROR: " + message);
                break;
            default:
                outputMessages.Add(message);
                break;
        }
    }
}