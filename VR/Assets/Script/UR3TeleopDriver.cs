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

    [Header("Workspace offset (metres, robot frame)")]
    [Tooltip("Shifts the mapped hand position in robot space. " +
             "Increase X to push the workspace forward (away from base). " +
             "Start with X=0.4 and tune until hand-at-rest maps to centre of robot reach.")]
    public Vector3 workspaceOffset = new Vector3(0.4f, 0f, 0f);

    [Header("Joint drive")]
    [Tooltip("Max degrees per second any joint is allowed to move. " +
             "Caps sudden IK jumps. 90 deg/s is a safe starting point.")]
    public float maxJointSpeedDegPerSec = 90f;

    [Header("Debug")]
    public bool logSolverResults = false;
    [Tooltip("Drag your end effector / tool0 transform here for debug logging")]
    public Transform endEffector;

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

        // S key snapshot works always — even without VR or teleop active
        if (Input.GetKeyDown(KeyCode.S))
        {
            ReadCurrentAngles();
            Debug.Log($"[Teleop S] Current joints (deg): " +
                      $"[{_currentRadians[0]*Mathf.Rad2Deg:F1}, {_currentRadians[1]*Mathf.Rad2Deg:F1}, " +
                      $"{_currentRadians[2]*Mathf.Rad2Deg:F1}, {_currentRadians[3]*Mathf.Rad2Deg:F1}, " +
                      $"{_currentRadians[4]*Mathf.Rad2Deg:F1}, {_currentRadians[5]*Mathf.Rad2Deg:F1}]");
            if (endEffector != null)
            {
                // Convert end effector world pos into robot base local frame then remap to ROS axes
                Vector3 eeLocal = Quaternion.Inverse(robotBaseTransform.rotation)
                                  * (endEffector.position - robotBaseTransform.position);
                Vector3 eeRobot = new Vector3(eeLocal.z, -eeLocal.x, eeLocal.y);
                Debug.Log($"[Teleop S] End effector world pos: X={endEffector.position.x:F3} Y={endEffector.position.y:F3} Z={endEffector.position.z:F3}");
                Debug.Log($"[Teleop S] End effector robot frame: X={eeRobot.x:F3} Y={eeRobot.y:F3} Z={eeRobot.z:F3}");
                Debug.Log($"[Teleop S] End effector world rotation (euler): {endEffector.rotation.eulerAngles}");
                // Rotation relative to robot base
                Quaternion eeLocalRot = Quaternion.Inverse(robotBaseTransform.rotation) * endEffector.rotation;
                Debug.Log($"[Teleop S] End effector local rotation (euler): {eeLocalRot.eulerAngles}");
                // Convert local Unity rotation to ROS frame using same axis remap as position
                Vector3 eeFwd = eeLocalRot * Vector3.forward;
                Vector3 eeUp  = eeLocalRot * Vector3.up;
                Vector3 rosFwd2 = new Vector3( eeFwd.z, -eeFwd.x,  eeFwd.y);
                Vector3 rosUp2  = new Vector3(  eeUp.z,  -eeUp.x,   eeUp.y);
                Quaternion rosRot = Quaternion.LookRotation(rosFwd2, rosUp2);
                Debug.Log($"[Teleop S] ROS target rotation (euler): {rosRot.eulerAngles}");
                Debug.Log($"[Teleop S] ROS target rotation (xyzw): {rosRot.x:F4},{rosRot.y:F4},{rosRot.z:F4},{rosRot.w:F4}");
            }
            if (rightHandTracker != null && rightHandTracker.IsTracked)
                Debug.Log($"[Teleop S] Hand world pos: X={rightHandTracker.Position.x:F3} Y={rightHandTracker.Position.y:F3} Z={rightHandTracker.Position.z:F3}");
            else
                Debug.Log("[Teleop S] Hand not tracked");
        }

        if (!IsTeleopActive) return;
        if (rightHandTracker == null || !rightHandTracker.IsTracked) return;

        Vector3 handWorld = rightHandTracker.Position;

        // Rotate the world-space offset into the robot base's local frame so the
        // axis swap below is applied in the right reference frame regardless of
        // how the base is yawed in the scene.  InverseTransformPoint would also
        // divide by scale — use the rotation-only form to stay scale-safe.
        Vector3 handLocalPos = Quaternion.Inverse(robotBaseTransform.rotation)
                               * (handWorld - robotBaseTransform.position);

        // Unity local axes  →  ROS robot-base axes
        //   Unity local X (right)   → ROS Y (left, negated)
        //   Unity local Y (up)      → ROS Z (up)
        //   Unity local Z (forward) → ROS X (forward)
        Vector3 robotPos = new Vector3(
             handLocalPos.z,
            -handLocalPos.x,
             handLocalPos.y
        ) + workspaceOffset;

        // Fixed end effector orientation derived from FK of column 1 hover pose.
        // Keeps the arm in the correct downward-pointing configuration for Connect4.
        Quaternion robotRot = new Quaternion(-0.5816f, 0.4113f, -0.4181f, 0.5637f);

        if (Input.GetKeyDown(KeyCode.S))
        {
            // 1. Raw hand world position
            Debug.Log($"[Teleop S] 1. Hand world pos: X={handWorld.x:F3} Y={handWorld.y:F3} Z={handWorld.z:F3}");

            // 1b. Hand relative to robot base (before axis swap)
            Debug.Log($"[Teleop S] 1b. Hand relative to base (local): X={handLocalPos.x:F3} Y={handLocalPos.y:F3} Z={handLocalPos.z:F3}");

            // 2. Robot frame position (IK input)
            Debug.Log($"[Teleop S] 2. IK target (robot frame): X={robotPos.x:F3} Y={robotPos.y:F3} Z={robotPos.z:F3}");

            // 3. Solved joint angles (logged after solve below)
        }

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

        if (Input.GetKeyDown(KeyCode.S))
        {
            // 3. IK output joint angles
            Debug.Log($"[Teleop S] 3. Solved joints (rad): " +
                      $"[{solvedRadians[0]:F3}, {solvedRadians[1]:F3}, {solvedRadians[2]:F3}, " +
                      $"{solvedRadians[3]:F3}, {solvedRadians[4]:F3}, {solvedRadians[5]:F3}]");
            Debug.Log($"[Teleop S] 3. Solved joints (deg): " +
                      $"[{solvedRadians[0]*Mathf.Rad2Deg:F1}, {solvedRadians[1]*Mathf.Rad2Deg:F1}, " +
                      $"{solvedRadians[2]*Mathf.Rad2Deg:F1}, {solvedRadians[3]*Mathf.Rad2Deg:F1}, " +
                      $"{solvedRadians[4]*Mathf.Rad2Deg:F1}, {solvedRadians[5]*Mathf.Rad2Deg:F1}]");

            // 4. End effector world position
            if (endEffector != null)
                Debug.Log($"[Teleop S] 4. End effector world pos: X={endEffector.position.x:F3} Y={endEffector.position.y:F3} Z={endEffector.position.z:F3}");
            else
                Debug.Log("[Teleop S] 4. End effector not assigned — drag tool0 into the End Effector field");
        }
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