using System.Collections;
using UnityEngine;

/// <summary>
/// UR3 Pose Controller — smoothly moves the UR3e arm through named joint-space
/// preset poses. Designed to coexist with URDF-Importer's Controller script
/// (arrow-key per-joint control): preset poses are triggered with number keys
/// 1-6, and the URDF Controller is temporarily disabled during a pose move so
/// the two don't fight over drive targets.
///
/// USAGE:
///  1. Attach this script to the "ur3e" GameObject (same one that has the
///     URDF-Importer "Controller" script).
///  2. In the Inspector:
///       - Leave "Joints" empty to auto-find by link name, OR drag each
///         ArticulationBody (shoulder_link, upper_arm_link, forearm_link,
///         wrist_1_link, wrist_2_link, wrist_3_link) into the slots manually.
///       - Drag the URDF Controller component into the "Urdf Controller"
///         slot so this script can disable it during a pose move.
///       - Edit the Presets array to add/tune poses. Values are in DEGREES.
///  3. Press Play. Click into the Game view to give it keyboard focus.
///       - Press 1 -> Home pose
///       - Press 2 -> Ready pose
///       - Arrow keys still work for per-joint fine-tuning (via URDF Controller)
///
/// Joint order (matches ROS UR convention):
///   [0] shoulder_pan   (shoulder_link)
///   [1] shoulder_lift  (upper_arm_link)
///   [2] elbow          (forearm_link)
///   [3] wrist_1        (wrist_1_link)
///   [4] wrist_2        (wrist_2_link)
///   [5] wrist_3        (wrist_3_link)
/// </summary>
public class UR3PoseController : MonoBehaviour
{
    [System.Serializable]
    public class JointPose
    {
        public string name = "Pose";
        [Tooltip("Six joint angles in DEGREES, in UR order: " +
                 "shoulder_pan, shoulder_lift, elbow, wrist_1, wrist_2, wrist_3")]
        public float[] jointAngles = new float[6];
    }

    [Header("Joint References")]
    [Tooltip("Leave empty to auto-find by link name on Start.")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("Preset Poses (degrees)")]
    public JointPose[] presets = new JointPose[]
    {
        new JointPose { name = "Home",  jointAngles = new float[] {   0f, -90f,   0f, -90f,   0f,   0f } },
        new JointPose { name = "Ready", jointAngles = new float[] {   0f, -60f,  60f, -90f, -90f,   0f } }
    };

    [Header("Motion")]
    [Tooltip("Seconds to transition between poses. Smaller = faster, jerkier.")]
    [Range(0.1f, 10f)]
    public float transitionDuration = 2f;

    [Header("Coexistence")]
    [Tooltip("Optional: the URDF-Importer Controller script on this GameObject. " +
             "Will be temporarily disabled while a pose move is running so arrow " +
             "keys don't fight the interpolation.")]
    public MonoBehaviour urdfController;

    // Standard UR link names (ROS-industrial convention)
    private static readonly string[] LinkNames =
    {
        "shoulder_link",
        "upper_arm_link",
        "forearm_link",
        "wrist_1_link",
        "wrist_2_link",
        "wrist_3_link"
    };

    private Coroutine activeMove;

    void Start()
    {
        if (JointsUnassigned())
            AutoFindJoints();
    }

    void Update()
    {
        // Number keys 1-6 trigger the first six presets
        if (Input.GetKeyDown(KeyCode.Alpha1)) GoToPose(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) GoToPose(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) GoToPose(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) GoToPose(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) GoToPose(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) GoToPose(5);
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC API — call these from other scripts or UI buttons
    // ─────────────────────────────────────────────────────────

    /// <summary>Move to the preset at the given index (0-based).</summary>
    public void GoToPose(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length)
        {
            // silent ignore — lets you safely bind unused number keys
            return;
        }
        StartPoseMove(presets[index]);
    }

    /// <summary>Move to a preset by name (case-sensitive).</summary>
    public void GoToPose(string poseName)
    {
        foreach (var p in presets)
        {
            if (p.name == poseName)
            {
                StartPoseMove(p);
                return;
            }
        }
        Debug.LogWarning($"[UR3PoseController] No preset named '{poseName}'");
    }

    /// <summary>Move to an arbitrary set of six joint angles (degrees).</summary>
    public void GoToAngles(float[] sixAngles)
    {
        if (sixAngles == null || sixAngles.Length != 6)
        {
            Debug.LogError("[UR3PoseController] GoToAngles requires exactly 6 values.");
            return;
        }
        StartPoseMove(new JointPose { name = "Custom", jointAngles = sixAngles });
    }

    // ─────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────

    void StartPoseMove(JointPose pose)
    {
        if (activeMove != null) StopCoroutine(activeMove);
        activeMove = StartCoroutine(InterpolateToPose(pose));
    }

    IEnumerator InterpolateToPose(JointPose pose)
    {
        // Pause the URDF Controller so arrow keys don't fight us
        bool controllerWasEnabled = false;
        if (urdfController != null)
        {
            controllerWasEnabled = urdfController.enabled;
            urdfController.enabled = false;
        }

        // Capture starting joint targets
        float[] startAngles = new float[6];
        for (int i = 0; i < 6; i++)
            startAngles[i] = joints[i] != null ? joints[i].xDrive.target : 0f;

        // Smoothly interpolate over transitionDuration seconds
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
            for (int i = 0; i < 6; i++)
            {
                if (joints[i] == null) continue;
                float angle = Mathf.Lerp(startAngles[i], pose.jointAngles[i], t);
                SetDriveTarget(joints[i], angle);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to final
        for (int i = 0; i < 6; i++)
        {
            if (joints[i] == null) continue;
            SetDriveTarget(joints[i], pose.jointAngles[i]);
        }

        Debug.Log($"[UR3PoseController] Reached pose: {pose.name}");

        // Resume URDF Controller
        if (urdfController != null && controllerWasEnabled)
            urdfController.enabled = true;

        activeMove = null;
    }

    static void SetDriveTarget(ArticulationBody joint, float targetDegrees)
    {
        var drive = joint.xDrive;
        drive.target = targetDegrees;
        joint.xDrive = drive;
    }

    bool JointsUnassigned()
    {
        if (joints == null || joints.Length < 6) return true;
        for (int i = 0; i < 6; i++)
            if (joints[i] == null) return true;
        return false;
    }

    void AutoFindJoints()
    {
        joints = new ArticulationBody[6];
        for (int i = 0; i < LinkNames.Length; i++)
        {
            Transform found = FindChildRecursive(transform, LinkNames[i]);
            if (found != null)
                joints[i] = found.GetComponent<ArticulationBody>();

            if (joints[i] == null)
            {
                Debug.LogWarning(
                    $"[UR3PoseController] Could not auto-find ArticulationBody for '{LinkNames[i]}'. " +
                    "Assign it manually in the Inspector.");
            }
        }
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindChildRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
