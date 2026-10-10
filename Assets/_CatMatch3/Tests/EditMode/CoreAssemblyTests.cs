using CatMatch3.Core;
using NUnit.Framework;

namespace CatMatch3.Tests.EditMode
{
    public sealed class CoreAssemblyTests
    {
        [Test]
        public void CoreMarkerResolvesFromTheDedicatedDomainAssembly()
        {
            Assert.That(typeof(CoreAssembly).Assembly.GetName().Name,
                Is.EqualTo("CatMatch3.Core"),
                "Core code must compile into its own assembly, not a default Unity assembly.");
        }
    }
}
