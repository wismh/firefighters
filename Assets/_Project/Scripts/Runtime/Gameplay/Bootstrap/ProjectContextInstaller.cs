using MyToolkit.AssetLoader;
using MyToolkit.Audio;
using MyToolkit.SceneLoader;
using UnityEngine;
using Zenject;

namespace _Project.Bootstrap
{
    [CreateAssetMenu(
        menuName = "Configurations/Bootstrap/" + nameof(ProjectContextInstaller),
        fileName = nameof(ProjectContextInstaller) + "_Default")]
    public sealed class ProjectContextInstaller : ScriptableObjectInstaller<ProjectContextInstaller>
    {
        public override void InstallBindings()
        {
            Container.Bind<ISceneCatalog>().To<SceneCatalog>().AsSingle();
            SceneLoaderServiceInstaller.Install(Container);
            AssetLoaderInstaller.Install(Container);
            AudioSystemInstaller.Install(Container);
        }
    }
}
