using System.Collections.Generic;
using UnityEngine;

// Manages one pool of Unit instances per UnitDef; avoids per-frame instantiation.
public class UnitPool : MonoBehaviour
{
    private readonly Dictionary<UnitDef, Stack<Unit>> pools = new();

    public Unit Get(UnitDef def)
    {
        if (def == null || def.prefab == null)
        {
            Debug.LogError("UnitPool.Get: def or def.prefab is null");
            return null;
        }

        if (!pools.TryGetValue(def, out var stack))
        {
            stack = new Stack<Unit>();
            pools[def] = stack;
        }

        Unit unit;
        if (stack.Count > 0)
        {
            unit = stack.Pop();
        }
        else
        {
            var go = Instantiate(def.prefab, transform);
            unit = go.GetComponent<Unit>();
            if (unit == null)
            {
                Debug.LogError($"UnitPool: prefab '{def.prefab.name}' has no Unit component", go);
                Destroy(go);
                return null;
            }
        }

        unit.def = def;              // set before activating so OnEnable sees it
        unit.gameObject.SetActive(true);
        return unit;
    }

    public void Release(Unit unit)
    {
        if (unit == null) return;

        unit.gameObject.SetActive(false);

        var def = unit.def;
        if (def == null)
        {
            Destroy(unit.gameObject);
            return;
        }

        if (!pools.TryGetValue(def, out var stack))
        {
            stack = new Stack<Unit>();
            pools[def] = stack;
        }
        stack.Push(unit);
    }
}
