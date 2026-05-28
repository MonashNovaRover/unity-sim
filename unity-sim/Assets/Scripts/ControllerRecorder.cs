using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerRecorder : MonoBehaviour
{
    [Header("Recording")]
    [Tooltip("Minimum change in an axis value to count as a new event")]
    public float axisThreshold = 0.02f;

    [Tooltip("Which joystick buttons to watch (0-19). Default covers common face/shoulder buttons.")]
    public int maxButtonsToWatch = 20;

    [System.Serializable]
    public struct InputEvent
    {
        public float timestamp;
        public string control;   // e.g. "Horizontal", "Vertical", "Button0"
        public float value;      // axis value, or 1/0 for button down/up
    }

    private List<InputEvent> events = new List<InputEvent>();
    private bool recording = false;

    // Track previous values so we only log changes
    private Dictionary<string, float> prevValues = new Dictionary<string, float>();

    // Axis names from the legacy Input Manager. These are the defaults that
    // exist in every Unity project. Add custom ones if your project defines them.
    private string[] axesToWatch = { "Horizontal", "Vertical" };

    void Update()
    {
        if (!recording) return;
        RecordNewInputSystem();
    }

    float SafeGetAxis(string axis)
    {
        try { return Input.GetAxis(axis); }
        catch { return 0f; }
    }

    void TryWatchAxis(string axis)
    {
        float val;
        try { val = Input.GetAxis(axis); }
        catch { return; }   // axis not defined in Input Manager, skip silently

        if (!prevValues.TryGetValue(axis, out float prev) ||
            Mathf.Abs(val - prev) >= axisThreshold)
        {
            events.Add(new InputEvent { timestamp = Time.time, control = axis, value = val });
            prevValues[axis] = val;
        }
    }

    public void StartRecording()
    {
        events.Clear();
        prevValues.Clear();
        recording = true;
        Debug.Log("ControllerRecorder: recording started");
    }

    public void StopRecording()
    {
        recording = false;
        Debug.Log($"ControllerRecorder: recording stopped, {events.Count} events captured");
    }

    public void SaveToCSV(string filename = "controller_inputs.csv")
    {
        string folder = Path.Combine(Application.dataPath, "InputTracking");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, filename);

        // Zero the timestamps so they start at 0 (matches race start)
        float t0 = events.Count > 0 ? events[0].timestamp : 0f;

        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("time,control,value");
            foreach (var e in events)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F4},{1},{2:F4}", e.timestamp - t0, e.control, e.value));
            }
        }
        Debug.Log($"ControllerRecorder: saved {events.Count} events to {path}");

        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }

    void RecordNewInputSystem()
    {
        var gp = Gamepad.current;
        if (gp == null) return;

        // Sticks (Vector2, each component -1..1)
        Vector2 left = gp.leftStick.ReadValue();
        Vector2 right = gp.rightStick.ReadValue();

        LogIfChanged("LeftStickX", left.x);
        LogIfChanged("LeftStickY", left.y);
        LogIfChanged("RightStickX", right.x);
        LogIfChanged("RightStickY", right.y);

        // Triggers (0..1)
        LogIfChanged("LeftTrigger", gp.leftTrigger.ReadValue());
        LogIfChanged("RightTrigger", gp.rightTrigger.ReadValue());

        // D-pad (each direction is a button: 0 or 1)
        LogIfChanged("DPadUp", gp.dpad.up.isPressed ? 1f : 0f);
        LogIfChanged("DPadDown", gp.dpad.down.isPressed ? 1f : 0f);
        LogIfChanged("DPadLeft", gp.dpad.left.isPressed ? 1f : 0f);
        LogIfChanged("DPadRight", gp.dpad.right.isPressed ? 1f : 0f);

        // Face buttons (DS4 names)
        LogIfChanged("Cross", gp.buttonSouth.isPressed ? 1f : 0f);
        LogIfChanged("Circle", gp.buttonEast.isPressed ? 1f : 0f);
        LogIfChanged("Square", gp.buttonWest.isPressed ? 1f : 0f);
        LogIfChanged("Triangle", gp.buttonNorth.isPressed ? 1f : 0f);

        // Shoulders
        LogIfChanged("L1", gp.leftShoulder.isPressed ? 1f : 0f);
        LogIfChanged("R1", gp.rightShoulder.isPressed ? 1f : 0f);

        // Stick presses
        LogIfChanged("L3", gp.leftStickButton.isPressed ? 1f : 0f);
        LogIfChanged("R3", gp.rightStickButton.isPressed ? 1f : 0f);

        // Options / Share / Start
        LogIfChanged("Options", gp.startButton.isPressed ? 1f : 0f);
        LogIfChanged("Share", gp.selectButton.isPressed ? 1f : 0f);
    }

    void LogIfChanged(string control, float value)
    {
        if (!prevValues.TryGetValue(control, out float prev) ||
            Mathf.Abs(value - prev) >= axisThreshold)
        {
            events.Add(new InputEvent { timestamp = Time.time, control = control, value = value });
            prevValues[control] = value;
        }
    }
}