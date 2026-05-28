using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PathRecorder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the rover here IF it exists at edit time. Otherwise leave empty and use 'Find By Name'.")]
    public Transform rover;

    [Header("Auto-Find (for runtime-spawned rovers)")]
    [Tooltip("If rover is spawned at runtime, the script will search for a GameObject containing this name. Leave empty to disable.")]
    public string findByName = "Banksia_FYP";

    [Tooltip("How long to keep searching for the rover after Start (seconds)")]
    public float findTimeout = 10f;

    [Header("Recording Settings")]
    [Tooltip("Seconds between samples. 0.1 = 10 samples per second")]
    public float sampleInterval = 0.1f;

    // [Tooltip("Skip sample if rover moved less than this since last sample (meters)")]
    // public float minDistanceBetweenSamples = 0.05f;

    [Tooltip("Start recording automatically when sim starts (and rover is found)?")]
    public bool autoStart = false; // for testing can change to true

    [Header("Visual Trail")]
    [Tooltip("LineRenderer that draws the trail. Optional.")]
    public LineRenderer trailLine;

    [Tooltip("Lift the line above the terrain by this much (meters)")]
    public float trailHeightOffset = 0.3f;

    [Tooltip("Show the trail while driving?")]
    public bool showTrailWhileDriving = true;

    // Each recorded point
    [System.Serializable]
    public struct PathSample
    {
        public Vector3 position;
        public Quaternion rotation;
        public float timestamp;
        public float speed;
    }

    private List<PathSample> samples = new List<PathSample>();
    private float timer = 0f;
    private bool recording = false;

//     void OnGUI()
// {
//     GUI.Label(new Rect(10, 10, 800, 20),
//         $"Rover: {(rover != null ? rover.name : "NULL")}");
//     GUI.Label(new Rect(10, 30, 800, 20),
//         $"Recording: {recording}, Samples: {samples.Count}");
//     GUI.Label(new Rect(10, 50, 800, 20),
//         $"TrailLine: {(trailLine != null ? "OK" : "NULL")}, " +
//         $"enabled: {(trailLine != null && trailLine.enabled)}, " +
//         $"positions: {(trailLine != null ? trailLine.positionCount : 0)}");
//     if (rover != null)
//     {
//         GUI.Label(new Rect(10, 70, 800, 20),
//             $"Rover world pos: X={rover.position.x:F2} Y={rover.position.y:F2} Z={rover.position.z:F2}");
//         GUI.Label(new Rect(10, 90, 800, 20),
//             $"Rover local pos: X={rover.localPosition.x:F2} Y={rover.localPosition.y:F2} Z={rover.localPosition.z:F2}");
//     }
//     if (samples.Count > 0)
//     {
//         var first = samples[0].position;
//         var last = samples[samples.Count - 1].position;
//         GUI.Label(new Rect(10, 110, 800, 20),
//             $"First sample: ({first.x:F2}, {first.y:F2}, {first.z:F2})");
//         GUI.Label(new Rect(10, 130, 800, 20),
//             $"Last sample:  ({last.x:F2}, {last.y:F2}, {last.z:F2})");
//     }
// }

    void Start()
    {
        if (rover == null && !string.IsNullOrEmpty(findByName))
        {
            StartCoroutine(FindRoverCoroutine());
        }
        else if (rover == null)
        {
            Debug.LogError("PathRecorder: rover not assigned and no findByName set!");
            enabled = false;
        }
        else
        {
            if (autoStart) StartRecording();
        }
    }

    IEnumerator FindRoverCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < findTimeout)
        {
            var all = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t.name.Contains(findByName))
                {
                    rover = t;
                    Debug.Log($"PathRecorder: found rover '{t.name}' after {elapsed:F2}s");
                    if (autoStart) StartRecording();
                    yield break;
                }
            }
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }
        Debug.LogError($"PathRecorder: couldn't find a GameObject containing '{findByName}' within {findTimeout}s");
        enabled = false;
    }

    void Update()
    {
        // Toggle trail visibility with T
        if (Input.GetKeyDown(KeyCode.T))
            SetTrailVisible(!showTrailWhileDriving);

        // Ctrl+S to stop recording and save
        // if (Input.GetKeyDown(KeyCode.S) && Input.GetKey(KeyCode.LeftControl))
        // {
        //     StopRecording();
        //     SaveToCSV();
        // }

        if (rover == null) return;
        if (!recording) return;

        timer += Time.deltaTime;
        if (timer < sampleInterval) return;
        timer = 0f;

        // Skip if rover hasn't moved meaningfully
        // if (samples.Count > 0)
        // {
        //     float dist = Vector3.Distance(samples[samples.Count - 1].position, rover.position);
        //     if (dist < minDistanceBetweenSamples) return;
        // }

        float speed = 0f;
        if (samples.Count > 0)
        {
            var prev = samples[samples.Count - 1];
            float dt = Time.time - prev.timestamp;
            if (dt > 0) speed = Vector3.Distance(prev.position, rover.position) / dt;
        }

        samples.Add(new PathSample
        {
            position = rover.position,
            rotation = rover.rotation,
            timestamp = Time.time,
            speed = speed
        });
    }

    void LateUpdate()
    {
        if (trailLine == null) return;
        trailLine.enabled = showTrailWhileDriving;
        if (!showTrailWhileDriving) return;

        trailLine.positionCount = samples.Count;
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 p = samples[i].position;
            p.y += trailHeightOffset;
            trailLine.SetPosition(i, p);
        }
    }

    public void StartRecording()
    {
        samples.Clear();
        timer = 0f;
        recording = true;
        Debug.Log("PathRecorder: recording started");
    }

    public void StopRecording()
    {
        recording = false;
        Debug.Log($"PathRecorder: recording stopped, {samples.Count} samples captured");
    }

    public bool IsRecording() => recording;
    public List<PathSample> GetSamples() => samples;

    public void SetTrailVisible(bool visible)
    {
        showTrailWhileDriving = visible;
        if (trailLine != null) trailLine.enabled = visible;
    }

    public void SaveToCSV(string filename = "3_Mast.csv")
    {
        string folder = Path.Combine(Application.dataPath, "PathTracking");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, filename);
        
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("time,x,z,rotY,speed");
            
            // saved run time starts at 0, not elapsed sim time
            float t0 = samples.Count > 0 ? samples[0].timestamp : 0f;

            foreach (var s in samples)
            {
                var e = s.rotation.eulerAngles;
                writer.WriteLine(
                    $"{(s.timestamp - t0):F4},{s.position.x:F4},{s.position.z:F4}," +
                    $"{e.y:F4},{s.speed:F4}");
            }
        }
        Debug.Log($"PathRecorder: saved {samples.Count} samples to {path}");
    }
}
