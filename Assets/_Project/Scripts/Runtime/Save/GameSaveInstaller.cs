using System;
using System.IO;
using MyToolkit.Save;
using MyToolkit.Save.Migrations;
using UnityEngine;
using Zenject;

namespace _Project.Save
{
    public sealed class GameSaveInstaller : Installer<GameSaveInstaller>
    {
        public const int CurrentSaveVersion = 1;
        public const string SaveFileName = "save.json";

        public override void InstallBindings()
        {
            SaveServiceInstaller.Install(Container);

            Container.Bind<SaveMigrationRunner>()
                .FromInstance(new SaveMigrationRunner(Array.Empty<ISaveMigration>(), CurrentSaveVersion))
                .AsSingle();

            string savePath = Path.Combine(Application.persistentDataPath, SaveFileName);

            Container.Bind<ISaveService<GameSaveState>>()
                .To<SaveService<GameSaveState>>()
                .AsSingle()
                .WithArguments(
                    CurrentSaveVersion,
                    (Action<GameSaveState, int>)SetVersion,
                    savePath,
                    (Func<GameSaveState>)CreateDefault)
                .NonLazy();
        }

        private static void SetVersion(GameSaveState state, int version)
        {
            state.Version = version;
        }

        private static GameSaveState CreateDefault()
        {
            return new GameSaveState { Version = CurrentSaveVersion };
        }
    }
}
