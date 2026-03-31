using UnityEngine;
using System.Collections;

public class RepairPickup : MonoBehaviour
{
    public float rotationSpeed = 90f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.5f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Visual Juice: Rotate and Bob
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);
        
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void OnTriggerEnter(Collider other)
    {
        // Detect player
        SuperminiCarController car = other.GetComponentInParent<SuperminiCarController>();
        if (car != null)
        {
            car.HealToFull();
            Debug.Log("[PICKUP] Wrench collected! Health restored.");
            
            // Destroy the pickup
            Destroy(gameObject);
        }
    }
}
