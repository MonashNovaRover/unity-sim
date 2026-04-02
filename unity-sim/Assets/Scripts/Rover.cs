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
using Unity.Robotics.UrdfImporter;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.Rendering.PostProcessing;

public class Rover : MonoBehaviour
{
	private ROSConnection ros;

	public float pivotForce = 10000f;
	public float wheelForce = 10000f;
	
    string[] wheelNames = { "flw", "blw", "frw", "brw" };
    string[] pivotNames = { "flp", "blp", "frp", "brp" };
    string[] legNames = { "chassis_to_left_leg", "chassis_to_right_leg", "chassis_to_diffbar" };

    private JointStateMsg lastCommandMessage;
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
	    ros = ROSConnection.GetOrCreateInstance();
	    ros.RegisterPublisher<JointStateMsg>("/topic_based_joint_states");
	    ros.Subscribe<JointStateMsg>("/topic_based_joint_commands", JointCommandCallback);
	    
	    GetArticulationBodies(wheelNames);
	    GetArticulationBodies(pivotNames);
	    GetArticulationBodies(legNames);
    }

    void DoDriveUpdate()
    {
	    // Check ROS2 Control Command
		if (lastCommandMessage is not null)
		{
			string[] jointNames = lastCommandMessage.name;
			//Wheels
			foreach (string wheel in wheelNames)
			{
				int index = Array.IndexOf(jointNames, wheel);
				ArticulationDrive jointState = articulationBodies[wheel].xDrive;
				jointState.stiffness = 0.0f;
				jointState.damping = 10000f;
            	jointState.forceLimit = wheelForce;
            	jointState.targetVelocity = 180f / 3.14159f * (float)lastCommandMessage.velocity[index];
            	jointState.driveType = ArticulationDriveType.Velocity;
	            articulationBodies[wheel].xDrive = jointState;
    		}

			//Pivots
			foreach (string pivot in pivotNames)
			{
				int index = Array.IndexOf(jointNames, pivot);
				ArticulationDrive jointState = articulationBodies[pivot].xDrive;
            	jointState.forceLimit = pivotForce;
            	jointState.target = 180f / 3.14159f * (float)lastCommandMessage.position[index];
            	jointState.driveType = ArticulationDriveType.Target;
	            articulationBodies[pivot].xDrive = jointState;
    		}
		}
		
		// Publish ROS2 Control States
		var count = wheelNames.Length + pivotNames.Length;
		JointStateMsg stateMessage = new();
		stateMessage.header =  new HeaderMsg(TimePublisher.GetCurrentTime(), "");
		stateMessage.name = new string[count];
		stateMessage.position = new double[count];
		stateMessage.velocity = new double[count];
		stateMessage.effort = new double[count];

		//Wheels
		for (int i = 0; i < wheelNames.Length; i++)
		{
			string wheel = wheelNames[i];
			stateMessage.name[i] = wheel;
			stateMessage.position[i] = articulationBodies[wheel].jointPosition[0];
			stateMessage.velocity[i] = articulationBodies[wheel].jointVelocity[0];
		}
		
		//Pivots
		for (int i = 0; i < pivotNames.Length; i++)
		{
			string pivot = pivotNames[i];
			stateMessage.name[wheelNames.Length + i] = pivot;
			stateMessage.position[wheelNames.Length + i] = articulationBodies[pivot].jointPosition[0];
			stateMessage.velocity[wheelNames.Length + i] = articulationBodies[pivot].jointVelocity[0];
		}
		
		ros.Publish("/topic_based_joint_states", stateMessage);
    }

    /*
    void DoDiffBarPhysics()
    {
	    float leftLegAngle  = articulationBodies[legNames[0]].jointPosition[0];
	    float rightLegAngle = articulationBodies[legNames[1]].jointPosition[0];
	    
	    float deltaAngle = rightLegAngle - leftLegAngle;
		float halfDeltaAngle = 0.5f * deltaAngle;
		
	    float target = leftLegAngle + halfDeltaAngle;

	    float e = target - leftLegAngle;
	    float force = Math.Max(40.0f, Math.Abs(1000 * e));
	    
	    //Left
	    ArticulationDrive jointState = articulationBodies[legNames[0]].xDrive;
	    jointState.driveType = ArticulationDriveType.Target;
	    jointState.forceLimit = force;
	    jointState.target = Mathf.Rad2Deg * target;
	    articulationBodies[legNames[0]].xDrive = jointState;
	    
		//Right	    
	    jointState = articulationBodies[legNames[1]].xDrive;
	    jointState.driveType = ArticulationDriveType.Target;
	    jointState.forceLimit = force;
	    jointState.target = Mathf.Rad2Deg * target;
	    articulationBodies[legNames[1]].xDrive = jointState;
	    
	    Debug.Log("Diffbar error (degrees): " + deltaAngle * Mathf.Rad2Deg);
	    
	    ArticulationDrive diffbar = articulationBodies[legNames[2]].xDrive;
	    diffbar.target = -1.3f * Mathf.Rad2Deg * target;
	    articulationBodies[legNames[2]].xDrive = diffbar;
    }*/
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
	    //DoDriveUpdate();
	    DoDiffBarPhysics();
    }
    private void JointCommandCallback(JointStateMsg msg)
    {
	 	lastCommandMessage = msg;
    }
}