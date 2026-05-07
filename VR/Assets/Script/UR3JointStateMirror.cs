using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

/// <summary>
/// Subscribes to /joint_states and drives the UR3e ArticulationBody chain
/// to mirror the real arm in real-time.
/// </summary>
public class UR3JointStateMirror : MonoBehaviour
{
    [Header("ArticulationBody joints — drag in Inspector order:")]
    [Tooltip("shoulder_pan, shoulder_lift, elbow, wrist_1, wrist_2, wrist_3")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("ROS")]
    public string jointStatesTopic = "/joint_states";

    [Header("Smoothing")]
    [Tooltip("Higher = snappier but more jitter. Lower = smoother but more lag.")]
    [Range(1f, 30f)]
    public float smoothingSpeed = 10f;

    // The UR3e joint names as published by ROS (order may vary in message!)
    private static readonly string[] UR3JointNames = {
        "shoulder_pan_joint",
        "shoulder_lift_joint",
        "elbow_joint",
        "wrist_1_joint",
        "wrist_2_joint",
        "wrist_3_joint"
    };

    // Latest angles received from ROS (degrees), indexed to match joints[]
    private float[] targetDegrees = new float[6];
    private bool hasReceivedData = false;

    void Start()
    {
        // Initialize targets to current joint positions
        for (int i = 0; i < joints.Length; i++)
            targetDegrees[i] = joints[i] != null ? joints[i].xDrive.target : 0f;

        ROSConnection.GetOrCreateInstance()
            .Subscribe<JointStateMsg>(jointStatesTopic, OnJointState);

        Debug.Log("[UR3Mirror] Subscribed to " + jointStatesTopic);
    }

    void OnJointState(JointStateMsg msg)
    {
        // msg.name[] and msg.position[] are parallel arrays.
        // We must look up by NAME because ROS doesn't guarantee order.
        var nameToIndex = new Dictionary<string, int>();
        for (int i = 0; i < msg.name.Length; i++)
            nameToIndex[msg.name[i]] = i;

        for (int j = 0; j < UR3JointNames.Length; j++)
        {
            if (nameToIndex.TryGetValue(UR3JointNames[j], out int rosIdx))
            {
                // ROS uses radians — convert to degrees for ArticulationBody
                float rosRad = (float)msg.position[rosIdx];
                targetDegrees[j] = ROSToUnityAngle(j, rosRad);
            }
        }
        hasReceivedData = true;
    }

    void Update()
    {
        if (!hasReceivedData) return;

        float maxStep = smoothingSpeed * Time.deltaTime;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;

            var drive = joints[i].xDrive;
            float current = drive.target;
            float diff = Mathf.DeltaAngle(current, targetDegrees[i]);
            float step = Mathf.Clamp(diff, -maxStep * Mathf.Rad2Deg,
                                          +maxStep * Mathf.Rad2Deg);
            drive.target = current + step;
            joints[i].xDrive = drive;
        }
    }

    /// <summary>
    /// Converts a ROS joint angle (radians, right-hand) to Unity xDrive
    /// degrees for the given joint index. See section 3 for the full
    /// explanation of what flips and why.
    /// </summary>
    float ROSToUnityAngle(int jointIndex, float rosRad)
    {
        float deg = rosRad * Mathf.Rad2Deg;

        // Joints 1 and 3 (shoulder_lift, wrist_1) need sign flip because
        // the URDF Importer mirrors those axes into Unity's left-handed frame.
        // Verify these experimentally with your specific import — see section 3.
        switch (jointIndex)
        {
            case 1: // shoulder_lift
            case 3: // wrist_1
                return -deg;
            default:
                return deg;
        }
    }
}