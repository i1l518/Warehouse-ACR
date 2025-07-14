using UnityEngine;

public class MeasureObjectSize3DCollider : MonoBehaviour
{
    void Start()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            Vector3 size = bounds.size;
            string objectName = gameObject.name; // ¶Ç´Â transform.name

            Debug.Log("[" + objectName + "] Object width: " + size.x + ", height: " + size.y + ", depth: " + size.z);
        }
        else
        {
            Debug.LogWarning("Collider not found on object: " + gameObject.name);
        }
    }
}
