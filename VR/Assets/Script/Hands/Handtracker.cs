// HandTracker.cs
// -----------------------------------------------------------------------------
// Tracks one hand (left OR right) via Unity's XR Hands package and exposes:
//   - Position        (world-space, metres)
//   - Rotation        (world-space quaternion)
//   - LinearVelocity  (world-space, m/s)
//   - AngularVelocity (world-space, rad/s)
//
// These four quantities are exactly what a ROS geometry_msgs/PoseStamped +
// TwistStamped (or a custom msg combining both) needs, so when you wire in
// ROS-TCP-Connector later you can publish straight from these properties.
//
// Requirements:
//   * Package Manager -> "XR Hands" (com.unity.xr.hands)
//   * Your XR plugin (OpenXR / Oculus) must have its Hand Tracking feature ON
//   * Put this script on (or under) your XR Origin so we can convert joint
//     poses from tracking space into world space correctly.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

[DisallowMultipleComponent]
public class HandTracker : MonoBehaviour
{
    public enum Handedness { Left, Right }

    [Header("Which hand")]
    public Handedness hand = Handedness.Right;

    [Tooltip("Joint used as the 'hand point'. Palm is usually what you want " +
             "for commanding a robot TCP; Wrist is more stable but offset.")]
    public XRHandJointID joint = XRHandJointID.Palm;

    [Header("XR Origin (leave empty to use this transform)")]
    [Tooltip("XR Hands returns joint poses in XR-Origin tracking space. We " +
             "multiply by this transform to get world-space poses.")]
    public Transform xrOrigin;

    [Header("Velocity smoothing (0 = raw, ~0.2 gentle, ~0.5 heavy)")]
    [Range(0f, 0.95f)] public float velocitySmoothing = 0.2f;

    // -------- Read-only outputs, refreshed every frame the hand is tracked --
    public bool IsTracked { get; private set; }
    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }
    public Vector3 LinearVelocity { get; private set; }
    public Vector3 AngularVelocity { get; private set; }

    // -------- Internals -----------------------------------------------------
    XRHandSubsystem _subsystem;
    bool _havePrev;
    Vector3 _prevPos;
    Quaternion _prevRot;

    void OnEnable()
    {
        if (xrOrigin == null) xrOrigin = transform;

        // Grab the first running XR Hand subsystem (there is usually only one).
        var subs = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count == 0)
        {
            Debug.LogWarning("[HandTracker] No XRHandSubsystem found. Is the " +
                             "XR Hands package installed and hand tracking " +
                             "enabled in your XR plugin?");
            return;
        }

        _subsystem = subs[0];
        _subsystem.updatedHands += OnHandsUpdated;
    }

    void OnDisable()
    {
        if (_subsystem != null) _subsystem.updatedHands -= OnHandsUpdated;
        _subsystem = null;
        _havePrev = false;
        IsTracked = false;
    }

    void OnHandsUpdated(XRHandSubsystem subsystem,
                        XRHandSubsystem.UpdateSuccessFlags flags,
                        XRHandSubsystem.UpdateType updateType)
    {
        // Only use the Dynamic update so dt == Time.deltaTime (one update per
        // render frame). BeforeRender updates would double-count dt.
        if (updateType != XRHandSubsystem.UpdateType.Dynamic) return;

        XRHand xrHand = (hand == Handedness.Left) ? subsystem.leftHand
                                                  : subsystem.rightHand;

        if (!xrHand.isTracked ||
            !xrHand.GetJoint(joint).TryGetPose(out Pose poseLocal))
        {
            IsTracked = false;
            _havePrev = false;    // reset so we don't produce a bogus spike
            return;                // when tracking resumes next frame
        }

        // --- Convert joint pose: XR-Origin tracking space -> world space ----
        Vector3 worldPos = xrOrigin.TransformPoint(poseLocal.position);
        Quaternion worldRot = xrOrigin.rotation * poseLocal.rotation;

        float dt = Time.deltaTime;
        if (_havePrev && dt > 0f)
        {
            // ---------------- Linear velocity -------------------------------
            //   v = (p_now - p_prev) / dt                                m/s
            Vector3 v = (worldPos - _prevPos) / dt;

            // ---------------- Angular velocity ------------------------------
            // 1) Quaternion delta that rotates the PREVIOUS orientation
            //    onto the CURRENT one:
            //        q_delta = q_now * inverse(q_prev)
            // 2) Express that delta as (axis, angle). The axis is a unit
            //    vector in world space, angle is in degrees.
            // 3) Unwrap to the shortest arc (Unity returns 0..360).
            // 4) omega = axis * (angle_rad / dt)                      rad/s
            Quaternion dq = worldRot * Quaternion.Inverse(_prevRot);
            dq.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;          // shortest arc

            Vector3 w = (axis.sqrMagnitude > 1e-8f)
                      ? axis.normalized * (angleDeg * Mathf.Deg2Rad / dt)
                      : Vector3.zero;

            // ---------------- Exponential smoothing -------------------------
            // new = prev * s + raw * (1-s).  Zero smoothing -> raw finite diff.
            float a = 1f - velocitySmoothing;
            LinearVelocity = Vector3.Lerp(LinearVelocity, v, a);
            AngularVelocity = Vector3.Lerp(AngularVelocity, w, a);
        }
        else
        {
            LinearVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
        }

        Position = worldPos;
        Rotation = worldRot;
        _prevPos = worldPos;
        _prevRot = worldRot;
        _havePrev = true;
        IsTracked = true;
    }
}