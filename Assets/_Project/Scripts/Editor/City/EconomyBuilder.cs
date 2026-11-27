using System.IO;
using _Project.Economy;
using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>The damage price list the scene installer hands to the bill.</summary>
    public static class EconomyBuilder
    {
        private const string PricesPath = ProjectPaths.Configurations + "/Economy/DamagePrices.asset";

        /// <summary>Loads the price list, or creates it with the default prices.</summary>
        public static DamagePrices EnsurePrices()
        {
            var prices = AssetDatabase.LoadAssetAtPath<DamagePrices>(PricesPath);
            if (prices != null)
            {
                return prices;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PricesPath));
            prices = ScriptableObject.CreateInstance<DamagePrices>();
            AssetDatabase.CreateAsset(prices, PricesPath);
            AssetDatabase.SaveAssets();
            return prices;
        }
    }
}
