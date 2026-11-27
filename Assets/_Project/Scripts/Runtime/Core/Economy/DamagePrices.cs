using System;
using _Project.City;
using UnityEngine;

namespace _Project.Economy
{
    /// <summary>What the city charges for each kind of damage. A new prop kind gets a row here, not a code change.</summary>
    [CreateAssetMenu(menuName = "Configurations/Economy/" + nameof(DamagePrices), fileName = nameof(DamagePrices))]
    public sealed class DamagePrices : ScriptableObject
    {
        [Serializable]
        public struct PropPrice
        {
            public PropKind kind;
            [Min(0)] public int price;
        }

        [SerializeField] private PropPrice[] props =
        {
            new PropPrice { kind = PropKind.Lamp, price = 1200 },
            new PropPrice { kind = PropKind.TrafficLight, price = 4800 },
            new PropPrice { kind = PropKind.RoadSign, price = 650 },
            new PropPrice { kind = PropKind.Hydrant, price = 2300 },
            new PropPrice { kind = PropKind.Bin, price = 90 },
        };

        [Tooltip("For a prop kind that has no row above.")]
        [Min(0)] [SerializeField] private int otherProp = 300;
        [Tooltip("Repair cost per metre of total dent depth.")]
        [Min(0)] [SerializeField] private int dentPerMetre = 6000;
        [Min(0)] [SerializeField] private int minDent = 10;
        [Min(0)] [SerializeField] private int knockout = 800;
        [Min(0)] [SerializeField] private int moralDamage = 5000;

        public int Knockout => knockout;
        public int MoralDamage => moralDamage;

        public int PriceOf(PropKind kind)
        {
            for (var i = 0; i < props.Length; i++)
            {
                if (props[i].kind == kind)
                {
                    return props[i].price;
                }
            }

            return otherProp;
        }

        /// <summary>A dent of <paramref name="depth"/> metres, rounded to tens.</summary>
        public int DentCost(float depth)
        {
            return Mathf.Max(minDent, Mathf.RoundToInt(depth * dentPerMetre / 10f) * 10);
        }
    }
}
