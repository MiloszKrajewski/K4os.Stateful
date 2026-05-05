using System;

namespace K4os.Stateful.Legacy.Internal
{
	internal static class ContractExtender
	{
		/// <summary>Ensures that value is not null.</summary>
		/// <typeparam name="T">Type of value.</typeparam>
		/// <param name="object">The object.</param>
		/// <param name="name">The name.</param>
		/// <returns>Same value.</returns>
		/// <exception cref="System.ArgumentNullException">Thrown if value is <c>null</c>.</exception>
		public static T EnsureNotNull<T>(this T @object, string name)
		{
			if (ReferenceEquals(@object, null))
				throw new ArgumentNullException(name);
			return @object;
		}

		/// <summary>Ensures the value meets specific criteria.</summary>
		/// <typeparam name="T">Type of value.</typeparam>
		/// <param name="object">The object.</param>
		/// <param name="message">The message.</param>
		/// <param name="predicate">The predicate.</param>
		/// <returns>Same value.</returns>
		/// <exception cref="System.ArgumentException">Thrown if value does not meet given criteria.</exception>
		public static T EnsureThat<T>(this T @object, string message, Func<T, bool> predicate)
		{
			if (!predicate(@object))
				throw new ArgumentException(message);
			return @object;
		}
	}
}
