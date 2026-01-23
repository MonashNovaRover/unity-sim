using UnityEngine;

public class DiffbarController : MonoBehaviour
{
    // Transform to measure rotation relative to 
    // Should be legs (will change later to be angle w.r.t both legs)
    public Transform referenceTransform;
    
    // Proportional gain (correction)
    [Range(0f, 10000f)]
    public float stiffness = 1000f;
    
    // Derivative gain (damping)
    [Range(0f, 1000f)]
    public float damping = 100f;
    
    [Range(0f, 1000f)]
    public float maxTorque = 400f;
    
    // Control Axis
    public RotationAxis stabilizeAxis = RotationAxis.Pitch;
    
    private ArticulationBody ab;
    private float previousError = 0f;
    
    public enum RotationAxis { Pitch, Roll, Yaw }
    
    void Start()
    {
        ab = GetComponent<ArticulationBody>();
        
        if (referenceTransform == null)
        {
            Debug.LogWarning($"Reference transform not set on {gameObject.name}. Using parent transform.");
            referenceTransform = transform.parent;
        }
    }
    
    void FixedUpdate()
    {
        if (referenceTransform == null || ab == null) return;
        
        // Get current rotation error
        float error = CalculateRotationError();
        
        // Calculate PD control torque
        float torque = CalculatePDTorque(error);
        
        // Apply torque around the appropriate axis
        ApplyStabilizingTorque(torque);
        
        previousError = error;
    }
    
    private float CalculateRotationError()
    {
        Vector3 localEuler = referenceTransform.localEulerAngles;
        
        // Get the angle for the specified axis
        float currentAngle = stabilizeAxis switch
        {
            RotationAxis.Pitch => localEuler.x,
            RotationAxis.Roll => localEuler.z,
            RotationAxis.Yaw => localEuler.y,
            _ => 0f
        };
        
        // Target is 0 degrees, so error is negative of current angle
        float error = -currentAngle;
        
        // Normalize to -180 to 180 range
        return NormalizeAngle(error);
    }
    
    private float CalculatePDTorque(float error)
    {
        // Corrects current error
        float proportionalTorque = stiffness * error;
        
        // Resists rate of change (damping)
        float errorDerivative = (error - previousError) / Time.fixedDeltaTime;
        float derivativeTorque = damping * errorDerivative;
        
        // Combine and clamp
        float totalTorque = proportionalTorque + derivativeTorque;
        return Mathf.Clamp(totalTorque, -maxTorque, maxTorque);
    }
    
    private void ApplyStabilizingTorque(float torque)
    {
        // Map stabilization axis to torque application axis
        // Note: torque axis is perpendicular to rotation axis
        // Fixed rotation switching (why do the axis change?)
        Vector3 torqueAxis = stabilizeAxis switch
        {
            RotationAxis.Pitch => Vector3.forward, 
            RotationAxis.Roll => Vector3.right,     
            RotationAxis.Yaw => Vector3.up,         
            _ => Vector3.forward
        };
        
        ab.AddRelativeTorque(torqueAxis * -torque);
    }
    
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (referenceTransform == null) return;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, referenceTransform.position);
        
        // Show current error
        float error = Mathf.Abs(CalculateRotationError());
        Gizmos.color = Color.Lerp(Color.green, Color.red, error / 30f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}