using UnityEngine;

/// <summary>
/// Locks a gameObject's position and rotation to another transform.
/// Helpful for objects that rely on being attached to the rover without
/// specifically changing the rovers joints.
/// </summary>
public class LockTransform : MonoBehaviour
{
    [SerializeField] private Transform _bind;
    
    // Able to offset from the exact position of the bound transform
    [SerializeField] private Vector3 _offset;
    private void Update()
    {
        transform.SetPositionAndRotation(_bind.position + _offset, _bind.rotation);
    }
}