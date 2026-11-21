using System;
using System.IO;
using MyToolkit.Save;
using MyToolkit.Save.Migrations;
using MyToolkit.Save.Serialization;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MyToolkit.Tests.Save
{
    public sealed class SaveServiceTests
    {
        private sealed class TestState
        {
            public int Version;
            public int Coins;
        }

        private sealed class DoubleCoinsMigration : ISaveMigration
        {
            public int FromVersion => 0;

            public int ToVersion => 1;

            public void Migrate(JObject root)
            {
                root["Coins"] = root.Value<int>("Coins") * 2;
                SaveJsonVersion.Write(root, ToVersion);
            }
        }

        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "MyToolkitSaveTests_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private SaveService<TestState> CreateService(int version = 1, params ISaveMigration[] migrations) =>
            new(
                new JsonSerializer(prettyPrintInEditor: false),
                new SaveMigrationRunner(migrations, version),
                version,
                (state, v) => state.Version = v,
                _path);

        [Test]
        public void Load_MissingFile_CreatesDefaultStateWithCurrentVersion()
        {
            TestState state = CreateService(version: 3).Load();

            Assert.AreEqual(0, state.Coins);
            Assert.AreEqual(3, state.Version);
        }

        [Test]
        public void State_BeforeLoad_Throws()
        {
            SaveService<TestState> service = CreateService();

            Assert.Throws<InvalidOperationException>(() => _ = service.State);
        }

        [Test]
        public void SaveImmediate_ThenLoadInNewService_RestoresState()
        {
            SaveService<TestState> first = CreateService();
            first.Load().Coins = 42;
            first.SaveImmediate();

            TestState restored = CreateService().Load();

            Assert.AreEqual(42, restored.Coins);
        }

        [Test]
        public void SaveImmediate_Twice_OverwritesAndLeavesNoTempFile()
        {
            SaveService<TestState> service = CreateService();
            service.Load().Coins = 1;
            service.SaveImmediate();
            service.State.Coins = 2;
            service.SaveImmediate();

            Assert.AreEqual(2, CreateService().Load().Coins);
            Assert.IsFalse(File.Exists(_path + ".tmp"));
        }

        [Test]
        public void Load_OldVersionFile_RunsMigrations()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{\"Coins\":5}");

            TestState state = CreateService(version: 1, new DoubleCoinsMigration()).Load();

            Assert.AreEqual(10, state.Coins);
            Assert.AreEqual(1, state.Version);
        }

        [Test]
        public void Load_CorruptFile_FallsBackToDefaultState()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{ not json");
            LogAssert.ignoreFailingMessages = true;

            try
            {
                TestState state = CreateService().Load();

                Assert.AreEqual(0, state.Coins);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Load_FileFromNewerVersion_FallsBackToDefaultState()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{\"Version\":9,\"Coins\":5}");
            LogAssert.ignoreFailingMessages = true;

            try
            {
                TestState state = CreateService(version: 1).Load();

                Assert.AreEqual(0, state.Coins);
                Assert.AreEqual(1, state.Version);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
