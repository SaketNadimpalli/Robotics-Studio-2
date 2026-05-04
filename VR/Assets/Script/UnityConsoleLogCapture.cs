using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UnityConsoleLogCapture : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private int maxLines = 500;

    private readonly Queue<string> lines = new Queue<string>();

    void OnEnable() => Application.logMessageReceived += HandleLog;
    void OnDisable() => Application.logMessageReceived -= HandleLog;

    void HandleLog(string log, string stack, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error or LogType.Exception => "<color=#ff6b6b>[ERR]</color> ",
            LogType.Warning => "<color=#ffd93d>[WRN]</color> ",
            _ => "<color=#a8dadc>[LOG]</color> "
        };
        lines.Enqueue(prefix + log);
        while (lines.Count > maxLines) lines.Dequeue();
        if (targetText != null) targetText.text = string.Join("\n", lines);
    }
}