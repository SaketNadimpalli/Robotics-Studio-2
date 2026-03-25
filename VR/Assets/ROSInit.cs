
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using UnityEngine;
using UnityEngine.InputSystem;

public class ROSInit : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/test_topic";

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<StringMsg>(topicName);
        Debug.Log("Connecting to ROS...");
    }

    void Update()
    {
        // New Input System version of Space key
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StringMsg message = new StringMsg("Hello from Unity!");
            ros.Publish(topicName, message);
            Debug.Log("Message sent!");
        }
    }
}
