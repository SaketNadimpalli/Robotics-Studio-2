using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

/// <summary>
/// Mode 3 (XR): spawns a virtual player coin when the IRL human drops one,
/// so the VR player can see the real board move reflected in the virtual board.
/// Subscribes to /connect4/detected_human_move (1-based column).
/// </summary>
public class IRLHumanCoinSpawner : MonoBehaviour
{
    [Header("Coin prefab — should be the same as the player coin prefab")]
    public GameObject coinPrefab;

    [Header("Material for IRL human coins — should differ from VR player colour")]
    public Material humanCoinMaterial;

    [Header("Column transforms — assign Column_1..Column_7 in order")]
    public Transform[] columns = new Transform[7];

    [Header("Spawn height above the column trigger")]
    public float spawnHeightOffset = 0.5f;

    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<Int32Msg>("/connect4/detected_human_move", OnIRLHumanMove);
        Debug.Log("IRLHumanCoinSpawner ready");
    }

    void OnIRLHumanMove(Int32Msg msg)
    {
        // Only mirror the move when it is actually the IRL human's turn
        // (IsPlayerTurn == true means it's the VR player's turn — reject)
        if (GameStateManager.IsPlayerTurn)
        {
            Debug.LogWarning("IRLHumanCoinSpawner: ignoring move — it is the VR player's turn");
            return;
        }

        if (GameStateManager.IsGameOver)
        {
            Debug.LogWarning("IRLHumanCoinSpawner: ignoring move — game is over");
            return;
        }

        int col = msg.data - 1; // convert 1-based to 0-based

        if (col < 0 || col >= columns.Length || columns[col] == null)
        {
            Debug.LogWarning($"IRLHumanCoinSpawner: invalid column {col}");
            return;
        }

        Vector3 spawnPos = columns[col].position + Vector3.up * spawnHeightOffset;
        GameObject coin = Instantiate(coinPrefab, spawnPos, coinPrefab.transform.rotation);

        // Not an AI coin — player coloured, but not grabbable (it mirrors a real move)
        if (humanCoinMaterial != null)
        {
            var renderer = coin.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material = humanCoinMaterial;
        }

        CoinSnap snap = coin.GetComponent<CoinSnap>();
        if (snap != null)
        {
            snap.isAICoin = false;
            snap.isIRLMirrorCoin = true; // prevents ColumnDetector from treating it as a VR player move
        }

        var grab = coin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        Rigidbody rb = coin.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Debug.Log($"IRLHumanCoinSpawner: spawned player coin in column {col}");
    }
}
