using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    public static RaceTimer Instance;

    public float elapsedTime = 0f;
    public bool timer = true;
    public TMP_Text timerText;

    private void Start()
    {
        Instance = this;
    }

    void Update()
    {
        if (!timer) return;

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
    }
}