namespace FrenchExDev.Net.Ddd.Testing
{
    /// <summary>
    /// Test helpers for DDD DSL consumers.
    /// </summary>
    public static class DddTestHelpers
    {
        /// <summary>
        /// Verifies that a type has the [AggregateRoot] attribute.
        /// </summary>
        public static bool IsAggregateRoot(System.Type type)
        {
            var attrType = System.Type.GetType("FrenchExDev.Net.Ddd.Attributes.AggregateRootAttribute, FrenchExDev.Net.Ddd.Attributes");
            if (attrType == null) return false;
            return System.Attribute.GetCustomAttribute(type, attrType) != null;
        }
    }
}
