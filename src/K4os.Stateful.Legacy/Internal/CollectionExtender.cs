using System;

namespace K4os.Stateful.Legacy.Internal;

internal static class CollectionExtender
{
	/// <summary>Iterates over sequence of object and executes action for each item.</summary>
	/// <typeparam name="T">Type of item.</typeparam>
	/// <param name="collection">The collection.</param>
	/// <param name="action">The action.</param>
	public static void Iterate<T>(this IEnumerable<T> collection, Action<T> action)
	{
		foreach (var item in collection) action(item);
	}
	
	public static V GetOrCreate<K, V>(this IDictionary<K, V> map, K key, Func<K, V> factory) => 
		map.TryGetValue(key, out var value) ? value : map[key] = factory(key);
}