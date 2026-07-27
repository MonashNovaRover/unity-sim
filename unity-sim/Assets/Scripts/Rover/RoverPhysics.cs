/*
   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
   Monash Nova Rover Team
   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
   CREATION:	23/12/2025
   CREATED BY:  Joel Kruger
   EDITED:      08/01/2026
   EDITED BY:
   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
 */

using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Robotics.UrdfImporter;
using UnitySensors.Sensor.GNSS;

public class RoverPhysics : MonoBehaviour
{
	public float pivotForce = 10000f;
	public float wheelForce = 10000f;
	
    string[] wheelNames = { "flw", "blw", "frw", "brw" };
    string[] pivotNames = { "flp", "blp", "frp", "brp" };
    string[] legNames = { "chassis_to_left_leg", "chassis_to_right_leg", "chassis_to_diffbar" };

    private Dictionary<string, ArticulationBody> articulationBodies = new();

    void GetArticulationBodies(string[] jointNames)
    {
	    UrdfJoint[] urdfJoints = GetComponentsInChildren<UrdfJoint>();
	    foreach (UrdfJoint urdfJoint in urdfJoints)
	    {
		    if (jointNames.Contains(urdfJoint.jointName))
		    {
			    articulationBodies.Add(urdfJoint.jointName, urdfJoint.GetComponent<ArticulationBody>());
		    }
	    }
    }
    //Hardcode, this function makes it easier to test physics with changes consistent across rover prefabs
    void SetJointMasses()
    {
        foreach (var joint in articulationBodies)
        {
            float mass = 1e-7f;
            if (joint.Key == "flw" || joint.Key == "frw" || joint.Key == "blw" || joint.Key == "brw")
            {
                mass = 5.0f;
            }

            joint.Value.mass = mass;
        }
    }
        void ZeroAllJoints()
    {
        foreach (var joint in articulationBodies)
        {
            ArticulationDrive jointState = joint.Value.xDrive;
            jointState.forceLimit = 35f;
            jointState.target = 0.0f;
            jointState.driveType = ArticulationDriveType.Target;
            joint.Value.xDrive = jointState;
        }
    }
    void Start()
    {
		GetArticulationBodies(wheelNames);
		GetArticulationBodies(pivotNames);
		GetArticulationBodies(legNames);

        ZeroAllJoints();
        SetJointMasses();

        //Find GNSS coordinate origin in the scene
        GNSSSensor gnss = transform.Find("base_link/chassis/GNSS_ros")?.GetComponent<GNSSSensor>();
		GeoCoordinateSystem coord = transform.Find("/GeoCoordinateSystem")?.GetComponent<GeoCoordinateSystem>();

		if (gnss is not null && coord is not null)
		{
			typeof(GNSSSensor).GetField("_coordinateSystem", BindingFlags.Instance | BindingFlags.NonPublic)
				?.SetValue(gnss, coord);
		}
    }
    void DoDiffBarPhysics()
    {
	    float leftLegAngle  = articulationBodies[legNames[0]].jointPosition[0];
	    float rightLegAngle = articulationBodies[legNames[1]].jointPosition[0];
	    
	    float deltaAngle = rightLegAngle - leftLegAngle;
	    float halfDeltaAngle = 0.5f * deltaAngle;
		
	    float target = leftLegAngle + halfDeltaAngle;
	    
	    //Left
	    ArticulationDrive jointState = articulationBodies[legNames[0]].xDrive;
	    jointState.target = Mathf.Rad2Deg * target;
	    articulationBodies[legNames[0]].xDrive = jointState;
	    
	    //Right	    
	    jointState = articulationBodies[legNames[1]].xDrive;
	    jointState.target = Mathf.Rad2Deg * target;
	    articulationBodies[legNames[1]].xDrive = jointState;
	    
	    //Debug.Log("Diffbar error (degrees): " + deltaAngle * Mathf.Rad2Deg);
	    
	    ArticulationDrive diffbar = articulationBodies[legNames[2]].xDrive;
	    diffbar.target = -1.3f * Mathf.Rad2Deg * target;
	    articulationBodies[legNames[2]].xDrive = diffbar;
    }
    void Update()
    {
	    DoDiffBarPhysics();
    }
}