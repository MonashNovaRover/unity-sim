using UnityEngine;

/// <summary>
/// Keyboard camera movement for easier debugging at
/// runtime.
/// </summary>
public class KeyboardCameraMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    public float verticalLookLimit = 85f;

    [Header("Options")]
    public bool lockCursor = true;

    float yaw;
    float pitch;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
        if (lockCursor) Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Cursor lock toggle
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
        }

        // Mouse look
        Vector2 mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        yaw += mouse.x * mouseSensitivity;
        pitch -= mouse.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -verticalLookLimit, verticalLookLimit);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Movement (local)
        float forward = Input.GetAxis("Vertical");   // W/S
        float strafe = Input.GetAxis("Horizontal");  // A/D
        float vertical = 0f;
        if (Input.GetKey(KeyCode.Space)) vertical += 1f;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) vertical -= 1f;

        Vector3 input = new Vector3(strafe, vertical, forward);
        if (input.sqrMagnitude > 1f) input.Normalize();

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        Vector3 movement = transform.TransformDirection(input) * speed * Time.deltaTime;
        transform.position += movement;
    }
}