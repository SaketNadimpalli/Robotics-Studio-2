// HandDataHUD.cs
// -----------------------------------------------------------------------------
// Renders the live pose + twist of one or both HandTrackers to a TextMeshPro
// label on a Canvas. Handy for confirming your numbers before you start
// sending them over ROS.
//
// Setup:
//   1) Create a Canvas (Screen Space - Overlay is simplest).
//   2) Add a TextMeshPro - Text (UI) to it, anchor it top-left, make it big
//      enough that 8 lines fit comfortably (~500x260, font size ~22).
//   3) Put this component anywhere in the scene. Drag your Left/Right
//      HandTracker components and the TMP_Text into its inspector slots.
// -----------------------------------------------------------------------------

using System.Text;
using TMPro;
using UnityEngine;

public class HandDataHUD : MonoBehaviour
{
    [Header("Sources")]
    public HandTracker leftHand;
    public HandTracker rightHand;

    [Header("Output label (TextMeshPro)")]
    public TMP_Text label;

    [Header("Also log to Console (spammy)")]
    public bool alsoLogToConsole = false;

    readonly StringBuilder _sb = new StringBuilder(512);

    void LateUpdate()
    {
        _sb.Clear();
        AppendHand(_sb, "L", leftHand);
        _sb.AppendLine();
        AppendHand(_sb, "R", rightHand);

        if (label != null)       label.text = _sb.ToString();
        if (alsoLogToConsole)    Debug.Log(_sb.ToString());
    }

    static void AppendHand(StringBuilder sb, string tag, HandTracker h)
    {
        if (h == null)        { sb.Append(tag).AppendLine(": (no tracker assigned)"); return; }
        if (!h.IsTracked)     { sb.Append(tag).AppendLine(": not tracked");           return; }

        Vector3 p = h.Position;
        Vector3 e = h.Rotation.eulerAngles;
        Vector3 v = h.LinearVelocity;
        Vector3 w = h.AngularVelocity;

        // Use invariant culture so we always print with '.' decimals.
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        sb.AppendFormat(ci, "{0}  pos (m)    : {1,7:F3} {2,7:F3} {3,7:F3}\n",  tag, p.x, p.y, p.z);
        sb.AppendFormat(ci, "{0}  rot (deg)  : {1,7:F1} {2,7:F1} {3,7:F1}\n",  tag, e.x, e.y, e.z);
        sb.AppendFormat(ci, "{0}  v   (m/s)  : {1,7:F3} {2,7:F3} {3,7:F3}  |v|={4:F3}\n",
                            tag, v.x, v.y, v.z, v.magnitude);
        sb.AppendFormat(ci, "{0}  w (rad/s)  : {1,7:F3} {2,7:F3} {3,7:F3}  |w|={4:F3}",
                            tag, w.x, w.y, w.z, w.magnitude);
    }
}