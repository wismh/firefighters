using Zenject;

namespace MyToolkit.Audio
{
    public sealed class AudioSystemInstaller : Installer<AudioSystemInstaller>
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<AudioSystem>()
                .FromComponentInHierarchy()
                .AsSingle();
        }
    }
}
