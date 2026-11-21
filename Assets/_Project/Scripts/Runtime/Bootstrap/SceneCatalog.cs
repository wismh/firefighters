using MyToolkit.SceneLoader;
using Project.Generated;

namespace _Project.Bootstrap
{
    public sealed class SceneCatalog : ISceneCatalog
    {
        public string BootstrapScene => SceneInBuild.BootstrapScene;

        public bool IsAddressable(string sceneKey) =>
            Address.AllSceneKeys.Contains(sceneKey);
    }
}
