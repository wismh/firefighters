namespace MyToolkit.Save.Serialization
{
    /// <summary>Serialize/deserialize save payloads without coupling domain code to a specific library.</summary>
    public interface ISerializer
    {
        string Serialize<T>(T value);

        T Deserialize<T>(string json);
    }
}
