using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using UnityEngine;
using SocketCANSharp;
using SocketCANSharp.Network;
using Unity.VisualScripting;

public class CanTest : MonoBehaviour
{
    private ConcurrentQueue<CanFrame> receivedFrames = new();
    private Thread canThread;

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
            "unknown blcmd", "fld", "bld", "brd", "frd",
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
    }
    void Update()
    {
        while (receivedFrames.TryDequeue(out CanFrame frame))
        {
            var blcmdNumber = (frame.CanId & 0xF0) >> 4;
            var blcmdCommand = (frame.CanId & 0x0F);

            var blcmdName = BLCMDNumberToName(blcmdNumber);
            var blcmdCommandName = BLCMDCommandToName(blcmdCommand);

            var rawData0 = BitConverter.ToInt16(frame.Data.AsSpan(0, 2));
            float data0 = (float)rawData0 / Int16.MaxValue;

            //Debug.Log("CANID: " + frame.CanId.ToString("X") + " data: " + frame.Data.ToHexString());

            switch (blcmdCommand)
            {
                case 0x3:
                    Debug.Log(blcmdName + ": Drive at speed " + data0);
                    break;
                case 0x4:
                    Debug.Log(blcmdName + ": Drive to position" + data0);
                    break;
            }
        }
    }
}