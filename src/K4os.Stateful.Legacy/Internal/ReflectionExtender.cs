using System;
using System.Linq;
using System.Reflection;

namespace K4os.Stateful.Legacy.Internal
{
	internal static class ReflectionExtender
	{
		/// <summary>
		/// Type distance cache. It should Concurrent dictionary but it is not available
		/// on all flavors of Portable Class Library.
		/// </summary>
		private static readonly IDictionary<Tuple<Type, Type>, int> CachedTypeDistanceMap =
			new Dictionary<Tuple<Type, Type>, int>();

		/// <summary>Checks if child type inherits (or implements) from parent.</summary>
		/// <param name="child">The child.</param>
		/// <param name="parent">The parent.</param>
		/// <returns><c>true</c> if child type inherits (or implements) from parent; <c>false</c> otherwise</returns>
		public static bool InheritsFrom(this Type child, Type parent) =>
			parent.GetTypeInfo().IsAssignableFrom(child);

		/// <summary>Calculates distance between child and parent type.</summary>
		/// <param name="child">The child.</param>
		/// <param name="grandparent">The parent.</param>
		/// <returns>Inheritance distance between child and parent.</returns>
		/// <exception cref="System.ArgumentException">Thrown when child does not inherit from parent at all.</exception>
		public static int DistanceFrom(this Type child, Type grandparent)
		{
			if (child == grandparent)
				return 0;

			int result;

			// try cache, maybe is has been already calculated
			var key = Tuple.Create(child, grandparent);
			lock (CachedTypeDistanceMap)
				if (CachedTypeDistanceMap.TryGetValue(key, out result))
					return result;

			child.EnsureNotNull("child");
			grandparent.EnsureNotNull("grandparent");

			if (!child.InheritsFrom(grandparent))
				throw new ArgumentException(
					string.Format(
						"Type '{0}' does not inherit nor implements '{1}'",
						child.Name, grandparent.Name));

			result = GetIntermediateParents(child, grandparent)
				.Select(t => t.DistanceFrom(grandparent))
				.Min();

			// update cache
			lock (CachedTypeDistanceMap)
				CachedTypeDistanceMap[key] = result;

			return result;
		}

		/// <summary>Gets the list of parent types which also inherit for grandparent.</summary>
		/// <param name="child">The child.</param>
		/// <param name="grandparent">The parent.</param>
		/// <returns>Collection of types.</returns>
		private static IEnumerable<Type> GetIntermediateParents(Type child, Type grandparent)
		{
			var childInfo = child.GetTypeInfo();
			var baseType = childInfo.BaseType;

			bool IsFirstImplementationOf(Type interfaceType) =>
				interfaceType.InheritsFrom(grandparent) &&
				(baseType == null || !baseType.InheritsFrom(interfaceType));

			if (grandparent.GetTypeInfo().IsInterface)
			{
				var baseInterfaces = childInfo.GetInterfaces().Where(IsFirstImplementationOf);

				foreach (var i in baseInterfaces)
					yield return i;
			}

			if (baseType is not null && baseType.InheritsFrom(grandparent))
			{
				yield return baseType;
			}
		}
	}
}
