using _Project.Traffic;

namespace _Project.Pedestrians
{
    /// <summary>A way from one sidewalk point to another: along the sidewalk, or over a crosswalk.</summary>
    public readonly struct SidewalkLink
    {
        public readonly int To;
        /// <summary>The intersection whose signal rules the crosswalk; null on the sidewalk.</summary>
        public readonly Intersection Crossing;
        /// <summary>Signal axis of the road being crossed.</summary>
        public readonly int RoadAxis;

        public SidewalkLink(int to, Intersection crossing, int roadAxis)
        {
            To = to;
            Crossing = crossing;
            RoadAxis = roadAxis;
        }

        public bool IsCrossing => Crossing != null;

        /// <summary>The cars on the crossed road have red, or there is no signal and they just give way.</summary>
        public bool MayCross => !IsCrossing || !Crossing.HasSignal || Crossing.GetSignal(RoadAxis) == SignalState.Red;
    }
}
