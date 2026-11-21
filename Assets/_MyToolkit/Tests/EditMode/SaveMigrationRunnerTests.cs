using System;
using MyToolkit.Save;
using MyToolkit.Save.Migrations;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MyToolkit.Tests.Save
{
    public sealed class SaveMigrationRunnerTests
    {
        private sealed class StepMigration : ISaveMigration
        {
            private readonly Action<JObject> _migrate;

            public StepMigration(int from, int to, Action<JObject> migrate)
            {
                FromVersion = from;
                ToVersion = to;
                _migrate = migrate;
            }

            public int FromVersion { get; }

            public int ToVersion { get; }

            public void Migrate(JObject root)
            {
                _migrate(root);
                SaveJsonVersion.Write(root, ToVersion);
            }
        }

        [Test]
        public void RunToCurrent_AppliesMigrationsInOrder()
        {
            var runner = new SaveMigrationRunner(
                new ISaveMigration[]
                {
                    new StepMigration(1, 2, root => root["Log"] = root.Value<string>("Log") + "2"),
                    new StepMigration(0, 1, root => root["Log"] = "1"),
                },
                targetVersion: 2);
            var json = new JObject();

            runner.RunToCurrent(json);

            Assert.AreEqual("12", json.Value<string>("Log"));
            Assert.AreEqual(2, SaveJsonVersion.Read(json));
        }

        [Test]
        public void RunToCurrent_AlreadyCurrent_DoesNothing()
        {
            var runner = new SaveMigrationRunner(new ISaveMigration[0], targetVersion: 3);
            var json = new JObject { ["Version"] = 3 };

            runner.RunToCurrent(json);

            Assert.AreEqual(3, SaveJsonVersion.Read(json));
        }

        [Test]
        public void RunToCurrent_MissingStep_Throws()
        {
            var runner = new SaveMigrationRunner(new ISaveMigration[0], targetVersion: 1);

            Assert.Throws<InvalidOperationException>(() => runner.RunToCurrent(new JObject()));
        }

        [Test]
        public void SaveJsonVersion_ReadsEitherCasing_AndKeepsExistingKey()
        {
            var camel = new JObject { ["version"] = 4 };
            Assert.AreEqual(4, SaveJsonVersion.Read(camel));

            SaveJsonVersion.Write(camel, 5);
            Assert.AreEqual(5, camel.Value<int>("version"));
            Assert.IsFalse(camel.ContainsKey("Version"));
        }
    }
}
