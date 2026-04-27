using UnityEngine;

public class ColumnDetector : MonoBehaviour
{
    [Header("Row Slots - assign Row_1 to Row_6 in order bottom to top")]
    public Transform[] rows = new Transform[6];

    private bool[] occupiedRows = new bool[6];

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Coin")) return;

        CoinSnap coin = other.GetComponent<CoinSnap>();
        if (coin == null || coin.hasSnapped) return;

        // find lowest empty row
        for (int i = 0; i < rows.Length; i++)
        {
            if (!occupiedRows[i])
            {
                occupiedRows[i] = true;
                coin.SnapToSlot(rows[i]);
                return;
            }
        }

        // column is full - do nothing, coin bounces off
        Debug.Log("Column full!");
    }
}