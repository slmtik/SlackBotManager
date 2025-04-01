namespace API.VersionStrategists
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SlackElementDefinitionAttribute : Attribute
    {
        public Type? InputElementType { get; set; }
        public string? InitialValue { get; set; }
    }
}
