using UnityEngine;

public class CoinSnap : MonoBehaviour
{
    private Rigidbody rb;
    public bool hasSnapped = false;

    [Header("Snap Settings")]
    public float snapDelay = 0.5f; // time after entering column before snapping
    public Vector3 snappedRotation = new Vector3(-89.98f, 0f, 0f); // match board rotation

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
        // let it fall naturally for a moment
        yield return new WaitForSeconds(snapDelay);

        // freeze it
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // snap to slot position and correct rotation
        transform.position = slot.position;
        transform.rotation = Quaternion.Euler(snappedRotation);

        // disable XR grab so it cant be picked up again
        var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
    }
}