using UnityEngine;
using System.Collections.Generic;

using RosSharp.RosBridgeClient;
using RosUtils;
using JointStateMsg = RosSharp.RosBridgeClient.MessageTypes.Sensor.JointState;

public class Rover : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

	public float pivotForce = 1000f;
	public float wheelForce = 1000f;
	public float wheelSpeedMultiplier = 50f;

	private ArticulationBody flp, blp, frp, brp;
    private ArticulationBody blw, flw, brw, frw;

	private Subscriber<JointStateMsg> jointStateSubscriber = new("/topic_based_joint_commands");
	private JointStateMsg lastCommandMessage;
    void Start()
    {
        blw = transform.Find("base_link/chassis/left_leg/bl_ankle/bl_wheel").GetComponent<ArticulationBody>();
        flw = transform.Find("base_link/chassis/left_leg/fl_ankle/fl_wheel").GetComponent<ArticulationBody>();
        brw = transform.Find("base_link/chassis/right_leg/br_ankle/br_wheel").GetComponent<ArticulationBody>();
        frw = transform.Find("base_link/chassis/right_leg/fr_ankle/fr_wheel").GetComponent<ArticulationBody>();

        blp = transform.Find("base_link/chassis/left_leg/bl_ankle").GetComponent<ArticulationBody>();
        flp = transform.Find("base_link/chassis/left_leg/fl_ankle").GetComponent<ArticulationBody>();
        brp = transform.Find("base_link/chassis/right_leg/br_ankle").GetComponent<ArticulationBody>();
        frp = transform.Find("base_link/chassis/right_leg/fr_ankle").GetComponent<ArticulationBody>();

        jointStateSubscriber.OnMessage += JointCommandCallback;
        jointStateSubscriber.Connect();
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
	}
    private void JointCommandCallback(JointStateMsg msg)
    {
	 	lastCommandMessage = msg;
    }
}