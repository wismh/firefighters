using Cysharp.Threading.Tasks;

namespace MyToolkit.SceneLoader
{
    /// <summary>
    /// Keeps Global + gameplay additive stack and swaps only the level scene (additive).
    /// </summary>
    public interface IGameplayLevelSceneService
    {
        UniTask SwapLevelSceneAsync(string levelSceneToLoad, string levelSceneToUnload);
    }
}
