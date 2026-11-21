using Zenject;

namespace MyToolkit.Steam
{
    public sealed class SteamInstaller : Installer<SteamInstaller>
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<SteamService>()
                .AsSingle()
                .NonLazy();
        }
    }
}
