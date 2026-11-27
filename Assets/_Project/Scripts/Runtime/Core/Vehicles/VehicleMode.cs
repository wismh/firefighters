namespace _Project.Vehicles
{
    public enum VehicleMode
    {
        /// <summary>Kinematic, moved along lanes by the traffic system.</summary>
        Traffic,
        /// <summary>Physics car driven by the player.</summary>
        Driven,
        /// <summary>Physics car without a driver: left by the player or knocked out of traffic.</summary>
        Parked,
    }
}
