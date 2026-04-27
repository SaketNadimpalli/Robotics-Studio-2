// HandDataLogger.cs
// -----------------------------------------------------------------------------
// Prints the live pose + velocities of both hands to Unity's Console at a
// configurable interval. Drop on any empty GameObject, wire up the two
// HandTracker references, and you'll see the numbers stream while you play.
//
// Throttled by default (4 prints/sec). Logging every frame floods the Console
// to the point it becomes unusable; set logInterval = 0 if you really want it.
// -----------------------------------------------------------------------------

using UnityEngine;

public class HandDataLogger : MonoBehaviour
{
    [Header("Sources")]
    public HandTracker leftHand;
    public HandTracker rightHand;

    [Header("Print rate")]
    [Tooltip("Seconds between Console prints. 0.25 = 4 lines/sec per hand. " +
             "Set to 0 to log every frame (very spammy).")]
    public float logInterval = 0.25f;

    float _timer;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < logInterval) return;
        _timer = 0f;

        LogHand("L", leftHand);
        LogHand("R", rightHand);
    }

    static void LogHand(string tag, HandTracker h)
    {
        if (h == null)       { Debug.Log($"{tag}: (no tracker assigned)"); return; }
        if (!h.IsTracked)    { Debug.Log($"{tag}: not tracked");           return; }

        Vector3 p = h.Position;
        Vector3 v = h.LinearVelocity;
        Vector3 w = h.AngularVelocity;

        Debug.Log(
            $"{tag}  pos=({p.x:F3}, {p.y:F3}, {p.z:F3}) m   " +
            $"v=({v.x:F3}, {v.y:F3}, {v.z:F3}) |v|={v.magnitude:F3} m/s   " +
            $"w=({w.x:F3}, {w.y:F3}, {w.z:F3}) |w|={w.magnitude:F3} rad/s"
        );
    }
}