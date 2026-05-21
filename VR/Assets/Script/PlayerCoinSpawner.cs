using UnityEngine;

public class PlayerCoinSpawner : MonoBehaviour
{
    [Header("Coin prefab (the grabbable player coin)")]
    public GameObject coinPrefab;

    [Header("Tray slot transforms — create 10 empty GameObjects in the tray and drag them here")]
    public Transform[] spawnPoints = new Transform[10];

    void Start()
    {
        SpawnCoins();
    }

    public void SpawnCoins()
    {
        foreach (var point in spawnPoints)
        {
            if (point == null) continue;
            Instantiate(coinPrefab, point.position, point.rotation);
        }
    }
}
