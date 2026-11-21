namespace MyToolkit.CustomCodeGenerator.Editor 
{
    public static class UnityCodeGenUtility
    {
        public static void Generate() 
        {
            ScriptFileGenerator.Generate();
        }

        public const string defaultFolderPath = "Assets/UnityCodeGen.Generated";
    }
}