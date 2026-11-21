using Zenject;

namespace MyToolkit.AssetLoader 
{
    public class AssetLoaderInstaller : Installer<AssetLoaderInstaller>
    {
        public override void InstallBindings() 
        {
            Container.Bind<IAssetLoaderFacadeService>()
                .To<AssetLoaderFacadeService>()
                .AsSingle();
            
            Container.Bind<IAddressablesAssetLoaderService>()
                .To<AddressableAssetLoaderService>()
                .AsSingle();

            Container.Bind<IResourceAssetLoaderService>()
                .To<ResourceAssetLoaderService>()
                .AsSingle();
        }
    }
}