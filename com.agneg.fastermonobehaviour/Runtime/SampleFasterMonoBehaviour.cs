using UnityEngine;

public class SampleFasterMonoBehaviour : FasterMonoBehaviour, IFasterUpdate
{
    public EventOrder updateOrder => EventOrder.UI;

    public void SystemUpdate(float delta)
    {
        Debug.Log("This will only update after everything else has.");
    }
}