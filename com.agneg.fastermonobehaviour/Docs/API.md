# FasterMonoBehaviour - API Documentation

> [Readme](../../README.md)
---

## Table of Contents
- [Architecture Overview](#architecture-overview)
- [FasterMonoBehaviour.cs](#fastermonobehaviourcs)
  - [`IFasterEventProvider`](#ifastereventprovider)
  - [`IFasterUpdate`](#ifasterupdate)
  - [`IFasterFixedUpdate`](#ifasterfixedupdate)
  - [`IFasterLateUpdate`](#ifasterlateupdate)
  - [`FasterMonoBehaviour`](#fastermonobehaviour-class)
- [EventOrder.cs](#eventordercs)
- [WeightedList.cs](#weightedlistcs) 
  - [`EventWeightedList<TArg>`](#eventweightedlisttarg)
  - [`WeightedList<T>`](#weightedlistt)
- [SystemEvent.cs](#systemeventcs)
- [Usage Guide](#usage-guide)

---

## Architecture Overview

```
FasterMonoBehaviour (base class)
   │  implements one or more of:
   ▼
IFasterUpdate / IFasterFixedUpdate / IFasterLateUpdate
   │  on OnEnable(), registers callback + EventOrder weight into:
   ▼
SystemEvent.OnUpdate / OnFixedUpdate / OnLateUpdate
   │  each is an:
   ▼
EventWeightedList<float>
   │  internally backed by:
   ▼
WeightedList<Action<float>>
```

The core idea: each `FasterMonoBehaviour` registers a delegate into a shared, sorted list. `SystemEventHelper` calls `SystemEvent.OnUpdate.Invoke(Time.deltaTime)` (and the Fixed/Late equivalents) once per frame, firing every registered callback **in weight order**.

---

## FasterMonoBehaviour.cs
### `FasterMonoBehaviour` (class)

```csharp
public class FasterMonoBehaviour : MonoBehaviour
```

The base class your components inherit from instead of `MonoBehaviour` directly. It automatically wires up registration/deregistration with `SystemEvent` based on which `IFaster*` interfaces the subclass implements.

#### Methods

**`protected virtual void OnEnable()`**

For each interface implemented, registers the corresponding method (`SystemUpdate`, `SystemFixedUpdate`, or `SystemLateUpdate`), using `updateOrder` as the weight.

**`protected virtual void OnDisable()`**

Mirrors `OnEnable()` - removes the corresponding registered callback(s) from `SystemEvent` for each `IFaster*` interface implemented.

> Both methods are `virtual`, so subclasses can override and extend them - but **must call `base.OnEnable()` / `base.OnDisable()`** to preserve registration behavior, since the pattern relies on these exact methods running.

### `IFasterEventProvider`

Base interface providing the ordering weight used by all "Faster" update interfaces.

```csharp
public interface IFasterEventProvider
{
    EventOrder updateOrder { get; }
}
```

> **Don't implement this directly:** use one of the child interfaces (see [IFasterUpdate](#ifasterupdate), [IFasterFixedUpdate](#ifasterFixedUpdate)).  Implementing any of those will require you to add the `updateOrder` property, which the system needs to order your event.

| Member | Type | Description |
|---|---|---|
| `updateOrder` | `EventOrder` (get) | The phase/weight used to determine when this object's callback fires relative to others (see [`EventOrder`](#eventordercs)). |

### `IFasterUpdate`

```csharp
public interface IFasterUpdate : IFasterEventProvider
{
    void SystemUpdate(float delta);
}
```
Implement this to receive a per-frame update callback (analogous to Unity's `Update()`).

### `IFasterFixedUpdate`

```csharp
public interface IFasterFixedUpdate : IFasterEventProvider
{
    void SystemFixedUpdate(float delta);
}
```
Analogous to Unity's `FixedUpdate()`.

### `IFasterLateUpdate`

```csharp
public interface IFasterLateUpdate : IFasterEventProvider
{
    void SystemLateUpdate(float delta);
}
```
Analogous to Unity's `LateUpdate()`.

---

## EventOrder.cs

```csharp
public enum EventOrder
{
    First = 0,
    Networking = 1000,
    Normal = 2000,
    UI = 3000
}
```

An enum of named "phases" used as the weight value passed to `Add` throughout the system. Lower numeric value = fires earlier in a given update tick.

| Value | Number | Typical use |
|---|---|---|
| `First` | `0` | Logic that must run before anything else this tick. |
| `Networking` | `1000` | Network sync / receive logic - early, but after critical setup. |
| `Normal` | `2000` | General gameplay logic - the default "everything else" phase. |
| `UI` | `3000` | UI updates - last, so UI reflects the final state of the frame. |

These named values exist purely for convenience. Since `EventWeightedList`/`WeightedList` just cast `EventOrder` to `int` and sort numerically, you can pass any arbitrary `int` (via an explicit cast) to slot a callback precisely between two named phases - e.g. `(EventOrder)1500` would run after `Networking` but before `Normal`.  (See [EventWeightedList](#EventWeightedListTArg) for more)

---

## WeightedList.cs

### `EventWeightedList<TArg>`

A weighted, ordered event/delegate dispatcher for `Action<TArg>` callbacks. Wraps a `WeightedList<Action<TArg>>` and adds **safe mutation during iteration** - callbacks can add/remove/clear subscriptions mid-invocation without throwing a "Collection was modified" exception.

Anything with a different weight fires in the correct order (lower weight = fires earlier); items sharing the same weight may fire in any order relative to each other.

```csharp
public class EventWeightedList<TArg>
```

#### Constructor

**`EventWeightedList()`**
Initializes the internal `WeightedList<Action<TArg>>`.

#### Methods

**`void Invoke(TArg arg)`**
Fires all subscribed callbacks, in weight order, passing `arg` to each.

Internally:
1. Processes any pending removes, unique-adds, and adds that were queued during a *previous* invocation (see "Safety-related adding/removing" below).
2. Iterates `itemEnumerables` and invokes each delegate. Each invocation is wrapped in a `try/catch`; exceptions are caught individually and logged via `Debug.LogError` (with stack trace) so one failing listener doesn't stop the rest from running.

**`void Add(Action<TArg> newDelegate, EventOrder eventOrderIndex)`**
Adds `newDelegate` (duplicates allowed), ordered by `eventOrderIndex`.
- If called while `Invoke` is currently iterating (`inLoop == true`), the request is **cached** and applied after the current invocation finishes.
- Otherwise, applied immediately to the underlying `WeightedList`.

**`void Remove(Action<TArg> oldDelegate)`**
Removes `oldDelegate` from the list.
- If called mid-invocation, the delegate's *current index* is recorded and queued for removal after the invocation completes (rather than removing immediately), to avoid mutating the list while it's being iterated.
- If called outside an invocation, removed immediately.

**`void Clear()`**
Removes all subscribed callbacks.
- If called mid-invocation: queues every current index for removal (effectively clearing once the in-progress `Invoke` finishes), and discards any pending add requests.
- If called outside an invocation: clears the underlying list and all pending add/remove caches immediately.

> **Behavior to note:** Queued adds/removes from one `Invoke()` call are not applied until the *start* of the *next* `Invoke()` call - not immediately after the current one finishes. If `Invoke` isn't called again, queued changes never take effect.  But: if you never run the event again, they won't fire anyway, so nothing lost.

---

### `WeightedList<T>`

A generic, **insertion-sorted-by-weight** list. Lower weight values are stored earlier in the list; items are kept sorted at all times as they're added.

```csharp
public class WeightedList<T>
```

#### Properties

| Property | Type | Description |
|---|---|---|
| `HasItems` | `bool` | `true` if the list contains at least one item. |
| `Count` | `int` | Number of items currently in the list. |
| `itemEnumerables` | `IEnumerable<T>` | A read-only view of the items, in weight-sorted order. |

#### Methods

**`bool Contains(T item)`**
Returns whether `item` already exists in the list (uses `List<T>.Contains`, i.e. default equality comparison).

**`int IndexOf(T item)`**
Returns the index of `item`, or `-1` if not found.

**`void Add(T item, int weight)`**
Inserts `item` into the list at the correct sorted position based on `weight`:
- If no valid position is found (should not normally happen), logs an error via `Debug.LogError` and the item is **not added**.

**`void Remove(T item)`**
Removes the first occurrence of `item` (and its associated weight) from the list. No-op if not found.

**`void RemoveAt(int index)`**
Removes the item (and weight) at `index`. Silently does nothing if `index` is out of bounds.

**`void Clear()`**
Empties both the items and weights lists.

---

## SystemEvent.cs

A static hub holding the four shared `EventWeightedList<float>` instances that drive the entire system, plus bootstrap logic to ensure a driver object exists in the scene.

```csharp
public static class SystemEvent
```

#### Static Fields

| Field | Type | Description |
|---|---|---|
| `OnNextUpdate` | `EventWeightedList<float>` | One-shot dispatch list. Runs everything subscribed to it on the next update **before** `OnUpdate`, then clears.|
| `OnUpdate` | `EventWeightedList<float>` | Fires on every update tick, in `EventOrder` weight order. |
| `OnFixedUpdate` | `EventWeightedList<float>` | Fires on every fixed-update tick, in `EventOrder` weight order. |
| `OnLateUpdate` | `EventWeightedList<float>` | Fires on every late-update tick, in `EventOrder` weight order. |

> `SystemEvent` uses an automatically-instantiated GameObject with a `SystemEventHelper` applied to receive the initial Unity event ticks.  `SystemEventHelper` contains a few settings related to functionality, but 99% of the time you're never going to look at it or notice it's there.

---

## Usage Guide

**1. Create a component that needs ordered updates:**

```csharp
public class Player : FasterMonoBehaviour, IFasterUpdate, IFasterLateUpdate
{
    public EventOrder updateOrder => EventOrder.Normal;

    public void SystemUpdate(float delta)
    {
        // movement logic, runs in the Normal phase (weight 2000)
    }

    public void SystemLateUpdate(float delta)
    {
        // camera-follow logic, runs in LateUpdate's Normal phase
    }
}
```

**2. You don't need to call `SystemEvent` methods manually for registration** - inheriting from `FasterMonoBehaviour` handles `Add`/`Remove` automatically via `OnEnable`/`OnDisable`.

**3. If overriding `OnEnable`/`OnDisable`, always call the base implementation:**

```csharp
protected override void OnEnable()
{
    base.OnEnable(); // required - registers with SystemEvent
    // your additional logic
}
```

**4. Pick the right `EventOrder` phase for your logic:**

| Phase | When to use it |
|---|---|
| `First` (0) | Setup or state that everything else this tick depends on. |
| `Networking` (1000) | Applying incoming network state before gameplay reacts to it. |
| `Normal` (2000) | The default - most gameplay logic belongs here. |
| `UI` (3000) | Anything that should reflect the final state of the frame. |

You can also bypass the named values entirely and cast an arbitrary `int` to `EventOrder` to slot precisely between two phases.

**5. If you manually subscribed to a SystemEvent update,** feel free to manually subscribe/unsubscribe from within the event itself. `EventWeightedList` defers `Add`/`Remove`/`Clear` calls made during an active `Invoke()` until the *next* `Invoke()` call begins.
