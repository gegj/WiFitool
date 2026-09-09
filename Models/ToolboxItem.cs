using System.Runtime.Serialization;

namespace WiFitool.Models
{
    [DataContract]
    public sealed class ToolboxItem
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string ExecutablePath { get; set; }
        [DataMember] public string Icon { get; set; }
        public string BuiltinId { get; set; }
    }
}
