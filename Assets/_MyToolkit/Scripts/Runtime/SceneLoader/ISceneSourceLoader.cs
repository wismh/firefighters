using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MyToolkit.SceneLoader
{
    /// <summary>Loads and unloads scenes from one source (build settings or Addressables).</summary>
    public interface ISceneSourceLoader
    {
        UniTask LoadSceneAsync(string sceneToLoad, bool unloadRedundant);

        UniTask LoadScenesAsync(List<string> scenesToLoad, bool unloadRedundant);

        UniTask UnloadSceneAsync(string sceneToUnload);

        UniTask UnloadScenesAsync(List<string> scenesToUnload);
    }
}
