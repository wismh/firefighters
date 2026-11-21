using Zenject;

namespace MyToolkit.SceneLoader
{
    /// <summary>Requires the game to bind <see cref="ISceneCatalog"/> first.</summary>
    public sealed class SceneLoaderServiceInstaller : Installer<SceneLoaderServiceInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<ISceneLoaderService>()
                .FromMethod(context =>
                {
                    var catalog = context.Container.Resolve<ISceneCatalog>();
                    return new SceneLoaderServiceFacade(
                        new BuildInSceneLoaderService(catalog.BootstrapScene),
                        new AddressablesSceneLoaderService(),
                        catalog);
                })
                .AsSingle();

            Container.Bind<IGameplayLevelSceneService>()
                .To<GameplayLevelSceneService>()
                .AsSingle();
        }
    }
}
