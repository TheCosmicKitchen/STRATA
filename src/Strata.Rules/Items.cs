using System;
using System.Collections.Generic;
using System.Linq;

namespace Strata.Rules;

public enum Item { Stone, Sand, Ore, Fibre, Metal, Glass, Rope, Pick, Flare, Junk, Coal, Salt, Sulfur, Crystal, Clay }

/// <summary>What the delver carries. Bulk materials share one capacity; tools do not count against it. Godot-free.</summary>
public sealed class Pack
{
    public int Capacity = 30;
    public readonly int[] Count = new int[Enum.GetValues<Item>().Length];
    readonly float[] _partial = new float[Enum.GetValues<Item>().Length];

    public static bool IsBulk(Item i) => i is Item.Stone or Item.Sand or Item.Ore or Item.Fibre or Item.Metal or Item.Glass or Item.Coal or Item.Salt or Item.Sulfur or Item.Crystal or Item.Clay;
    public int BulkUsed => Enum.GetValues<Item>().Where(IsBulk).Sum(i => Count[(int)i]);
    public bool Full => BulkUsed >= Capacity;
    public int this[Item i] => Count[(int)i];

    public bool Add(Item i, int n = 1)
    {
        if (IsBulk(i) && BulkUsed + n > Capacity) return false;
        Count[(int)i] += n; return true;
    }

    /// <summary>Digging gives fractions of a unit per cell; whole units land in the pack.</summary>
    public bool AddPartial(Item i, float amount)
    {
        _partial[(int)i] += amount;
        while (_partial[(int)i] >= 1) { if (!Add(i)) { _partial[(int)i] = .99f; return false; } _partial[(int)i] -= 1; }
        return true;
    }

    public bool Take(Item i) { if (Count[(int)i] <= 0) return false; Count[(int)i]--; return true; }
}

/// <summary>
/// A work bench: things are laid on it by hand, then struck. After enough strikes whatever is on it becomes
/// something, or junk. The game never lists what makes what; see docs for the hidden recipes.
/// </summary>
public sealed class Bench
{
    public const int MaxItems = 4, StrikesNeeded = 4;
    public readonly List<Item> On = [];
    public int Strikes;

    public bool Place(Item i) { if (On.Count >= MaxItems) return false; On.Add(i); Strikes = 0; return true; }

    /// <summary>One hammer strike. Returns the result once finished, otherwise null.</summary>
    public (Item item, int count)? Strike()
    {
        if (On.Count == 0) return null;
        if (++Strikes < StrikesNeeded) return null;
        int fibre = On.Count(i => i == Item.Fibre), metal = On.Count(i => i == Item.Metal), glass = On.Count(i => i == Item.Glass);
        (Item, int) result =
            On.Count == 3 && fibre == 3 ? (Item.Rope, 1) :
            On.Count == 2 && metal == 2 ? (Item.Pick, 1) :
            On.Count == 2 && glass == 1 && fibre == 1 ? (Item.Flare, 2) :
            (Item.Junk, 1);
        On.Clear(); Strikes = 0;
        return result;
    }
}

/// <summary>A forge holds one thing at a time. It heats, turns into something, and stays hot for a while.</summary>
public sealed class Forge
{
    public float CookTime = 4, CoolTime = 3;
    public Item? Content;
    public float Timer;
    public bool Burning;      // something flammable went in

    public bool Done => Content != null && Timer >= CookTime;
    public bool Hot => Content != null && Timer < CookTime + CoolTime;

    public bool Put(Item i)
    {
        if (Content != null) return false;
        Content = i; Timer = 0; Burning = i is Item.Fibre or Item.Rope or Item.Flare;
        return true;
    }

    public void Update(float dt)
    {
        if (Content == null) return;
        Timer += dt;
        if (Timer >= CookTime && Burning) { Content = null; Burning = false; }
    }

    public Item? Result => Content switch
    {
        Item.Ore => Item.Metal, Item.Sand => Item.Glass, null => null, var other => other,
    };

    public Item? TakeOut() { var r = Done ? Result : Content; Content = null; Timer = 0; return r; }
}
