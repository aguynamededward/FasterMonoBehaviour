using System;
using UnityEngine;
public static class SystemEvent
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeSystemEvent()
    {
        if(SystemEventHelper.instance == null)
        {
            var newObj = new GameObject();
            var helperComponent = newObj.AddComponent<SystemEventHelper>();
            SystemEventHelper.instance = helperComponent;

            newObj.hideFlags = (SystemEventHelper.HideInInspector ? HideFlags.HideAndDontSave : (HideFlags.DontSave | HideFlags.NotEditable));
            
            GameObject.DontDestroyOnLoad(newObj);
        }
    }

    /// <summary>
    /// Runs everything subscribed to it on the next update, and then clears the list.<br></br>
    /// <b>NOTE:</b> Runs BEFORE OnUpdate
    /// </summary>
    public static EventWeightedList<float> OnNextUpdate = new();
    public static EventWeightedList<float> OnUpdate = new();
    public static EventWeightedList<float> OnFixedUpdate = new();
    public static EventWeightedList<float> OnLateUpdate = new();
}