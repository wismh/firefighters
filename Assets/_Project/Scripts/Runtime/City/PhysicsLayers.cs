namespace _Project.City
{
    /// <summary>
    /// Physics layers named in ProjectSettings/TagManager. Traffic cars do not collide with debris, so a lamp knocked
    /// onto the road never jams the kinematic traffic.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int Default = 0;
        public const int Traffic = 8;
        public const int Debris = 9;
    }
}
