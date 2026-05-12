// UR3TeleopDriver.cs
// -----------------------------------------------------------------------------
// Reads the right hand pose from HandTracker, converts it into robot-base
// frame, calls the analytical IK solver, and drives the ArticulationBody
// joints — exactly the same joints used by UR3JointStateMirror.
//
// Mode switching:
//   Call SetTeleopActive(true)  to hand control to this driver.
//   Call SetTeleopActive(false) to hand control back to UR3JointStateMirror.
//   Only one driver writes joints at a time — the other silently skips.
//
// Stage B note:
//   The solved joint angles (in radians) are exposed via SolvedAnglesRadians.
//   Publishing them to ROS is a one-liner addition in a separate script —
//   nothing in this file needs to change for Stage B.
// -----------------------------------------------------------------------------
using UnityEngine;

[DisallowMultipleComponent]
public class UR3TeleopDriver : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------
    [Header("References")]
    [Tooltip("The HandTracker on your right hand")]
    public HandTracker rightHandTracker;

    [Tooltip("The RobotBase GameObject (has RobotBaseAnchor script on it)")]
    public Transform robotBaseTransform;

    [Tooltip("Same 6 joints as UR3JointStateMirror — drag in same order")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Tooltip("Wire up UR3JointStateMirror so we can toggle it off/on")]
    public UR3JointStateMirror jointStateMirror;

    [Header("Pose smoothing")]
    [Tooltip("Exponential smoothing on the hand pose before IK. " +
             "0 = raw, 0.85 = heavy. Start at 0.7 and tune.")]
    [Range(0f, 0.95f)]
    public float positionSmoothing = 0.7f;
    [Range(0f, 0.95f)]
    public float rotationSmoothing = 0.7f;

    [Header("Joint drive")]
    [Tooltip("Max degrees per second any joint is allowed to move. " +
             "Caps sudden IK jumps. 90 deg/s is a safe starting point.")]
    public float maxJointSpeedDegPerSec = 90f;

    [Header("Debug")]
    public bool logSolverResults = false;

    // -------------------------------------------------------------------------
    // Public state (read by FistGestureDetector and Stage B publisher)
    // -------------------------------------------------------------------------
    public bool IsTeleopActive { get; private set; } = false;

    /// <summary>
    /// Last valid solved joint angles in RADIANS.
    /// Stage B: publish these directly on your joint command topic.
    /// </summary>
    public float[] SolvedAnglesRadians { get; private set; } = new float[6];

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    // Smoothed hand pose in robot-base frame
    Vector3 _smoothedPos;
    Quaternion _smoothedRot = Quaternion.identity;

    // Current joint angles in radians — used by solver for branch selection
    float[] _currentRadians = new float[6];

    // Whether smoothed pose has been initialised (avoids snap on first frame)
    bool _poseInitialised = false;

    // -------------------------------------------------------------------------
    // Unity messages
    // -------------------------------------------------------------------------
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            SetTeleopActive(!IsTeleopActive);

        if (!IsTeleopActive) return;
        if (rightHandTracker == null || !rightHandTracker.IsTracked) return;

        Vector3 handWorld = rightHandTracker.Position;
        Vector3 baseWorldPos = joints[0].transform.position;
        Vector3 handLocalPos = handWorld - baseWorldPos;

        Vector3 robotPos = new Vector3(
             handLocalPos.z,
            -handLocalPos.x,
             handLocalPos.y
        );

        Quaternion robotRot = Quaternion.identity;

        if (!_poseInitialised)
        {
            _smoothedPos = robotPos;
            _smoothedRot = robotRot;
            _poseInitialised = true;
        }
        else
        {
            _smoothedPos = Vector3.Lerp(_smoothedPos, robotPos, 1f - positionSmoothing);
            _smoothedRot = Quaternion.Slerp(_smoothedRot, robotRot, 1f - rotationSmoothing);
        }

        ReadCurrentAngles();

        bool solved = UR3eIKSolver.Solve(
            _smoothedPos,
            _smoothedRot,
            _currentRadians,
            out float[] solvedRadians);

        if (!solved) return;

        SolvedAnglesRadians = solvedRadians;
        ApplyJointAngles(solvedRadians);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Arm or disarm teleop mode.
    /// Called by FistGestureDetector.
    /// </summary>
    public void SetTeleopActive(bool active)
    {
        if (active == IsTeleopActive) return;

        IsTeleopActive = active;

        // Let UR3JointStateMirror know — it will skip its Update() while
        // teleop is active so both scripts don't fight over the joints
        if (jointStateMirror != null)
            jointStateMirror.SetMirrorActive(!active);

        // Reset smoothing so we don't snap from wherever _smoothedPos was
        _poseInitialised = false;

        Debug.Log($"[TeleopDriver] Teleop {(active ? "ARMED" : "DISARMED")}");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Read current xDrive.target (degrees) from each joint and convert
    /// to radians for the IK solver's branch-selection logic.
    /// </summary>
    void ReadCurrentAngles()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;
            float deg = joints[i].xDrive.target;
            // NO un-flip needed
            _currentRadians[i] = deg * Mathf.Deg2Rad;
        }
    }

    /// <summary>
    /// Write solved joint angles to ArticulationBody xDrive targets.
    /// Applies the same axis flips as UR3JointStateMirror.ROSToUnityAngle.
    /// Rate-limits each joint to maxJointSpeedDegPerSec.
    /// </summary>
    void ApplyJointAngles(float[] radians)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;

            float targetDeg = Rad2Deg(radians[i]);
            // NO sign flips — xDrive takes direct degrees

            var drive = joints[i].xDrive;
            float current = drive.target;
            float maxStep = maxJointSpeedDegPerSec * Time.deltaTime;
            float diff = Mathf.DeltaAngle(current, targetDeg);
            float step = Mathf.Clamp(diff, -maxStep, maxStep);

            drive.target = current + step;
            joints[i].xDrive = drive;
        }
    }

    static float Rad2Deg(float r) => r * Mathf.Rad2Deg;
}