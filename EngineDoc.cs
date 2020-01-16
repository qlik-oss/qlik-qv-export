using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace qlik_qv_export
{
    [DataContract]
    public class EngineDoc
    {
        [DataMember(Name = "attributes", IsRequired = false, EmitDefaultValue = false)]
        public Attributes Attributes { get; set; }

        [DataMember(Name = "privileges", IsRequired = false, EmitDefaultValue = false)]
        public List<string> Privileges { get; set; }

        [DataMember(Name = "create", IsRequired = false, EmitDefaultValue = false)]
        public Create[] Create { get; set; }
    }

    [DataContract]
    public class Create
    {
        [DataMember(Name = "resource", IsRequired = false, EmitDefaultValue = false)]
        public string Resource { get; set; }

        [DataMember(Name = "CanCreate", IsRequired = false, EmitDefaultValue = false)]
        public bool CanCreate { get; set; }
    }

    [DataContract]
    public class Attributes
    {
        [DataMember(Name = "id", IsRequired = false, EmitDefaultValue = false)]
        public string AppId { get; set; }

        [DataMember(Name = "name", IsRequired = false, EmitDefaultValue = false)]
        public string Name { get; set; }

        [DataMember(Name = "description", IsRequired = false, EmitDefaultValue = false)]
        public string Description { get; set; }

        [DataMember(Name = "thumbnail", IsRequired = false, EmitDefaultValue = false)]
        public string Thumbnail { get; set; }

        [DataMember(Name = "lastReloadTime", IsRequired = false, EmitDefaultValue = false)]
        public string LastReloadTime { get; set; }

        [DataMember(Name = "createdDate", IsRequired = false, EmitDefaultValue = false)]
        public DateTime CreatedDate { get; set; }

        [DataMember(Name = "custom", IsRequired = false, EmitDefaultValue = false)]
        [JsonConverter(typeof(StringToListConverter))]
        public Dictionary<string, object> Custom { get; set; }

        [DataMember(Name = "modifiedDate", IsRequired = false, EmitDefaultValue = false)]
        public DateTime ModifiedDate { get; set; }

        [DataMember(Name = "owner", IsRequired = false, EmitDefaultValue = false)]
        public string Owner { get; set; }

        [DataMember(Name = "dynamicColor", IsRequired = false, EmitDefaultValue = false)]
        public string DynamicColor { get; set; }

        [DataMember(Name = "published", IsRequired = false, EmitDefaultValue = false)]
        public bool Published { get; set; }

        [DataMember(Name = "publishTime", IsRequired = false, EmitDefaultValue = false)]
        public string PublishTime { get; set; }

        [DataMember(Name = "originAppId", IsRequired = false, EmitDefaultValue = false)]
        public string OriginAppId { get; set; }

        [DataMember(Name = "hasSectionAccess", IsRequired = false, EmitDefaultValue = false)]
        public bool HasSectionAccess { get; set; }

        [DataMember(Name = "encrypted", IsRequired = false, EmitDefaultValue = false)]
        public bool Encrypted { get; set; }

        [DataMember(Name = "_resourcetype", IsRequired = false, EmitDefaultValue = false)]
        public string Resourcetype { get; set; }

        [DataMember(Name = "ownerId", IsRequired = false, EmitDefaultValue = false)]
        public string OwnerId { get; set; }
    }
}