using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosUtils;
using Unity.Robotics.UrdfImporter;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Joint = UnityEngine.Joint;

public class Rover : MonoBehaviour
{
	private ROSConnection ros;

	public float pivotForce = 10000f;
	public float wheelForce = 10000f;
	public float wheelSpeedMultiplier = 50f;

	private ArticulationBody flp, blp, frp, brp;
    private ArticulationBody blw, flw, brw, frw;

	private JointStateMsg lastCommandMessage;

    void Start()
    {
	    ros = ROSConnection.GetOrCreateInstance();
	    ros.RegisterPublisher<JointStateMsg>("/joint_states");
	    ros.RegisterPublisher<JointStateMsg>("/topic_based_joint_states");
	    ros.Subscribe<JointStateMsg>("/topic_based_joint_commands", JointCommandCallback);
	    
        blw = transform.Find("base_link/chassis/left_leg/bl_ankle/bl_wheel").GetComponent<ArticulationBody>();
        flw = transform.Find("base_link/chassis/left_leg/fl_ankle/fl_wheel").GetComponent<ArticulationBody>();
        brw = transform.Find("base_link/chassis/right_leg/br_ankle/br_wheel").GetComponent<ArticulationBody>();
        frw = transform.Find("base_link/chassis/right_leg/fr_ankle/fr_wheel").GetComponent<ArticulationBody>();

        blp = transform.Find("base_link/chassis/left_leg/bl_ankle").GetComponent<ArticulationBody>();
        flp = transform.Find("base_link/chassis/left_leg/fl_ankle").GetComponent<ArticulationBody>();
        brp = transform.Find("base_link/chassis/right_leg/br_ankle").GetComponent<ArticulationBody>();
        frp = transform.Find("base_link/chassis/right_leg/fr_ankle").GetComponent<ArticulationBody>();
    }

    // Update is called once per frame
    void Update()
    {
	    string[] publishedJointNames = {
		    "chassis_to_diffbar", "tl_ball_x", "tl_ball_y", "tl_ball_z", "bl_ball_x",
		    "bl_ball_y", "bl_ball_z", "tr_ball_x", "tr_ball_y", "tr_ball_z", "br_ball_x",
		    "br_ball_y", "br_ball_z", "chassis_to_left_leg", "blp", "blw", "flp", "flw",
		    "chassis_to_right_leg", "brp", "brw", "frp", "frw"
	    };
	    
		if (lastCommandMessage is not null)
		{
			ArticulationBody[] wheels = {flw, blw, frw, brw};
			ArticulationBody[] pivots = {flp, blp, frp, brp};
			
			//Wheels
			for (int i = 0; i < 4; i++)
			{
				ArticulationDrive jointState = wheels[i].xDrive;
            	jointState.forceLimit = wheelForce;
            	jointState.targetVelocity = wheelSpeedMultiplier * (float)lastCommandMessage.velocity[i];
            	jointState.driveType = ArticulationDriveType.Velocity;
            	wheels[i].xDrive = jointState;
    		}

			//Pivots
			for (int i = 0; i < 4; i++)
			{
				ArticulationDrive jointState = pivots[i].xDrive;
            	jointState.forceLimit = pivotForce;
            	jointState.target = 180f / 3.14159f * (float)lastCommandMessage.position[i];
            	jointState.driveType = ArticulationDriveType.Target;
            	pivots[i].xDrive = jointState;
    		}
		}

		//Copy and then publish joint states from last command message
		var jointCount = publishedJointNames.Length;
		JointStateMsg jointStates = new();
		jointStates.header = new HeaderMsg(TimePublisher.GetCurrentTime(), "");
		jointStates.name = publishedJointNames;
		jointStates.effort = new double[jointCount];
		jointStates.position =  new double[jointCount];
		jointStates.velocity = new double[jointCount];

		if (lastCommandMessage is not null)
		{
			for (var i = 0; i < publishedJointNames.Length; i++)
			{
				var jointName = publishedJointNames[i];
				if (lastCommandMessage.name.Contains(jointName))
				{
					var index = Array.IndexOf(lastCommandMessage.name, jointName);
					if (i < jointStates.effort.Length && index < lastCommandMessage.effort.Length)
						jointStates.effort[i] = lastCommandMessage.effort[index];
					if (i < jointStates.position.Length && index < lastCommandMessage.position.Length)
						jointStates.position[i] = lastCommandMessage.position[index];
					if (i < jointStates.velocity.Length && index < lastCommandMessage.velocity.Length)
						jointStates.velocity[i] = lastCommandMessage.velocity[index];
				}
			}
		}

		ros.Publish("/joint_states", jointStates);
		
		//Publish to ROS2 Control
		if (lastCommandMessage is not null)
		{
			ros.Publish("/topic_based_joint_states", lastCommandMessage);
		}
    }
    private void JointCommandCallback(JointStateMsg msg)
    {
	 	lastCommandMessage = msg;
    }
}