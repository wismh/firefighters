namespace _Project.Editor
{
    /// <summary>Folders the scene builders read from and write to, in one place.</summary>
    public static class ProjectPaths
    {
        public const string Project = "Assets/_Project";
        public const string Materials = Project + "/Art/Materials";
        public const string Settings = Project + "/Settings";
        public const string Configurations = Project + "/Configurations";
        public const string CityConfigurations = Configurations + "/City";
        public const string Prefabs = Project + "/Prefabs";
        public const string FxPrefabs = Prefabs + "/Fx";
        public const string Sfx = Project + "/Audio/Sfx/";
        public const string GeneratedAudio = Project + "/Audio/Generated/";
        public const string MainScene = Project + "/Scenes/MainScene.unity";
        public const string InputActions = Project + "/InputSystem_Actions.inputactions";

        public const string ThirdParty = "Assets/ThirdParty";
        public const string SimpleTown = ThirdParty + "/SimpleTown/Prefabs/";
        public const string EpicToonFx = ThirdParty + "/EpicToonFX/";
    }
}
