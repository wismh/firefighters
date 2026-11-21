using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace MyToolkit.SceneLoader
{
    public sealed class SceneLoaderServiceFacade : ISceneLoaderService
    {
        private readonly ISceneSourceLoader _buildIn;
        private readonly ISceneSourceLoader _addressables;
        private readonly ISceneCatalog _catalog;

        public SceneLoaderServiceFacade(
            ISceneSourceLoader buildIn,
            ISceneSourceLoader addressables,
            ISceneCatalog catalog)
        {
            _buildIn = buildIn;
            _addressables = addressables;
            _catalog = catalog;
        }

        public UniTask LoadSceneAsync(string sceneToLoad, bool unloadRedundant) =>
            SourceFor(sceneToLoad).LoadSceneAsync(sceneToLoad, unloadRedundant);

        public async UniTask LoadScenesAsync(List<string> scenesToLoad, string activeScene, bool unloadRedundant)
        {
            (List<string> builtIn, List<string> addressable) = ScenePartition.Split(scenesToLoad, _catalog);

            if (builtIn.Count > 0)
                await _buildIn.LoadScenesAsync(builtIn, unloadRedundant);

            // An empty request still goes to Addressables so that unloadRedundant clears its loaded scenes.
            if (addressable.Count > 0 || builtIn.Count == 0)
                await _addressables.LoadScenesAsync(addressable, unloadRedundant);

            SetActiveScene(activeScene);
        }

        public UniTask UnloadSceneAsync(string sceneToUnload) =>
            SourceFor(sceneToUnload).UnloadSceneAsync(sceneToUnload);

        public async UniTask UnloadScenesAsync(List<string> scenesToUnload)
        {
            (List<string> builtIn, List<string> addressable) = ScenePartition.Split(scenesToUnload, _catalog);

            if (builtIn.Count > 0)
                await _buildIn.UnloadScenesAsync(builtIn);

            if (addressable.Count > 0)
                await _addressables.UnloadScenesAsync(addressable);
        }

        private ISceneSourceLoader SourceFor(string sceneKey) =>
            _catalog.IsAddressable(sceneKey) ? _addressables : _buildIn;

        private static void SetActiveScene(string activeScene)
        {
            if (SceneManager.GetActiveScene().name == activeScene)
                return;

            Scene newActiveScene = SceneManager.GetSceneByName(activeScene);
            SceneManager.SetActiveScene(newActiveScene);
        }
    }
}
