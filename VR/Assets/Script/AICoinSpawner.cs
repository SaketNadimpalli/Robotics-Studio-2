using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class AICoinSpawner : MonoBehaviour
{
    [Header("Coin prefab to spawn for AI moves")]
    public GameObject coinPrefab;

    [Header("Optional: material for AI coins (overrides prefab material)")]
    public Material aiCoinMaterial;

    [Header("Column transforms � assign Column_1..Column_7 in order")]
    public Transform[] columns = new Transform[7];

    [Header("Spawn height above the column trigger")]
    public float spawnHeightOffset = 0.5f;

    [Header("ROS")]
    public string aiMoveTopic = "/connect4/robot_move";
    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<Int32Msg>(aiMoveTopic, OnAIMove);
        Debug.Log("AICoinSpawner ready, listening on " + aiMoveTopic);
    }

    void OnAIMove(Int32Msg msg)
    {
        int col = msg.data - 1;
        if (col < 0 || col >= columns.Length || columns[col] == null)
        {
            Debug.LogWarning($"Invalid AI column {col}");
            return;
        }

        Vector3 spawnPos = columns[col].position + Vector3.up * spawnHeightOffset;
        GameObject aiCoin = Instantiate(coinPrefab, spawnPos, coinPrefab.transform.rotation);

        if (aiCoinMaterial != null)
        {
            var renderer = aiCoin.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material = aiCoinMaterial;
        }

        // AI coins shouldn't be grabbable (and disabling XR grab keeps it from hijacking kinematic state)
        var grab = aiCoin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        // Force gravity to actually work on this coin
        Rigidbody rb = aiCoin.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        CoinSnap snap = aiCoin.GetComponent<CoinSnap>();
        if (snap != null) snap.isAICoin = true;

        Debug.Log($"Spawned AI coin in column {col}");
    }
}