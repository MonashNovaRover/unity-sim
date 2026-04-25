// using UnityEngine;
// using System.Net.Sockets;
// using System.Collections;

// // Declare Unity component to attach to GameObject
// public class FrameTransmitter : MonoBehaviour
// {
//     public RenderTexture cam0;
//     private TcpClient client;
//     private NetworkStream stream;
//     private bool connected = false;

//     // Begin TCP connection: address, port, and stream
//     void Start()
//     {
//         StartCoroutine(ConnectWithRetry());
//     }

//     IEnumerator ConnectWithRetry()
//     {
//         while (!connected)
//         {
//             bool failed = false;
//             try
//             {
//                 client = new TcpClient("127.0.0.1", 5000);
//                 stream = client.GetStream();
//                 connected = true;
//                 Debug.Log("Connected to Python through TCP");
//             }
//             catch (SocketException e)
//             {
//                 Debug.Log("Still waiting ... ... retrying in 1s (" + e.Message + ")");
//                 failed = true;
//             }

//             if (failed)
//             {
//                 yield return new WaitForSeconds(1);
//             }
//         }
//     }

//     // Function to update/send frames
//     void FrameUpdate()
//     {
//         if (!connected) return;
//         SendFrame(cam0);
//     }

//     // Devise frame from camera and write to stream
//     void SendFrame(RenderTexture rt)
//     {
//         // Read texture from camera GameObject
//         // Create temporary 2D texture, 3 bytes per pixel (no alpha)
//         // Read pixels from camera texture to 2D texture
//         RenderTexture.active = rt;
//         Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
//         tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
//         tex.Apply();
//         RenderTexture.active = null;

//         // Encode 2D texture to JPG format
//         // Free 2D texture memory
//         byte[] bytes = tex.EncodeToJPG(85);
//         Destroy(tex);

//         // Declare TCP stream package length in header
//         // Write header and frame bytes to stream
//         byte[] header = System.BitConverter.GetBytes(bytes.Length);
//         stream.Write(header, 0, 4);
//         stream.Write(bytes, 0, bytes.Length);
//     }
// }

using System.Collections;
using System.Net.Sockets;
using UnityEngine;

public class FrameTransmitter : MonoBehaviour
{
    public RenderTexture cam0;
    private TcpClient client;
    private NetworkStream stream;
    private bool connected = false;

    void Start()
    {
        Debug.Log("FrameTransmitter starting...");
        StartCoroutine(ConnectWithRetry());
    }

    IEnumerator ConnectWithRetry()
    {
        Debug.Log("Attempting connection to 127.0.0.1:5000...");
        while (!connected)
        {
            bool failed = false;
            try
            {
                client = new TcpClient("127.0.0.1", 5000);
                stream = client.GetStream();
                connected = true;
                Debug.Log("Connected to Python successfully!");
            }
            catch (SocketException e)
            {
                Debug.Log("Connection failed: " + e.Message);
                failed = true;
            }

            if (failed)
                yield return new WaitForSeconds(1f);
        }
        Debug.Log("Exited retry loop, connected = " + connected);
    }

    void LateUpdate()
    {
        if (!connected) return;
        // Debug.Log("Sending frame...");
        SendFrame(cam0);
    }

    void SendFrame(RenderTexture rt)
    {
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToJPG(85);
        Destroy(tex);

        byte[] header = System.BitConverter.GetBytes(bytes.Length);
        stream.Write(header, 0, 4);
        stream.Write(bytes, 0, bytes.Length);
    }
}
