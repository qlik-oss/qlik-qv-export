using System.Runtime.Serialization;

namespace qlik_qv_export
{
    [DataContract]
    public class TagObject
    {
        [DataMember(Name = "name", IsRequired = false)]
        public string Name { get; set; }
        [DataMember(Name = "type", IsRequired = false)]
        public string Type { get; set; }
        [DataMember(Name = "id", IsRequired = false)]
        public string Id { get; set; }

        [DataMember(Name = "published", IsRequired = false)]
        public bool Published { get; set; }
    }
}
