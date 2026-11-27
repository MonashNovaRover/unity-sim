using System.Collections.Generic;
using Unity.Robotics.UrdfImporter;
using UnityEngine;


public class Arm : MonoBehaviour
{
    public float pivotForce = 10000f;
    private Dictionary<string, ArticulationBody> articulationBodies = new();
    
    private string[] pivotNames =
    {
        "j1", 
        "j2",  
        "j3", 
        "l1_p_lower", 
        "l1_p_upper", 
        "l1_l2_pivot", 
        "j4", 
        "j5", 
        "j6", 
        "lead_left", 
        "la_prismatic",
        "arm_link_to_arm_box",
        "arm_kinematics_origin_joint",
        "endeffector_kinematics_joint",
        "chassis_to_arm_base",
        // "base_link_to_chassis"
    };
    
    /*void GetArticulationBodies(string[] jointNames)
    {
        UrdfJoint[] urdfJoints = GetComponentsInChildren<UrdfJoint>();
        foreach (UrdfJoint urdfJoint in urdfJoints)
        {
            if (jointNames.Contains(urdfJoint.jointName))
            {
                articulationBodies.Add(urdfJoint.jointName, urdfJoint.GetComponent<ArticulationBody>());
            }
        }
    }*/
    
    void Start()
    {
        print("HELLO IS THIS WORKING?");
    }

    void Update()
    {
        print("HELLO IS THIS WORKING?");
    }
}