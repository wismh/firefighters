using MyToolkit.Save.Serialization;
using Zenject;

namespace MyToolkit.Save
{
    /// <summary>Binds serializer infrastructure shared by all projects.</summary>
    public class SaveServiceInstaller : Installer<SaveServiceInstaller>
    {
        private readonly bool _useCamelCase;
        private readonly bool _prettyPrintInEditor;

        public SaveServiceInstaller(bool useCamelCase = false, bool prettyPrintInEditor = true)
        {
            _useCamelCase = useCamelCase;
            _prettyPrintInEditor = prettyPrintInEditor;
        }

        public override void InstallBindings()
        {
            Container.Bind<ISerializer>()
                .FromInstance(new JsonSerializer(_useCamelCase, _prettyPrintInEditor))
                .AsSingle();
        }
    }
}
