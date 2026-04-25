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
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.Rendering.PostProcessing;
using UnitySensors.Sensor.GNSS;

public class Rover : MonoBehaviour
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
    void Start()
    {
		GetArticulationBodies(wheelNames);
		GetArticulationBodies(pivotNames);
		GetArticulationBodies(legNames);
		
		//Find GNSS coordinate origin in the scene
		GNSSSensor gnss = transform.Find("base_link/chassis/GNSS_ros").GetComponent<GNSSSensor>();
		GeoCoordinateSystem coord = transform.Find("/GeoCoordinateSystem").GetComponent<GeoCoordinateSystem>();
		typeof(GNSSSensor).GetField("_coordinateSystem", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gnss, coord);
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