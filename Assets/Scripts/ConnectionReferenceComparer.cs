using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FishNet.Connection;

// FishNet equality/hash uses mutable ClientId. Session tables use object identity
// and remove on Stopped; another connection reusing the ID must not inherit state.
internal sealed class ConnectionReferenceComparer : IEqualityComparer<NetworkConnection>
{
    internal static readonly ConnectionReferenceComparer Instance = new();
    public bool Equals(NetworkConnection x, NetworkConnection y) => ReferenceEquals(x, y);
    public int GetHashCode(NetworkConnection value) => RuntimeHelpers.GetHashCode(value);
}
