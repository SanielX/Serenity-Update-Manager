# CLR-Update-Manager
Update manager replacement for MonoBehvaiour in Unity Engine inspired by famous [article](https://blog.unity.com/technology/1k-update-calls) about 10 000 update calls. 
However, this implementation also offers additional features, such as:
* Aggregation by type. As discussed [here](https://www.youtube.com/watch?v=CBP5bpwkO54), calling same virtual function is much better for cache coherency and thus provides better performance.

    ![](./Git/SortExample.png)

    Notice how objects of type `MovableObject` are coupled toghether.
* Run.After and Run.Before attributes. Allows all instances of some class to be updated before instances of another class. Works only for between CLRScripts
* Nice profiling. Not only it shows nice stats in Profiler windows, but string are preallocated and thus, don't cause GCAllocs every frame.
    ![](./Git/ProfilerExample.png)
* New calls. Exposes PreUpdate and EarlyUpdate as virtual functions.

__Cons__:
* Update manager does not work with [ExecuteAlways]
* Since all update calls are now external, they can not be private. Currently are public but you can change all callbacks to be `internal protected`
* Also it is possible that FixedUpdate may be called before Start if object was Instantiated. This happens for default MonoBehaviours too by the way
* Every CLRScript has Awake, Start, OnEnable, OnDisable and OnDestroyed callbacks regardless of you using them
* Currently, CLRManager adds and removes script from its list every time you enable/disable component. So if you switch it every frame it may become a problem (May not, I didn't measure)

## Install
To install package just use PackageManager and download it through git url, or import package into your assets folder. 
Minimum supported unity version is 2021.1. I didn't check if it will work on older versions.

## Quick Start
To start using new manager you need to derive you script from `CLRScript` (in `HostGame` namespace). Now the only difference between this manager and MonoBehvaiour is that instead of typing `void Update` you need to use virtual methods so it'll look like `public override void OnUpdate()`. Just create the script and following example should work, no setup required:
```csharp
using HostGame;

class MyClass : CLRScript 
{
    public override void OnUpdate()
    {
        Debug.Log("Hello world!");
    }
}
```
There is also a file in Resources folder called `CLR_Ex_Order.asset`. You should add this file to your `.gitignore`, since this file is generated automatically each recompile.

_Note: Exceptions are handled if UNITY_ASSERTIONS is enabled. Otherwise, if any of your scripts throws whole loop will be aborted_

## Available methods
Next methods are just mimicing MonoBehaviour methods:
* OnAwake
* OnStart
* OnUpdate 
* OnFixedUpdate
* OnLateUpdate 
* OnEnabled
* OnDisabled
* OnDestroyed

Now next 2 are new to the system:
* OnEarlyUpdate - Called at the start of each frame even before FixedUpdate.
* OnPreUpdate - Called after FixedUpdate but before Update each frame.

To configure which functions are called you don't need to do anything. Currently, system checks which Update functions you implement using Reflection. If you don't override Setup method, system caches results into scriptable object, which is then used to build runtime dictionaries for per-type lookup.

### Advanced Setup
If you want to configure calls yourself, you may just override `Setup` function like this:
```csharp
// Update mode has Update, FixedUpdate, LateUpdate and manual
[SerializeField] UpdateMode m_UpdateMode = UpdateMode.LateUpdate;

public override CLRSetupFlags Setup()
{
    CLRSetupFlags result = 0;

    // Should components of this game object be cached into global dictionary?
    // It does by default
    result |= CLRSetupFlags.DontCacheComponents;

    // Construct enum with calls 
    switch (m_UpdateMode)
    {
        case UpdateMode.Update:
            result |= CLRSetupFlags.Update;
            break;
        case UpdateMode.FixedUpdate:
            result |= CLRSetupFlags.FixedUpdate;
            break;
        case UpdateMode.LateUpdate:
            result |= CLRSetupFlags.LateUpdate;
            break;
        case UpdateMode.Manual:
            break;
    }

    // return your settings
    // If safety checks are false, CLRManager won't check whether or 
    // not gameObject of script is null, 
    // active in hierarchy or whether component itself is enabled or not.
    // Though CLRScript removes itself from Update list anyway when OnDisabled is called
    result |= CLRSetupFlags.NoSafetyChecks;
    return result;
}
```

You may have a situation where your base class has sealed update function. So you may still want to group all child classess to be executed toghether. In this case you can add `ExecutionGroupBaseClass` attribute, so all children of this class will be boundled toghether if they have same execution order index.

## Execution Order
You can control execution order in 3 ways:
* `[DefaultExecutionOrder]` attribute, which will just set class order to some value
* `[Run.After(Type)]` and `[Run.Before(Type)]` which will generate execution order automatically based on execution order of given class.
* Execution Order window. System will take execution order from ProjectSettings as just number. So don't expect CLRScript to run between MonoBehaviours because all CLRScripts are executed in a single loop.

All execution IDs are stored in `CLR_Ex_Order.asset` which stores every class that has non-zero execution order.
Resolving execution order when using `Run.Before` and `Run.After` attributes may be undefined in cases when you have both Before and After attribute on your class or if you have created infinite loop by making cyclic dependency.

## UObject
Package also uses opportunity to insert some code into initialization to add ability to identify UnityEngine.Object's by instanceID. This is done via `UObject<T>` struct. It contains only instance ID instead of managed reference, therefore can be used in NativeArrays or even passed into Jobs (If it is not Burst compiled)

```csharp

GameObject GetGameObject(UObject<GameObject> u) => u;               // Has implicit operators
GameObject GetGameObject(UObject<GameObject> u) => u.Dereference(); // Gets reference as well 

// In this case might actually not work because not all Unity Objects are cached at startup
// Implicit operator only performs null check
UObject<GameObject> GetUObject() => gameObject;

// Therefore if you want to be safe about whether or not your UObject will be valid use
UObject<GameObject> GetUObject() => UObject<GameObject>.NewSafe(gameObject);
```

## Utility
Package also provides some utility classses.
### GlobalTypeCache
All CLRScripts execution order/used callbacks data is precached, therefore at runtime we need to assign it based on type. To do it package creates internal TypeCache which finds a type by name and caches result for further use. You can also use for your own needs

### OrderedScriptCollection<T>
Provides a way to store data grouped by type and sorted (roughly) by execution index (only defined for CLRScript derived types). This can be used in case you want to create your own callbacks

### UnityObjectHashMap
Is a hash map implementation which stores specifically InstanceID->Object pairs.
