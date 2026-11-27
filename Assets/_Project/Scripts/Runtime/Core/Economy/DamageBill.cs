using System;
using _Project.City;

namespace _Project.Economy
{
    /// <summary>
    /// What the city will charge the fire brigade: every lamp, sign and dent is on the bill, next to the buildings
    /// it saved. The joke of the game is that the first column grows faster than the second.
    /// </summary>
    public sealed class DamageBill
    {
        private readonly DamagePrices _prices;

        public DamageBill(DamagePrices prices)
        {
            _prices = prices;
        }

        /// <summary>Raised whenever the bill or one of its counters changes.</summary>
        public event Action Changed;

        public int Total { get; private set; }
        public int PropsBroken { get; private set; }
        public int CarsKnockedOut { get; private set; }
        public int PedestriansHit { get; private set; }
        public int PedestriansSoaked { get; private set; }
        public int DriversSoaked { get; private set; }
        public int BuildingsSaved { get; private set; }
        public int HydrantsOpened { get; private set; }

        public int AddProp(PropKind kind)
        {
            PropsBroken++;
            if (kind == PropKind.Hydrant)
            {
                HydrantsOpened++;
            }

            return Add(_prices.PriceOf(kind));
        }

        public int AddDent(float depth)
        {
            return Add(_prices.DentCost(depth));
        }

        public int AddKnockout()
        {
            CarsKnockedOut++;
            return Add(_prices.Knockout);
        }

        public void AddPedestrianHit()
        {
            PedestriansHit++;
            Changed?.Invoke();
        }

        public int AddMoralDamage()
        {
            return Add(_prices.MoralDamage);
        }

        public void AddSoakedPedestrian()
        {
            PedestriansSoaked++;
            Changed?.Invoke();
        }

        public void AddSoakedDriver()
        {
            DriversSoaked++;
            Changed?.Invoke();
        }

        public void AddSavedBuilding()
        {
            BuildingsSaved++;
            Changed?.Invoke();
        }

        private int Add(int cost)
        {
            Total += cost;
            Changed?.Invoke();
            return cost;
        }
    }
}
