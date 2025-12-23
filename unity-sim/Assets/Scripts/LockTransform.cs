using UnityEngine;

public class LockTransform : MonoBehaviour
{
    [SerializeField] private Transform _bind;
    [SerializeField] private Vector3 _offset;
    private void Update()
    {
        transform.SetPositionAndRotation(_bind.position + _offset, _bind.rotation);
    }
}