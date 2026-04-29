using System.Collections;
using System.Net.Sockets;
using UnityEngine;

public class FrameTransmitter : MonoBehaviour
{
    public RenderTexture cam0;
    public RenderTexture cam1;
    public RenderTexture cam2;
    public RenderTexture cam3;
    public int port = 5000;

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
        Debug.Log($"Attempting connection to 127.0.0.1:{port}...");
        while (!connected)
        {
            bool failed = false;
            try
            {
                client = new TcpClient("127.0.0.1", port);
                stream = client.GetStream();
                connected = true;
                Debug.Log($"Connected to Python on port {port}!");
            }
            catch (SocketException e)
            {
                Debug.Log($"Port {port} connection failed: " + e.Message);
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
        if (cam0 != null) SendFrame(cam0, camId: 0);
        if (cam1 != null) SendFrame(cam1, camId: 1);
        if (cam2 != null) SendFrame(cam2, camId: 2);
        if (cam3 != null) SendFrame(cam3, camId: 3);
    }

    void SendFrame(RenderTexture rt, byte camId)
    {
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToJPG(85);
        Destroy(tex);

        stream.WriteByte(camId);
        byte[] header = System.BitConverter.GetBytes(bytes.Length);
        stream.Write(header, 0, 4);
        stream.Write(bytes, 0, bytes.Length);
    }
}
