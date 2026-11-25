using _Project.City;

namespace _Project.Comedy
{
    /// <summary>
    /// What the city will charge the fire brigade: every lamp, sign and dent is on the bill, next to the buildings
    /// it saved. The joke of the game is that the first column grows faster than the second.
    /// </summary>
    public sealed class DamageBill
    {
        // Repair cost per metre of total dent depth.
        private const int DentCostPerMetre = 6000;
        private const int KnockoutCost = 800;
        private const int MoralDamageCost = 5000;

        public int Total { get; private set; }
        public int PropsBroken { get; private set; }
        public int CarsKnockedOut { get; private set; }
        public int PedestriansHit { get; private set; }
        public int PedestriansSoaked { get; private set; }
        public int DriversSoaked { get; private set; }
        public int BuildingsSaved { get; private set; }
        public int HydrantsOpened { get; private set; }

        public static int PriceOf(PropKind kind)
        {
            switch (kind)
            {
                case PropKind.Lamp:
                    return 1200;
                case PropKind.TrafficLight:
                    return 4800;
                case PropKind.RoadSign:
                    return 650;
                case PropKind.Hydrant:
                    return 2300;
                case PropKind.Bin:
                    return 90;
                default:
                    return 300;
            }
        }

        public int AddProp(PropKind kind)
        {
            PropsBroken++;
            if (kind == PropKind.Hydrant)
            {
                HydrantsOpened++;
            }

            return Add(PriceOf(kind));
        }

        public int AddDent(float depth)
        {
            return Add(UnityEngine.Mathf.Max(10, UnityEngine.Mathf.RoundToInt(depth * DentCostPerMetre / 10f) * 10));
        }

        public int AddKnockout()
        {
            CarsKnockedOut++;
            return Add(KnockoutCost);
        }

        public void AddPedestrianHit()
        {
            PedestriansHit++;
        }

        public int AddMoralDamage()
        {
            return Add(MoralDamageCost);
        }

        public void AddSoakedPedestrian()
        {
            PedestriansSoaked++;
        }

        public void AddSoakedDriver()
        {
            DriversSoaked++;
        }

        public void AddSavedBuilding()
        {
            BuildingsSaved++;
        }

        private int Add(int cost)
        {
            Total += cost;
            return cost;
        }
    }
}
