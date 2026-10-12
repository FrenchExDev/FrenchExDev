namespace FrenchExDev.Net.Entity.Dsl.Attributes
{
    using System;
    using FrenchExDev.Net.Dsl;
    using FrenchExDev.Net.Entity.Dsl.Attributes.Concepts;

    [MetaConcept(typeof(DbContextConcept))]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DbContextAttribute : Attribute
    {
        [MetaProperty("Name", "string")]
        public string? Name { get; set; }

        [MetaProperty("BoundedContext", "string")]
        public string? BoundedContext { get; set; }

        [MetaProperty("LazyLoading", "bool")]
        public bool LazyLoading { get; set; }

        [MetaProperty("QueryTracking", "string")]
        public string? QueryTracking { get; set; }

        [MetaProperty("QuerySplitting", "string")]
        public string? QuerySplitting { get; set; }

        [MetaProperty("ChangeTracking", "string")]
        public string? ChangeTracking { get; set; }

        [MetaProperty("EnableRetryOnFailure", "bool")]
        public bool EnableRetryOnFailure { get; set; }

        [MetaProperty("MaxRetryCount", "int")]
        public int MaxRetryCount { get; set; } = 6;

        /// <summary>
        /// Developer's project-level repository base class (open generic).
        /// If set, generated repositories inherit this instead of <c>RepositoryBase&lt;T&gt;</c>.
        /// Must be an open generic type inheriting <c>RepositoryBase&lt;T&gt;</c>.
        /// Example: <c>typeof(MyProjectRepository&lt;&gt;)</c>
        /// </summary>
        [MetaProperty("RepositoryBase", "Type")]
        public Type? RepositoryBase { get; set; }
    }
}
