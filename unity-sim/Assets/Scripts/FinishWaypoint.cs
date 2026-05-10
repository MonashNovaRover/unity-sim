using UnityEngine;
using TMPro;

public class FinishWaypoint : MonoBehaviour
{
    public static FinishWaypoint Instance;
    public float activationDelay = 10f;
    private bool activate = false;

    void Start()
    {
        Instance = this;
    }

    public void StartActivationCountdown()
    {
        Invoke(nameof(EnableFinish), activationDelay);
    }

    void EnableFinish()
    {
        activate = true;
        Debug.Log("Finish trigger enabled");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log("Triggered by: " + other.name + " with tag: " + other.tag);

        if (!activate) return;

        if (other.CompareTag("robot"))
        {
            Debug.Log("Rover detected");
            RaceTimer.Instance.StopTimer();
        }
    }
}