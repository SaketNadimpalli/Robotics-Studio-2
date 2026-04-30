using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class AICoinSpawner : MonoBehaviour
{
    [Header("Coin prefab to spawn for AI moves")]
    public GameObject coinPrefab;

    [Header("Column transforms — assign Column_1..Column_7 in order")]
    public Transform[] columns = new Transform[7];

    [Header("Spawn height above the column trigger")]
    public float spawnHeightOffset = 0.5f;

    [Header("ROS")]
    public string aiMoveTopic = "/connect4/ai_move";
    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<Int32Msg>(aiMoveTopic, OnAIMove);
        Debug.Log("AICoinSpawner ready, listening on " + aiMoveTopic);
    }

    void OnAIMove(Int32Msg msg)
    {
        int col = msg.data;
        if (col < 0 || col >= columns.Length || columns[col] == null)
        {
            Debug.LogWarning($"Invalid AI column {col}");
            return;
        }

        Vector3 spawnPos = columns[col].position + Vector3.up * spawnHeightOffset;
        GameObject aiCoin = Instantiate(coinPrefab, spawnPos, coinPrefab.transform.rotation);

        CoinSnap snap = aiCoin.GetComponent<CoinSnap>();
        if (snap != null) snap.isAICoin = true;

        Debug.Log($"Spawned AI coin in column {col}");
    }
}