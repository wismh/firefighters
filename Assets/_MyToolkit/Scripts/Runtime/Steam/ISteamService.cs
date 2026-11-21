namespace MyToolkit.Steam
{
    public interface ISteamService
    {
        bool IsAvailable { get; }

        ulong SteamId { get; }

        string PersonaName { get; }
    }
}
