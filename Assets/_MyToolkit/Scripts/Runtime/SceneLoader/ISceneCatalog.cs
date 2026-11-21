namespace MyToolkit.SceneLoader
{
    /// <summary>
    /// Game-side knowledge the scene loader needs: which scene is the always-loaded bootstrap scene and which
    /// scene keys are loaded through Addressables. The game implements and binds it, the toolkit never references
    /// generated game code.
    /// </summary>
    public interface ISceneCatalog
    {
        string BootstrapScene { get; }

        bool IsAddressable(string sceneKey);
    }
}
