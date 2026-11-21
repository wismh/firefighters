using System.Collections.Generic;
using System.Linq;

namespace MyToolkit.SceneLoader
{
    /// <summary>Splits scene keys by the source they are loaded from. Pure logic, no engine calls.</summary>
    public static class ScenePartition
    {
        public static (List<string> BuiltIn, List<string> Addressable) Split(
            IEnumerable<string> scenes,
            ISceneCatalog catalog)
        {
            List<string> distinct = scenes.Distinct().ToList();
            List<string> addressable = distinct.Where(catalog.IsAddressable).ToList();
            List<string> builtIn = distinct.Where(scene => !catalog.IsAddressable(scene)).ToList();
            return (builtIn, addressable);
        }
    }
}
