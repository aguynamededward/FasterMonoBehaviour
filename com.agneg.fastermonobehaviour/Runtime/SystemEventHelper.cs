using UnityEngine;
/// <summary>
/// Phases added for ease of use.  You can pass in direct values if you prefer to specifically set certain classes to run at certain times.
/// </summary>
public enum EventOrder
{
    First = 0,
    Networking = 1000,
    Normal = 2000,
    UI = 3000
}

/// <summary>
/// Runs the update loops in the background.
/// </summary>
public class SystemEventHelper : MonoBehaviour
{
    // Settings
    /// <summary>
    /// Change this to 'false' if you want to receive updates even if the deltaTime == 0.
    /// </summary>
    private const bool DontUpdateOnZeroDelta = true;
    /// <summary>
    /// If false, the SystemEventHelper still won't be editable or saved in hierarchy on close, but you'll be able to see it.
    /// </summary>
    public const bool HideInInspector = true;



    public static SystemEventHelper instance;
    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (DontUpdateOnZeroDelta && Mathf.Approximately(deltaTime, 0))
        {
            return;
        }

        SystemEvent.OnNextUpdate.Invoke(deltaTime);
        SystemEvent.OnNextUpdate.Clear();

        SystemEvent.OnUpdate.Invoke(deltaTime);
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (DontUpdateOnZeroDelta && Mathf.Approximately(deltaTime, 0))
        {
            return;
        }

        SystemEvent.OnFixedUpdate.Invoke(deltaTime);
    }
    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (DontUpdateOnZeroDelta && Mathf.Approximately(deltaTime, 0))
        {
            return;
        }

        SystemEvent.OnLateUpdate.Invoke(deltaTime);
    }
}
