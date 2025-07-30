using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Reflects the point charge back inside the sphere
// Without this component, the charge can easily slip through the sphere screen and escape containment
public class PointLeash : MonoBehaviour
{
    public GameObject point;
    private Rigidbody pointrb;
    private PresetControl preset;
    private float sphereRadius;

    void Start()
    {
        preset = point.GetComponent<PresetControl>();
        pointrb = point.GetComponent<Rigidbody>();
        Transform invertedSphere = transform.Find("InvertedSphere");

        if (invertedSphere != null)
        {
            MeshCollider invsphMesh = invertedSphere.gameObject.GetComponent<MeshCollider>();
            sphereRadius = invsphMesh.bounds.extents.x - 0.05f;
            invsphMesh.enabled = false;
        }
        else
        {
            Debug.LogWarning("InvertedSphere not found, disabling point leash");
            enabled = false;
        }
    }

    void Update()
    {
        // If the charge is outside the sphere and is moving away from the sphere, reflect it back.
        if (!preset.enabled && point.transform.localPosition.magnitude > sphereRadius && Vector3.Dot(pointrb.velocity, point.transform.localPosition) >= 0)
        {
            pointrb.velocity = Vector3.Reflect(pointrb.velocity, -point.transform.localPosition.normalized);
        }
    }
}
