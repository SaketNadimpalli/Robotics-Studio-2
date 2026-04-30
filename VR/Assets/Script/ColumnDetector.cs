using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class ColumnDetector : MonoBehaviour
{
    [Header("Row Slots - assign Row_1 to Row_6 in order bottom to top")]
    public Transform[] rows = new Transform[6];

    [Header("Column index 0..6 — set per column in Inspector")]
    public int columnIndex = 0;

    private bool[] occupiedRows = new bool[6];

    [Header("ROS")]
    public string playerMoveTopic = "/connect4/player_move";
    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<Int32Msg>(playerMoveTopic);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Coin")) return;
        CoinSnap coin = other.GetComponent<CoinSnap>();
        if (coin == null || coin.hasSnapped) return;

        for (int i = 0; i < rows.Length; i++)
        {
            if (!occupiedRows[i])
            {
                occupiedRows[i] = true;
                coin.SnapToSlot(rows[i]);

                // Only publish player moves for human-thrown coins
                if (!coin.isAICoin)
                {
                    Int32Msg msg = new Int32Msg(columnIndex);
                    ros.Publish(playerMoveTopic, msg);
                    Debug.Log($"Published player move: column {columnIndex}");
                }
                return;
            }
        }

        Debug.Log("Column full!");
    }

    public void ResetState()
    {
        for (int i = 0; i < occupiedRows.Length; i++)
            occupiedRows[i] = false;
    }
}