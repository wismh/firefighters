using System.Collections.Generic;
using MyToolkit.SceneLoader;
using NUnit.Framework;

namespace MyToolkit.Tests.SceneLoader
{
    public sealed class ScenePartitionTests
    {
        private sealed class FakeCatalog : ISceneCatalog
        {
            private readonly HashSet<string> _addressable;

            public FakeCatalog(params string[] addressable) =>
                _addressable = new HashSet<string>(addressable);

            public string BootstrapScene => "Bootstrap";

            public bool IsAddressable(string sceneKey) =>
                _addressable.Contains(sceneKey);
        }

        [Test]
        public void Split_SeparatesBySource_KeepingOrder()
        {
            var catalog = new FakeCatalog("A1", "A2");

            (List<string> builtIn, List<string> addressable) =
                ScenePartition.Split(new[] { "B1", "A1", "B2", "A2" }, catalog);

            Assert.AreEqual(new[] { "B1", "B2" }, builtIn);
            Assert.AreEqual(new[] { "A1", "A2" }, addressable);
        }

        [Test]
        public void Split_RemovesDuplicates()
        {
            var catalog = new FakeCatalog("A");

            (List<string> builtIn, List<string> addressable) =
                ScenePartition.Split(new[] { "A", "B", "A", "B" }, catalog);

            Assert.AreEqual(new[] { "B" }, builtIn);
            Assert.AreEqual(new[] { "A" }, addressable);
        }

        [Test]
        public void Split_EmptyInput_GivesEmptyLists()
        {
            (List<string> builtIn, List<string> addressable) =
                ScenePartition.Split(new string[0], new FakeCatalog());

            Assert.IsEmpty(builtIn);
            Assert.IsEmpty(addressable);
        }
    }
}
