using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// UR3 pose recorder for Unity URDF Importer (ArticulationBody-based).
///
/// Setup:
///   1. Drop this on any GameObject in the scene.
///   2. Drag the 6 UR3 joints (shoulder_pan, shoulder_lift, elbow,
///      wrist_1, wrist_2, wrist_3) into the `joints` array in Inspector order.
///   3. Press Play.
///
/// Controls (Play mode):
///   - Number keys 1-9 ........ Recall saved pose into that slot
///   - Hold LeftShift + 1-9 ... Record current joint angles into that slot
///   - R ...................... Reset to all-zero pose (home)
///   - Arrow keys / Q,E ....... Manually nudge the currently selected joint
///   - Tab .................... Cycle which joint the arrows control
///   - S ...................... Save all poses to disk (JSON next to the project)
///   - L ...................... Load poses from disk
///
/// Workflow to record a pose:
///   - Press Tab to pick a joint, use Up/Down arrows (and Q/E for fine) to
///     rotate it. Repeat for each of the 6 joints until the arm looks right.
///   - Hold LeftShift and press a number key to save that pose into that slot.
///   - Now plain number key presses recall those poses with smooth motion.
/// </summary>
public class UR3PoseRecorder : MonoBehaviour
{
    [Header("Drag the 6 UR3 ArticulationBody joints here, in order")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("Motion")]
    [Tooltip("Degrees per second the arm moves toward a recalled pose.")]
    public float recallSpeedDegPerSec = 60f;

    [Tooltip("Degrees per key press when nudging a joint with arrows.")]
    public float coarseNudgeDeg = 5f;
    public float fineNudgeDeg = 1f;

    [Header("Persistence")]
    [Tooltip("File written next to the Unity project (or to persistentDataPath in builds).")]
    public string saveFileName = "ur3_poses.json";

    // 9 pose slots, each is 6 joint angles in degrees
    private float[][] poses = new float[9][];

    // Which joint the arrow keys currently nudge
    private int selectedJoint = 0;

    // Target pose currently being driven toward (null = no active recall)
    private float[] targetPose = null;

    void Start()
    {
        for (int i = 0; i < 9; i++) poses[i] = null;
        TryLoad(silent: true);
        Debug.Log("[UR3PoseRecorder] Ready. Tab to pick joint, arrows to nudge, Shift+1..9 to save, 1..9 to recall, S/L to save/load file.");
    }

    void Update()
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Save / recall slots 1..9
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (shift) RecordPose(i);
                else RecallPose(i);
            }
        }

        // Joint selection
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            selectedJoint = (selectedJoint + 1) % joints.Length;
            Debug.Log($"[UR3PoseRecorder] Selected joint {selectedJoint} ({joints[selectedJoint]?.name})");
        }

        // Nudge selected joint
        if (Input.GetKeyDown(KeyCode.UpArrow)) NudgeJoint(selectedJoint, +coarseNudgeDeg);
        if (Input.GetKeyDown(KeyCode.DownArrow)) NudgeJoint(selectedJoint, -coarseNudgeDeg);
        if (Input.GetKeyDown(KeyCode.E)) NudgeJoint(selectedJoint, +fineNudgeDeg);
        if (Input.GetKeyDown(KeyCode.Q)) NudgeJoint(selectedJoint, -fineNudgeDeg);

        // Reset to home (all zeros)
        if (Input.GetKeyDown(KeyCode.R))
        {
            targetPose = new float[joints.Length];
            Debug.Log("[UR3PoseRecorder] Reset to home pose.");
        }

        // File I/O
        if (Input.GetKeyDown(KeyCode.S)) Save();
        if (Input.GetKeyDown(KeyCode.L)) TryLoad(silent: false);

        // Drive toward target pose if one is active
        if (targetPose != null) StepTowardTarget();
    }

    // ---- Core ops ----

    void RecordPose(int slot)
    {
        var snapshot = new float[joints.Length];
        for (int i = 0; i < joints.Length; i++) snapshot[i] = GetJointDegrees(joints[i]);
        poses[slot] = snapshot;
        Debug.Log($"[UR3PoseRecorder] Saved pose {slot + 1}: [{string.Join(", ", snapshot)}]");
    }

    void RecallPose(int slot)
    {
        if (poses[slot] == null)
        {
            Debug.LogWarning($"[UR3PoseRecorder] Slot {slot + 1} is empty.");
            return;
        }
        targetPose = (float[])poses[slot].Clone();
        Debug.Log($"[UR3PoseRecorder] Recalling pose {slot + 1}");
    }

    void StepTowardTarget()
    {
        float maxStep = recallSpeedDegPerSec * Time.deltaTime;
        bool allReached = true;
        for (int i = 0; i < joints.Length; i++)
        {
            float current = GetJointDegrees(joints[i]);
            float diff = Mathf.DeltaAngle(current, targetPose[i]);
            if (Mathf.Abs(diff) > 0.1f)
            {
                allReached = false;
                float step = Mathf.Clamp(diff, -maxStep, +maxStep);
                SetJointDegrees(joints[i], current + step);
            }
        }
        if (allReached) targetPose = null;
    }

    void NudgeJoint(int idx, float deltaDeg)
    {
        // Cancel any active recall so manual nudges aren't fighting the driver
        targetPose = null;
        if (joints[idx] == null) return;
        float current = GetJointDegrees(joints[idx]);
        SetJointDegrees(joints[idx], current + deltaDeg);
    }

    // ---- ArticulationBody helpers ----

    static float GetJointDegrees(ArticulationBody body)
    {
        if (body == null) return 0f;
        // For revolute joints the X drive holds the angle (in degrees).
        return body.xDrive.target;
    }

    static void SetJointDegrees(ArticulationBody body, float degrees)
    {
        if (body == null) return;
        var drive = body.xDrive;
        drive.target = degrees;
        body.xDrive = drive;
    }

    // ---- Persistence ----

    [System.Serializable]
    private class PoseFile
    {
        public List<PoseEntry> poses = new List<PoseEntry>();
    }

    [System.Serializable]
    private class PoseEntry
    {
        public int slot;
        public float[] angles;
    }

    string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    void Save()
    {
        var file = new PoseFile();
        for (int i = 0; i < poses.Length; i++)
        {
            if (poses[i] != null) file.poses.Add(new PoseEntry { slot = i, angles = poses[i] });
        }
        File.WriteAllText(SavePath, JsonUtility.ToJson(file, true));
        Debug.Log($"[UR3PoseRecorder] Saved {file.poses.Count} poses to {SavePath}");
    }

    void TryLoad(bool silent)
    {
        if (!File.Exists(SavePath))
        {
            if (!silent) Debug.LogWarning($"[UR3PoseRecorder] No save file at {SavePath}");
            return;
        }
        var file = JsonUtility.FromJson<PoseFile>(File.ReadAllText(SavePath));
        foreach (var e in file.poses)
        {
            if (e.slot >= 0 && e.slot < poses.Length) poses[e.slot] = e.angles;
        }
        Debug.Log($"[UR3PoseRecorder] Loaded {file.poses.Count} poses from {SavePath}");
    }
}