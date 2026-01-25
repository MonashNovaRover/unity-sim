using UnityEngine;

public class DiffbarStabilizer : MonoBehaviour
{
    public Transform leftLegTransform;
    public Transform rightLegTransform;

    // Proportional gain (correction)
    [Range(0f, 10000f)] public float stiffness = 1000f;

    // Derivative gain (damping)
    [Range(0f, 1000f)] public float damping = 50f;

    [Range(0f, 1000f)] public float maxTorque = 500f;

    // Control Axis
    public RotationAxis stabilizeAxis = RotationAxis.Pitch;

    // Ratio of differential motion
    [Range(0f, 1f)] public float differentialRatio = 0.5f;
    public float maxDeviation = 15f;
    private ArticulationBody ab;
    private float previousError = 0f;

    public enum RotationAxis
    {
        Pitch,
        Roll,
        Yaw
    }

    void Start()
    {
        ab = GetComponent<ArticulationBody>();

        if (leftLegTransform == null || rightLegTransform == null)
        {
            Debug.LogError($"Leg transforms not set on {gameObject.name}!");
        }
    }

    void FixedUpdate()
    {
        if (leftLegTransform == null || rightLegTransform == null || ab == null) return;

        // Calculate target angle based on leg positions
        float targetAngle = CalculateTargetAngleFromLegs();

        // Get current rotation error relative to target
        float error = CalculateRotationError(targetAngle);

        // Calculate PD control torque
        float torque = CalculatePDTorque(error);

        // Apply torque around the appropriate axis
        ApplyStabilizingTorque(torque);

        previousError = error;
    }

    private float CalculateTargetAngleFromLegs()
    {
        // Get the rotation angles of both legs
        float leftAngle = GetAxisAngle(leftLegTransform);
        float rightAngle = GetAxisAngle(rightLegTransform);

        // Normalize angles to -180 to 180
        leftAngle = NormalizeAngle(leftAngle);
        rightAngle = NormalizeAngle(rightAngle);

        // Handle wrap-around case (e.g., one leg at -170°, other at 170°)
        float angleDiff = Mathf.Abs(rightAngle - leftAngle);
        if (angleDiff > 180f)
        {
            if (leftAngle < 0) leftAngle += 360f;
            else rightAngle += 360f;
        }

        // Calculate target as weighted average (differential bar keeps chassis between legs)
        float targetAngle = Mathf.Lerp(leftAngle, rightAngle, differentialRatio);

        return NormalizeAngle(targetAngle);
    }

    private float GetAxisAngle(Transform transform)
    {
        Vector3 localEuler = transform.localEulerAngles;

        return stabilizeAxis switch
        {
            RotationAxis.Pitch => localEuler.x,
            RotationAxis.Roll => localEuler.z,
            RotationAxis.Yaw => localEuler.y,
            _ => 0f
        };
    }

    private float CalculateRotationError(float targetAngle)
    {
        // Get current angle of the chassis to apply to the diffbar
        float currentAngle = GetAxisAngle(transform);
        currentAngle = NormalizeAngle(currentAngle);

        float error = targetAngle - currentAngle;
        error = NormalizeAngle(error);

        // Optionally clamp error to max deviation
        if (maxDeviation > 0)
        {
            error = Mathf.Clamp(error, -maxDeviation, maxDeviation);
        }

        return error;
    }

    private float CalculatePDTorque(float error)
    {
        // Proportional term: corrects current error
        float proportionalTorque = stiffness * error;

        // Derivative term: resists rate of change (damping)
        float errorDerivative = (error - previousError) / Time.fixedDeltaTime;
        float derivativeTorque = damping * errorDerivative;

        // Combine and clamp
        float totalTorque = proportionalTorque + derivativeTorque;
        return Mathf.Clamp(totalTorque, -maxTorque, maxTorque);
    }

    private void ApplyStabilizingTorque(float torque)
    {
        // Map stabilization axis to torque application axis
        Vector3 torqueAxis = stabilizeAxis switch
        {
            RotationAxis.Pitch => Vector3.forward,
            RotationAxis.Roll => Vector3.right,
            RotationAxis.Yaw => Vector3.up,
            _ => Vector3.forward
        };

        ab.AddRelativeTorque(torqueAxis * torque);
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
        if (leftLegTransform == null || rightLegTransform == null) return;

        // Draw lines to both legs
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, leftLegTransform.position);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, rightLegTransform.position);

        // Show target angle indicator
        if (Application.isPlaying)
        {
            float targetAngle = CalculateTargetAngleFromLegs();
            float currentAngle = GetAxisAngle(transform);
            float error = Mathf.Abs(NormalizeAngle(targetAngle - currentAngle));

            // Color code based on error
            Gizmos.color = Color.Lerp(Color.green, Color.yellow, error / maxDeviation);
            Gizmos.DrawWireSphere(transform.position, 0.2f);

            // Draw direction arrow showing target
            Vector3 direction = stabilizeAxis switch
            {
                RotationAxis.Pitch => transform.right,
                RotationAxis.Roll => transform.forward,
                RotationAxis.Yaw => transform.up,
                _ => transform.forward
            };

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, direction * 0.5f);
        }
    }
}