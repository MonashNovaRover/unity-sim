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
        
        //Set initial speed / position
        ArticulationBody[] wheels = {flw, blw, frw, brw};
        ArticulationBody[] pivots = {flp, blp, frp, brp};
 
        //Wheels
        for (int i = 0; i < 4; i++)
        {
	        ArticulationDrive jointState = wheels[i].xDrive;
	        jointState.forceLimit = wheelForce;
	        jointState.targetVelocity = 0;
	        jointState.driveType = ArticulationDriveType.Velocity;
	        wheels[i].xDrive = jointState;
        }

        //Pivots
        for (int i = 0; i < 4; i++)
        {
	        ArticulationDrive jointState = pivots[i].xDrive;
	        jointState.forceLimit = pivotForce;
	        jointState.target = 0;
	        jointState.driveType = ArticulationDriveType.Target;
	        pivots[i].xDrive = jointState;
        }
    }

    // Update is called once per frame
    void Update()
    {
		if (lastCommandMessage is not null)
		{
			ArticulationBody[] wheels = {flw, blw, frw, brw};
			ArticulationBody[] pivots = {flp, blp, frp, brp};
			
			//Wheels
			for (int i = 0; i < 4; i++)
			{
				ArticulationDrive jointState = wheels[i].xDrive;
            	jointState.forceLimit = wheelForce;
            	jointState.targetVelocity = 180 / 3.14159f * (float)lastCommandMessage.velocity[i];
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
		
		//Publish to ROS2 Control
		if (lastCommandMessage is not null)
		{
			var count = lastCommandMessage.name.Length;
			JointStateMsg stateMessage = new();
			stateMessage.header =  new HeaderMsg(TimePublisher.GetCurrentTime(), "");
			stateMessage.name = lastCommandMessage.name;
			stateMessage.position = new double[count];
			stateMessage.velocity = new double[count];
			stateMessage.effort = new double[count];
			
			Array.Copy(lastCommandMessage.position, 0, stateMessage.position, 4, 4);
			Array.Copy(lastCommandMessage.velocity, 0, stateMessage.velocity, 0, 8);
			
			ros.Publish("/topic_based_joint_states", stateMessage);
		}
    }
    private void JointCommandCallback(JointStateMsg msg)
    {
	 	lastCommandMessage = msg;
    }
}