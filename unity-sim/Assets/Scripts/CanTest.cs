using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RosSharp.RosBridgeClient.MessageGeneration;
using UnityEngine;
using SocketCANSharp;
using SocketCANSharp.Network;
using Unity.Robotics.UrdfImporter;
using Unity.VisualScripting;

public class CanTest : MonoBehaviour
{
    public float pivotForce = 35f;
    public float wheelForce = 10000f;
    
    private ConcurrentQueue<CanFrame> receivedFrames = new();
    private Thread canThread;

    public GameObject led;
    private Light ledLight;
    
    string[] pivotNames = { "flp", "blp", "frp", "brp" };
    string[] wheelNames = { "flw", "blw", "frw", "brw" };

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
    
    void CANThread()
    {
        CanNetworkInterface can0 = CanNetworkInterface.GetAllInterfaces(true).First(iface => iface.Name.Equals("can0"));
        RawCanSocket can0Socket = new();
        can0Socket.Bind(can0);

        while (true)
        {
            can0Socket.Read(out CanFrame frame);
            receivedFrames.Enqueue(frame);
        }
    }

    string BLCMDNumberToName(uint number)
    {
        string[] table =
        {
            "unknown blcmd", "flw", "blw", "brw", "frw",
            "flp", "blp", "brp", "frp"
        };
        return (number < table.Length) ? table[number] : "unknown blcmd";
    }

    string BLCMDCommandToName(uint command)
    {
        string[] table =
        {
            "Stop", "Twitch Forward", "Twitch Backward", "Drive at Speed",
            "Drive to Position", "Drive at Current", "Drive Open Loop", "Home Rotor",
            "Resolver Zero Flag", "Get Configuration", "Set Configuration", "Reset BLCMD"
        };

        return (command < table.Length) ? table[command] : "Unknown Command";
    }

    void Start()
    {
        canThread = new Thread(CANThread);
        canThread.Start();
        
        ledLight = led.GetComponent<Light>();
        GetArticulationBodies(pivotNames);
        GetArticulationBodies(wheelNames);
    }

    void HandleBLCMDCommand(CanFrame frame)
    {
        var blcmdNumber = (frame.CanId & 0xF0) >> 4;
        var blcmdCommand = (frame.CanId & 0x0F);

        var blcmdName = BLCMDNumberToName(blcmdNumber);
        var blcmdCommandName = BLCMDCommandToName(blcmdCommand);

        var rawDataInt16 = BitConverter.ToInt16( new byte[] {frame.Data[1], frame.Data[0]} );

        switch (blcmdCommand)
        {
            case 0x3: //Drive at speed
            {
                var speedRadiansPerSecond = 30.0f * (float)rawDataInt16 / Int16.MaxValue;
                
                //Debug.Log(blcmdName + ": Drive at speed " + speedRadiansPerSecond);

                if (blcmdName == "flw" || blcmdName == "blw")
                {
                    speedRadiansPerSecond *= -1.0f;
                }

                ArticulationDrive jointState = articulationBodies[blcmdName].xDrive;
                jointState.stiffness = 0.0f;
                jointState.damping = 10000f;
                jointState.forceLimit = wheelForce;
                jointState.targetVelocity = Mathf.Rad2Deg * speedRadiansPerSecond;
                jointState.driveType = ArticulationDriveType.Velocity;
                articulationBodies[blcmdName].xDrive = jointState;
            } break;
            case 0x4: //Drive to position
            {
                var angleRadians = Mathf.PI * (rawDataInt16 - 0x397D) / UInt16.MaxValue;

                if (blcmdName == "flp" || blcmdName == "brp")
                {
                    angleRadians *= -1.0f;
                }

                //Debug.Log(blcmdName + ": Drive to position " + rawData0);
                //Debug.Log(blcmdName + ": Drive to angle " + angleRadians);

                ArticulationDrive jointState = articulationBodies[blcmdName].xDrive;
                jointState.forceLimit = pivotForce;
                jointState.target = Mathf.Rad2Deg * angleRadians;
                jointState.driveType = ArticulationDriveType.Target;
                articulationBodies[blcmdName].xDrive = jointState;
            } break;
        }
    }
    
    void HandleLEDCommand(CanFrame frame)
    {
        float intensity = (float)frame.Data[0] / 255.0f;
        Color color = ledLight.color;
        
        //From: https://github.com/MonashNovaRover/pics/blob/feature/ledstrip_v3/ledstrip_v3/ledstrip_v3_James.X/main.c
        int temp_data = frame.Data[0];
        
        switch (frame.CanId & 0xF)
        {
            case 0x1: //BRIGHTNESS_ID
                if (color.r != 0) color.r = temp_data / 128.0f;
                if (color.g != 0) color.g = temp_data / 128.0f;
                if (color.b != 0) color.b = temp_data / 128.0f;
                break;
            case 0x2: //Red
                color.r = temp_data;
                break;
            case 0x3: //Green
                color.g = temp_data;
                break;
            case 0x4: //Blue
                color.b = temp_data;
                break;
            case 0x5: //PRESET_ID
                switch (frame.Data[0])
                {
                    case 0x1: //Red
                        color = Color.red;
                        break;
                    case 0x2:
                        color = Color.green;
                        break;
                    case 0x3:
                        color = Color.blue;
                        break;
                }
                break;
            case 0x6: //ALL_ID
                int temp_r = (frame.Data[0] & 0xF0) >> 4;
                int temp_g = frame.Data[0] & 0x0F;
                int temp_b = (frame.Data[1] & 0xF0) >> 4;
                color = new Color(temp_r / 15.0f, temp_b / 15.0f, temp_g / 15.0f);
                break;
            case 0x7: //GLOBAL_PINK_ID
                color = new Color(1.0f, 0.0f, 0.5f, 1.0f);
                Debug.Log("color is " + color.ToString());
                break;
            case 0x8: //RESET_ALL
                color = Color.black;
                break;
        }

        ledLight.color = color;
        //Debug.Log("color is " + color.ToString());
        //Debug.Log("LED is" + ledLight.color.ToString());
    }
    
    void Update()
    {
        while (receivedFrames.TryDequeue(out CanFrame frame))
        {
            if ((frame.CanId >> 8) == 0 && ((frame.CanId >> 4) & 0xF) <= 0x8)
            {
                HandleBLCMDCommand(frame);
            }
            else if ((frame.CanId >> 8) == 0 && ((frame.CanId >> 4) & 0xF) == 0x9)
            {
                HandleLEDCommand(frame);
            }
        }
    }
}