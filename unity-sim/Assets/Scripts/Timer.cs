using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    public static RaceTimer Instance;

    public float elapsedTime = 0f;
    private bool timer = false;
    public TMP_Text timerText;

    private void Start()
    {
        Instance = this;
        if (timerText != null)
        {
            timerText.text = "Press ENTER to start the timer";
        }
    }

    void Update()
    {
        if (!timer)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                timer = true;
                FinishWaypoint.Instance.StartActivationCountdown();
                Debug.Log("Timer Started");

                // path recorder
                var recorder = FindFirstObjectByType<PathRecorder>();
                if (recorder != null) recorder.StartRecording();
            }
            return;
        }

        // e-stop
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E-STOP triggered");
            StopTimer();
            return;
        }

        elapsedTime += Time.deltaTime;
        DisplayTime(elapsedTime);
    }

    void DisplayTime(float timeDisplay)
    {
        int minutes = Mathf.FloorToInt(timeDisplay / 60);
        int seconds = Mathf.FloorToInt(timeDisplay % 60);
        int milliseconds = Mathf.FloorToInt((timeDisplay * 1000) % 1000);

        if (timerText != null)
        {
            timerText.text = string.Format("{0:00}:{1:00}:{2:000}", minutes, seconds, milliseconds);
        }
    }

    public void StopTimer()
    {
        timer = false;
        Debug.Log("Final Time: " + elapsedTime.ToString("F2") + "seconds");

        // path recorder
        var recorder = FindFirstObjectByType<PathRecorder>();
        if (recorder != null)
        {
            recorder.StopRecording();
            recorder.SaveToCSV();
        }
    }
}