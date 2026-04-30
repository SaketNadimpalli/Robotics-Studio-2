using UnityEngine;

public class CoinSnap : MonoBehaviour
{
    private Rigidbody rb;
    public bool hasSnapped = false;
    public bool isAICoin = false;   // NEW

    [Header("Snap Settings")]
    public float snapDelay = 0.5f;
    public Vector3 snappedRotation = new Vector3(-89.98f, 0f, 0f);

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SnapToSlot(Transform slot)
    {
        if (hasSnapped) return;
        hasSnapped = true;
        StartCoroutine(DelayedSnap(slot));
    }

    System.Collections.IEnumerator DelayedSnap(Transform slot)
    {
        yield return new WaitForSeconds(snapDelay);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        transform.position = slot.position;
        transform.rotation = Quaternion.Euler(snappedRotation);
        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
    }
}