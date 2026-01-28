using UnityEngine;
using System.Collections.Generic;

public class TransformAngleCorrection : MonoBehaviour
{
    public Transform chassis;
    public Transform leftLeg;
    public Transform rightLeg;
    
    // Transform Names to Find and lock position + rotation
    [Tooltip("Exact names of transforms to lock (case-insensitive)")]
    public List<string> transformNamesToFind = new List<string>
    {
        "Collisions",
        "Visuals",
        "clam",
        "diffbar",
        "left_difflink",
        "right_difflink"
    };
    
    // Ratio between legs (0 = follow left leg, 1 = follow right leg, 0.5 = average)
    [Range(0f, 1f)]
    public float differentialRatio = 0.5f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Stored initial transforms
    private List<TransformData> lockedTransforms = new List<TransformData>();
    
    // Initial leg states
    private Vector3 initialLeftLegPosition;
    private Quaternion initialLeftLegRotation;
    private Vector3 initialRightLegPosition;
    private Quaternion initialRightLegRotation;
    
    // Helper class to store transform data
    [System.Serializable]
    private class TransformData
    {
        public Transform transform;
        public Vector3 initialWorldPosition;
        public Quaternion initialWorldRotation;
        public string name;
        
        // Relative to the interpolated leg frame
        public Vector3 relativePosition;
        public Quaternion relativeRotation;
        
        public TransformData(Transform t)
        {
            transform = t;
            initialWorldPosition = t.position;
            initialWorldRotation = t.rotation;
            name = t.name;
        }
        
        public void CalculateRelativeTransform(Vector3 referencePosition, Quaternion referenceRotation)
        {
            // Calculate position relative to reference frame
            Vector3 offset = initialWorldPosition - referencePosition;
            relativePosition = Quaternion.Inverse(referenceRotation) * offset;
            
            // Calculate rotation relative to reference frame
            relativeRotation = Quaternion.Inverse(referenceRotation) * initialWorldRotation;
        }
        
        public void ApplyRelativeTransform(Vector3 referencePosition, Quaternion referenceRotation)
        {
            if (transform != null)
            {
                // Apply relative rotation
                transform.rotation = referenceRotation * relativeRotation;
                
                // Apply relative position
                Vector3 worldOffset = referenceRotation * relativePosition;
                transform.position = referencePosition + worldOffset;
            }
        }
    }
    
    void Start()
    {
        if (chassis == null)
        {
            Debug.LogError("Chassis not assigned");
            return;
        }
        
        if (leftLeg == null || rightLeg == null)
        {
            Debug.LogError("Left leg and/or right leg not assigned");
            return;
        }
        
        InitializeTransforms();
    }
    
    void InitializeTransforms()
    {
        // Store initial leg states
        initialLeftLegPosition = leftLeg.position;
        initialLeftLegRotation = leftLeg.rotation;
        initialRightLegPosition = rightLeg.position;
        initialRightLegRotation = rightLeg.rotation;
        
        // Calculate initial interpolated reference frame
        Vector3 initialReferencePosition = Vector3.Lerp(initialLeftLegPosition, initialRightLegPosition, differentialRatio);
        Quaternion initialReferenceRotation = Quaternion.Slerp(initialLeftLegRotation, initialRightLegRotation, differentialRatio);
        
        List<string> namesToFind = new List<string>();
        foreach (string name in transformNamesToFind)
        {
            namesToFind.Add(name.ToLower());
        }
        
        // Search through chassis children
        foreach (Transform child in chassis)
        {
            string childNameLower = child.name.ToLower();
            
            // Check if this child's name matches any in list
            if (namesToFind.Contains(childNameLower))
            {
                TransformData data = new TransformData(child);
                data.CalculateRelativeTransform(initialReferencePosition, initialReferenceRotation);
                lockedTransforms.Add(data);
                
                if (showDebugInfo)
                {
                    Debug.Log($"Found and locked: {child.name} at world pos: {data.initialWorldPosition}, rot: {data.initialWorldRotation.eulerAngles}");
                    Debug.Log($"  Relative pos: {data.relativePosition}, Relative rot: {data.relativeRotation.eulerAngles}");
                }
                
                // Find difflinks through diffbar children
                if (childNameLower == "diffbar")
                {
                    FindDifflinks(child, namesToFind, initialReferencePosition, initialReferenceRotation);
                }
            }
        }
        
        if (showDebugInfo)
        {
            foreach (string nameToFind in transformNamesToFind)
            {
                bool found = false;
                foreach (TransformData data in lockedTransforms)
                {
                    if (data.name.ToLower() == nameToFind.ToLower())
                    {
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    Debug.LogWarning($"Could not find transform named: {nameToFind}");
                }
            }
            
            Debug.Log($"Total transforms locked: {lockedTransforms.Count}");
        }
    }
    
    void FindDifflinks(Transform diffbar, List<string> namesToFind, Vector3 referencePosition, Quaternion referenceRotation)
    {
        foreach (Transform child in diffbar)
        {
            string childNameLower = child.name.ToLower();
            
            if (namesToFind.Contains(childNameLower))
            {
                TransformData data = new TransformData(child);
                data.CalculateRelativeTransform(referencePosition, referenceRotation);
                lockedTransforms.Add(data);
                
                if (showDebugInfo)
                {
                    Debug.Log($"Found difflink: {child.name} at world pos: {data.initialWorldPosition}, rot: {data.initialWorldRotation.eulerAngles}");
                    Debug.Log($"  Relative pos: {data.relativePosition}, Relative rot: {data.relativeRotation.eulerAngles}");
                }
            }
        }
    }
    
    void Update()
    {
        if (leftLeg == null || rightLeg == null) return;
        
        // Calculate current interpolated reference frame based on leg positions
        Vector3 currentReferencePosition = Vector3.Lerp(leftLeg.position, rightLeg.position, differentialRatio);
        Quaternion currentReferenceRotation = Quaternion.Slerp(leftLeg.rotation, rightLeg.rotation, differentialRatio);
        
        // Update all locked transforms to maintain their relative relationship
        foreach (TransformData data in lockedTransforms)
        {
            data.ApplyRelativeTransform(currentReferencePosition, currentReferenceRotation);
        }
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || chassis == null) return;
        
        // Draw reference frame (interpolated leg position)
        if (leftLeg != null && rightLeg != null)
        {
            Vector3 refPos = Vector3.Lerp(leftLeg.position, rightLeg.position, differentialRatio);
            Quaternion refRot = Quaternion.Slerp(leftLeg.rotation, rightLeg.rotation, differentialRatio);
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(refPos, 0.15f);
            
            // Draw reference axes
            Gizmos.color = Color.red;
            Gizmos.DrawRay(refPos, refRot * Vector3.right * 0.3f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(refPos, refRot * Vector3.up * 0.3f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(refPos, refRot * Vector3.forward * 0.3f);
        }
        
        // Draw locked transforms
        foreach (TransformData data in lockedTransforms)
        {
            if (data.transform != null)
            {
                // Color code based on name
                if (data.name.ToLower().Contains("left"))
                    Gizmos.color = Color.cyan;
                else if (data.name.ToLower().Contains("right"))
                    Gizmos.color = Color.red;
                else if (data.name.ToLower() == "diffbar")
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = Color.green;
                
                Gizmos.DrawWireSphere(data.transform.position, 0.1f);
                
                // Draw line from reference position to transform
                if (leftLeg != null && rightLeg != null)
                {
                    Vector3 refPos = Vector3.Lerp(leftLeg.position, rightLeg.position, differentialRatio);
                    Gizmos.DrawLine(refPos, data.transform.position);
                }
            }
        }
    }
}