using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using UnityEngine;
using UnityEngine.InputSystem;

public class ROSInit : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/test_topic";
    public string from_python = "/from_python";

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<StringMsg>(topicName);
        ros.Subscribe<StringMsg>(from_python, ReceiveMessage);
        Debug.Log("Connecting to ROS...");
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StringMsg message = new StringMsg("Hello from Unity!");
            ros.Publish(topicName, message);
            Debug.Log("Message sent!");
        }
    }

    void ReceiveMessage(StringMsg message)
    {
        Debug.Log("Message received: " + message.data);
    }
}