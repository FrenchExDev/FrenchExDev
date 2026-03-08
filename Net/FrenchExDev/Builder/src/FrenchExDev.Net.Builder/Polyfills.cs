#if NETSTANDARD2_0
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();
        private ReferenceEqualityComparer() { }
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => obj is null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}
#endif
