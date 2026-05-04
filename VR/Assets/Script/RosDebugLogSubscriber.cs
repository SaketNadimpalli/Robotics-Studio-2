using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class RosDebugLogSubscriber : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private int maxLines = 500;
    [SerializeField] private string topicName = "/connect4/debug_log";

    private readonly Queue<string> lines = new Queue<string>();
    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<StringMsg>(topicName, OnLog);
        Debug.Log($"RosDebugLogSubscriber subscribed to {topicName}");
    }

    void OnLog(StringMsg msg)
    {
        // Split incoming message on newlines so multi-line board prints become separate lines
        foreach (var rawLine in msg.data.Split('\n'))
        {
            string line = rawLine.Replace("🔍", "[search]").Replace("→", "->");
            if (line.Contains("[WARN]")) line = $"<color=#ffd93d>{line}</color>";
            else if (line.Contains("[ERROR]")) line = $"<color=#ff6b6b>{line}</color>";
            else if (line.Contains("Best column")) line = $"<color=#9bf6ff>{line}</color>";

            lines.Enqueue(line);
            while (lines.Count > maxLines) lines.Dequeue();
        }

        if (targetText != null)
            targetText.text = string.Join("\n", lines);
    }
}