using UnityEngine;

/// <summary>
/// Sets the virtual UR3 base position/orientation to match the real-world
/// physical offset. Measure these values from your real setup.
/// </summary>
public class RobotBaseAnchor : MonoBehaviour
{
    [Header("Measure these from your real setup (in metres, ROS convention)")]
    [Tooltip("ROS X = forward, Y = left, Z = up, relative to Connect4 board origin")]
    public Vector3 rosOffsetMetres = new Vector3(0f, -0.5f, 0f);

    [Tooltip("ROS rotation around Z-up axis, in degrees")]
    public float rosYawDegrees = 0f;

    [Header("Your virtual Connect4 board's Transform")]
    public Transform connect4BoardTransform;

    void Start()
    {
        ApplyAnchor();
    }

    [ContextMenu("Re-apply Anchor")]   // lets you click this in Editor too
    public void ApplyAnchor()
    {
        Transform reference = connect4BoardTransform != null
            ? connect4BoardTransform
            : null;

        // Convert ROS offset to Unity: swap Y and Z
        Vector3 unityOffset = new Vector3(
            rosOffsetMetres.x,
            rosOffsetMetres.z,   // ROS Z (up) → Unity Y (up)
            rosOffsetMetres.y    // ROS Y (left) → Unity Z (forward)
        );

        if (reference != null)
        {
            transform.position = reference.position + reference.TransformDirection(unityOffset);
        }
        else
        {
            transform.position = unityOffset;
        }

        // ROS yaw around Z-up → Unity rotation around Y, negated (handedness flip)
        transform.rotation = Quaternion.Euler(0f, -rosYawDegrees, 0f);

        Debug.Log($"[RobotBaseAnchor] Applied. Unity pos={transform.position}, yaw={-rosYawDegrees}°");
    }
}