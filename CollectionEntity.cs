using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace qlik_qv_export
{
    [DataContract]
    public class CollectionEntity
    {
        [DataMember(Name = "id", IsRequired = false, EmitDefaultValue = false)]
        public string Id;

        [DataMember(Name = "createdAt", IsRequired = false, EmitDefaultValue = false)]
        public string CreatedAt { get; set; }

        [DataMember(Name = "creatorId", IsRequired = false, EmitDefaultValue = false)]
        public string CreatorId;

        [DataMember(Name = "description", IsRequired = false)]
        public string Description;

        [DataMember(Name = "name", IsRequired = false)]
        public string Name;

        [DataMember(Name = "tenantId", IsRequired = false, EmitDefaultValue = false)]
        public string TenantId;

        [DataMember(Name = "meta", IsRequired = false, EmitDefaultValue = false)]
        public Meta Meta;

        [DataMember(Name = "type", IsRequired = false)] [JsonConverter(typeof(StringEnumConverter))]
        public CollectionType Type;
    }

    [DataContract]
    public enum CollectionType
    {
        [EnumMember(Value = "public")] Public,
        [EnumMember(Value = "private")] Private,
        [EnumMember(Value = "favorite")] Favorite
    }

    [DataContract]
    public class Tag
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    [DataContract]
    public class Meta
    {
        public bool isFavorited { get; set; }
        public List<string> actions { get; set; }
        [DataMember(Name = "tags", IsRequired = false)]
        public List<TagObject> Tags { get; set; }
        public List<object> collections { get; set; }
    }
}