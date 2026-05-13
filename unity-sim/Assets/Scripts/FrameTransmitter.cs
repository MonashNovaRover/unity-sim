using System.Collections;
using System.Net.Sockets;
using UnityEngine;

public class FrameTransmitter : MonoBehaviour
{
    public enum CameraMode
    {
        Single,
        All
    }

    [Header("Mode Selection")]
    public CameraMode mode = CameraMode.Single;

    [Header("Cameras")]
    public Camera cam0;
    public Camera cam1;
    public Camera cam2;
    public Camera cam3;
    public Camera camFloat;

    public RenderTexture cam0RT;
    public RenderTexture cam1RT;
    public RenderTexture cam2RT;
    public RenderTexture cam3RT;
    public RenderTexture camFloatRT;
    public int port = 5000;

    private TcpClient client;
    private NetworkStream stream;
    private bool connected = false;

    void Awake()
    {
        if (mode == CameraMode.Single)
        {
            cam0.enabled = false;
            cam1.enabled = false;
            cam2.enabled = false;
            cam3.enabled = false;
            camFloat.enabled = true;
        }
        else
        {
            cam0.enabled = true;
            cam1.enabled = true;
            cam2.enabled = true;
            cam3.enabled = true;
            camFloat.enabled = true;
        }
    }

    void Start()
    {
        Debug.Log($"Camera mode: {mode}");
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
        if (mode == CameraMode.All)
        {
            if (cam0RT != null) SendFrame(cam0RT, camId: 0);
            if (cam1RT != null) SendFrame(cam1RT, camId: 1);
            if (cam2RT != null) SendFrame(cam2RT, camId: 2);
            if (cam3RT != null) SendFrame(cam3RT, camId: 3);
        }
        else
        {
            if (camFloatRT != null) SendFrame(camFloatRT, camId: 0);
        }
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
