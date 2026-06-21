using UnityEngine;


public interface IFasterEventProvider
{
    public EventOrder updateOrder { get; }
}
public interface IFasterUpdate : IFasterEventProvider
{
    public void SystemUpdate(float delta);
}

public interface IFasterFixedUpdate : IFasterEventProvider
{
    public void SystemFixedUpdate(float delta);
}

public interface IFasterLateUpdate : IFasterEventProvider
{
    public void SystemLateUpdate(float delta);
}

public class FasterMonoBehaviour : MonoBehaviour
{
    protected virtual void OnEnable()
    {
        if(this is IFasterUpdate fasterUpdate)
        {
            SystemEvent.OnUpdate.Add(fasterUpdate.SystemUpdate,fasterUpdate.updateOrder);
        }

        if (this is IFasterFixedUpdate fasterFixedUpdate)
        {
            SystemEvent.OnFixedUpdate.Add(fasterFixedUpdate.SystemFixedUpdate, fasterFixedUpdate.updateOrder);
        }
        
        if(this is IFasterLateUpdate fasterLateUpdate)
        {
            SystemEvent.OnLateUpdate.Add(fasterLateUpdate.SystemLateUpdate, fasterLateUpdate.updateOrder);
        }
    }

    protected virtual void OnDisable()
    {
        if (this is IFasterUpdate fasterUpdate)
        {
            SystemEvent.OnUpdate.Remove(fasterUpdate.SystemUpdate);
        }

        if (this is IFasterFixedUpdate fasterFixedUpdate)
        {
            SystemEvent.OnFixedUpdate.Remove(fasterFixedUpdate.SystemFixedUpdate);
        }

        if (this is IFasterLateUpdate fasterLateUpdate)
        {
            SystemEvent.OnLateUpdate.Remove(fasterLateUpdate.SystemLateUpdate);
        }
    }
}
