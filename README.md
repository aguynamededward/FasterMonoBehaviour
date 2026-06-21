*Developed in Unity 6000.4.5, but should be backwards-compatible to most Unity versions starting in 2021.*

> [API Reference](/com.agneg.fastermonobehaviour/Docs/API.md)

# FasterMonoBehaviour

*A faster, more flexible way of processing UnityEngine update messages at scale.*

`FasterMonoBehaviour` allows you to:

- Run Unity-equivalent update loops with much less overhead.
- Custom tweak your script's execution order to _exactly_ when you want it to run.
- Receive Time.deltaTime automatically as an _argument_ in your update loop, _how incredible_.
- Maybe that last one is just for me.

## Table of Contents

- [Installation](#installation)
- [How to Implement](#how-to-implement)
- [Sample Implementation](#sample-implementation)
- [API Reference](#api-reference)
- [Under the Hood](#under-the-hood)
- [Event Order](#event-order)
- [Additional Feature(s)](#additional-features)
- [Special notes:](#special-notes)

## Installation

1) Download the files directly from https://github.com/aguynamededward/FasterMonoBehaviour and copy into your project.
2) Unity's Package Manager: use the "install via git URL" option and paste in this address: https://github.com/aguynamededward/FasterMonoBehaviour.git?path=/com.agneg.fastermonobehaviour

> Please note: FasterMonoBehaviour is still in alpha form.  Some APIs may change / break over the course of development.

## How to Implement
1. Wherever you'd inherit from `MonoBehaviour`, inherit from `FasterMonoBehaviour`.
2. Implement any or all of the `IFasterEventProvider` interfaces inside your `FasterMonoBehaviour` script. (See MonoBehaviour to FasterMonoBehaviour below)
3. Implementing the interface will also implement the `updateOrder` property, which is an `EventOrder` enum. This tells the `FasterMonoBehaviour` system *where in the execution order* of the particular update call you'd like to run this script.  See [Event Order](#event-order) below.

### MonoBehaviour to FasterMonoBehaviour  
```
Unity-equivalent       Interface to Implement        Method call
Update()          =>   IFasterUpdate          =>     SystemUpdate(float deltaTime)
FixedUpdate()     =>   IFasterFixedUpdate     =>     SystemFixedUpdate(float deltaTime)
LateUpdate()      =>   IFasterLateUpdate      =>     SystemLateUpdate(float deltaTime)

// all of the above use the same updateOrder property:
public EventOrder updateOrder { get; }
```

## Sample Implementation:

```csharp
public class SampleFasterUpdateLoop : FasterMonoBehaviour, IFasterUpdate
{
    public EventOrder updateOrder => EventOrder.UI;

    public void SystemUpdate(float delta)
    {
        Debug.Log("This will update along with everything else in the UI step.");
    }
}
```
## Under the Hood
`FasterMonoBehaviour` is powered primarily by `SystemEvent`, a collection of sorted lists (`EventWeightedList<float>`) that handle subscription, sorting, and safe unsubscription.

What that means is: you can actually skip `FasterMonoBehaviour` all together and interact directly with SystemEvent if you want.

### Example: Directly subscribing/unsubscribing to SystemEvent updates:
```csharp
public class MySameOlMonoBehaviour : MonoBehaviour

private void MyFixedUpdate(float deltaTime)
{
    // doing some work here
}

void OnEnable()
{
    SystemEvent.OnFixedUpdate.Add(MyFixedUpdate,EventOrder.First); // this will be among the very first events to fire during FixedUpdate
}

void OnDisable()
{
    SystemEvent.OnFixedUpdate.Remove(MyFixedUpdate);
}
```

### Expand to Add Your Own Sorted Event Callbacks
#### Add a new SystemEvent
```csharp
// inside SystemEvent.cs
public static EventWeightedList<float> OnUpdate = new();

public static EventWeightedList<float> MyOwnUpdate = new();

// somewhere, subscribing to it:
SystemEvent.MyOwnUpdate.Add(MyMethod,EventOrder.Normal);
// always unsubscribe
SystemEvent.MyOwnUpdate.Remove(MyMethod);

// then, somwhere else, calling it during an update:
SystemEvent.MyOwnUpdate.Invoke(Time.deltaTime);
```
Or you could go entirely mad with power, hooking your _new_ event up to `SystemUpdate` to be called _exactly_ at the end of all the _regular_ updates:

```csharp
// In some FasterMonoBehaviour somewhere
public static bool subscribedYet = false;
public void OnEnable()
{
    if(subscribedYet == false)
    {
        SystemEvent.OnUpdate.Add(SystemEvent.MyOwnUpdate.Invoke,(EventOrder)int.MaxValue); // maxValue = very last called
        subscribedYet = true;
    }
}

public void OnDisable()
{
    if(subscribedYet)
    {
        SystemEvent.OnUpdate.Remove(SystemEvent.MyOwnUpdate.Invoke);
        subscribedYet = false;
    }
}
```

#### Adding New Event Callbacks entirely
You can take it one step further.  Each `SystemEvent` update is just an `EventWeightedList<float>` that takes `Time.deltaTime` as a single argument.

You could create _entirely new_ callback systems that take different arguments and do different work:
```csharp
// when the world state changes
public static EventWeightedList<WorldStateStruct> OnWorldStateChanged = new();

// Passing in a serializer so multiple systems can save game state
public static EventWeightedList<SaveSerializer> OnSaveRequested = new();

// Allowing all interested parties to scritch a Game Object
public static EventWeightedList<GameObject> PleaseScritchThisGameObject = new();
```

## <a name="event-order" /> Event Order
`FasterMonoBehaviour` supports several separate update calls (`SystemUpdate`, `SystemFixedUpdate`,`SystemLateUpdate`, etc).

Under the hood, each of these updates is a separate list of events that can be subscribed to/unsubscribed from, at will.

Additionally, you can specify the _event order_, which is where your specific script should be fired _within_ that event list.

```csharp
public enum EventOrder
{
    First = 0,
    Networking = 1000,        // parse your network messages before applying any game logic
    Normal = 2000,            // run update methods as usual
    UI = 3000                 // update any UI elements waiting on changed state from the game logic
}
```
You can supply custom values in the enum, or just calculate them directly (`EventOrder.Normal + 5`) as you need. The `SystemEvent` will accept any int-value cast to `EventOrder`.

### EventOrder Sorts **Within** An Event, not Across Multiple Events
If you implement `SystemUpdate(float)` in `FasterMonoBehaviour`, you can supply any value as `updateOrder`, and it will be properly sorted and called in its time during the Update step.

But *all* `SystemUpdate(float)` events will *always* run before anything using `SystemLateUpdate(float)` is called, regardless of the `EventOrder` index.

If two scripts have the _same_ value, which script fires first is not determinable. But they are all guaranteed to run before any scripts with a higher index.

#### Example:
```csharp
// ScriptA:
    public EventOrder updateOrder => (EventOrder)int.MaxValue; // maxValue: will fire very last in the Update loop
    SystemUpdate(float deltaTime)
    {
        // runs before ScriptB
    }

// ScriptB:
    public EventOrder updateOrder => (EventOrder)int.MinValue; // minValue: will always fire first
    SystemLateUpdate(float deltaTime)
    {
        // LateUpdate *still* runs after Update
    }
    
// ScriptC:
    public EventOrder updateOrder => (EventOrder)int.MaxValue; // maxValue: same as ScriptA
    SystemUpdate(float deltaTime)
    {
        // ScriptA may or may not fire before ScriptC, as they are the same sorting value.
        // Both will fire before ScriptB.
    }
```
## Additional Features:
### Update Static Classes / Non-MonoBehaviours
The core of `FasterMonoBehaviour` is a set of sorted event lists (`SystemEvent`), so you can feel free to add any methods you'd like called, so long as they match the `MethodName(float deltaTime)` signature.

**Make sure** to unsubscribe them when they are no longer needed / being destroyed.  `SystemEvent` implements safeguards to keep any method from crashing the stack, but you'll lose a lot of performance if it happens.

#### Example:
I like subscribing static methods to `SystemEvent` when I need boilerplate work to happen over a period of time, and then unsubscribing when there is no more work left to do.
```csharp
public static UtilScript
{
    public static StartWork(Argument1)
    {
        if(totalWorkBeingDone == 0)
        {
            // subscribe to run update after the Normal scripts run, but before the UI scripts.
            SystemEvent.OnUpdate.Add(DoWork,EventOrder.Normal + 10);
        }
        totalWorkBeingDone++;
    }

    private static void DoWork(float deltaTime)
    {
        // do some work
    }

    public static void StopWork(Argument1)
    {
        totalWorkBeingDone--;

        if(totalWorkBeingDone <= 0)
        {
            // no more work to do, let's unsubscribe
            SystemEvent.OnUpdate.Remove(DoWork);
        }
    }
}
```

### OnNextUpdate
The `OnNextUpdate` is similar to the `Update` method, except it will only fire *once*, and then never again.  Useful when you need to come back next frame for something, but you don't want to deal with the cost of a coroutine. 

(Note: `OnNextUpdate` is an entirely separate event list that runs **before** the regular update loop occurs.)

#### Example:
```csharp
// subscribe to run update after the Normal scripts run, but before the UI scripts.
SystemEvent.OnNextUpdate.Add(MethodName,EventOrder.Normal + 10);

// the method will be called exactly *once*
// no unsubscription necessary, as the event list is cleared every time
```

## Special notes:
### OnEnable / OnDisable
`FasterMonoBehaviour` does its subscription logic in `OnEnable`/`OnDisable`, so if you also want to use those, make sure to call `base.OnEnable` / `base.OnDisable` before running your own logic.

### A few Toggleables
There are currently a couple "quality of life" booleans in the `SystemEventHelper` script that you can edit.
  - `DontUpdateOnZeroDelta` (defaults to 'true') - skips running an update if `Time.deltaTime` is 0f.
  - `HideInInspector` (defaults to 'true') - Hides `FasterMonoBehaviour`'s event calling mechanism in the editor. (_You probably want to leave this, but if you're trying to ensure the object is created, you can turn this off to see_)
