using System.Collections.Concurrent;

namespace K4os.Stateful.Internal;

internal static class TypeExtensions
{
    private static readonly ConcurrentDictionary<(Type child, Type parent), int?> DistanceCache = new();

    /// <summary>Calculates the inheritance distance from <paramref name="child"/> to <paramref name="parent"/>.</summary>
    /// <returns>
    /// Number of steps in the hierarchy; <c>null</c> if <paramref name="parent"/> is not
    /// in <paramref name="child"/>'s inheritance chain.
    /// </returns>
    /// <remarks>
    /// For class parents, only the class chain is walked. For interface parents, the distance
    /// equals the depth of the first class in the chain that introduces the interface (via
    /// set-difference from the base class's interfaces). Interface children always return
    /// <c>null</c> — runtime types are always concrete classes.
    /// Results are cached per pair; safe to call from multiple threads.
    /// </remarks>
    public static int? DistanceFrom(this Type child, Type parent) =>
        DistanceCache.GetOrAdd((child, parent), static key => ComputeDistance(key.child, key.parent));

    private static int? ComputeDistance(Type child, Type parent) =>
        child == parent ? 0 :
        child.IsInterface ? parent.IsInterface ? ComputeInterfaceDistance(parent, child) : null :
        parent.IsInterface ? ComputeClassToInterfaceDistance(child, parent) :
        ComputeClassDistance(child, parent);

    private static int? ComputeInterfaceDistance(Type child, Type parent)
    {
        // BFS on the interface DAG. Direct parent interfaces are derived via set-difference:
        //   direct(I) = I.GetInterfaces() − union(J.GetInterfaces() for J in I.GetInterfaces())
        throw new InvalidOperationException(
            $"Interface-to-interface distance ({child.Name} → {parent.Name}) is not yet implemented.");
    }

    private static int? ComputeClassDistance(Type child, Type parent)
    {
        var distance = 1;
        var current = child.BaseType;

        while (current is not null)
        {
            if (current == parent) 
                return distance;

            current = current.BaseType;
            distance++;
        }

        return null;
    }

    private static int? ComputeClassToInterfaceDistance(Type child, Type parent)
    {
        var distance = 0;
        var current = child;
        var currentInterfaces = child.GetInterfaces();

        while (current is not null)
        {
            var baseInterfaces = current.BaseType?.GetInterfaces() ?? [];
            if (currentInterfaces.Contains(parent) && !baseInterfaces.Contains(parent))
                return distance;

            current = current.BaseType;
            currentInterfaces = baseInterfaces;
            distance++;
        }

        return null;
    }
}
