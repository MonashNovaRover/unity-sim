/*
   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
   Monash Nova Rover Team
   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
   CREATION:	27/01/2026
   CREATED BY:  Joel Kruger
   EDITED:      
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

public class RoverNew : MonoBehaviour
{
	private ROSConnection ros;

	public float pivotForce = 10000f;
	public float wheelForce = 10000f;
	
    string[] wheelNames = { "flw", "blw", "frw", "brw" };
    string[] pivotNames = { "flp", "blp", "frp", "brp" };

    private JointStateMsg lastCommandMessage;
    private Dictionary<string, HingeJoint> hingeJoints;
	
	private Dictionary<string, HingeJoint> GetHingeJoints(string[] jointNames)
	{
		var result = new Dictionary<string, HingeJoint>();
		
		Transform[] children = GetComponentsInChildren<Transform>();
		foreach (Transform child in children)
		{
			if (jointNames.Contains(child.name))
			{
				result.Add(child.name, child.GetComponent<HingeJoint>());
			}
		}

		return result;
	}
	
    void Start()
    {
	    ros = ROSConnection.GetOrCreateInstance();
	    ros.RegisterPublisher<JointStateMsg>("/topic_based_joint_states");
	    ros.Subscribe<JointStateMsg>("/topic_based_joint_commands", JointCommandCallback);
	    
	    string[] jointNames = wheelNames.Concat(pivotNames).ToArray();
	    hingeJoints = GetHingeJoints(jointNames);
    }

    // Update is called once per frame
    void Update()
    {
	    // Check ROS2 Control Command
		if (lastCommandMessage is not null)
		{

			string[] jointNames = lastCommandMessage.name;
			//Wheels
			foreach (string wheel in wheelNames)
			{
				int index = Array.IndexOf(jointNames, wheel);
				JointMotor motor = hingeJoints[wheel].motor;
				motor.targetVelocity = 180f / 3.14159f * (float)lastCommandMessage.velocity[index];
				hingeJoints[wheel].motor = motor;
    		}

			//Pivots
			foreach (string pivot in pivotNames)
			{
				int index = Array.IndexOf(jointNames, pivot);
				JointSpring spring = hingeJoints[pivot].spring;
				spring.targetPosition = 180f / 3.14159f * (float)lastCommandMessage.position[index];
	            hingeJoints[pivot].spring = spring;
    		}
		}

        /*
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
		*/
    }
    private void JointCommandCallback(JointStateMsg msg)
    {
	 	lastCommandMessage = msg;
        Debug.Log("msgg");

    }
}