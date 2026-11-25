namespace _Project.Pedestrians
{
    /// <summary>From <see cref="Fall"/> on the pedestrian is off their feet.</summary>
    public enum PedestrianState
    {
        Walk,
        Wait,
        Flee,
        Emote,
        Fall,
        Ragdoll,
        GetUp,
    }
}
