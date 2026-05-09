using UnityEngine;
using TMPro;

public class FinishWaypoint : MonoBehaviour
{
    public float activationDelay = 5f;
    private bool activate = false;

    void Start()
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
        if (!activate) return;

        if (other.CompareTag("Rover"))
        {
            RaceTimer.Instance.StopTimer();
            Debug.Log("Crossed finish waypoint");
        }
    }
    // private void OnTriggerEnter(Collider other)
    // {
    //     Debug.Log("Triggered by: " + other.name);

    //     if (!activate) return;

    //     if (other.CompareTag("robot"))
    //     {
    //         Debug.Log("Robot detected");
    //         RaceTimer.Instance.StopTimer();
    //     }
    // }
}