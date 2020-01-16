using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace qlik_qv_export
{
    public class ItemRequestBody
    {

        public ItemRequestBody(EngineDoc doc)
        {
            Name = doc.Attributes.Name;
            Description = "";
            ResourceId = doc.Attributes.AppId;
            ResourceType = ResourceType.Qlikview;
            ResourceCreatedBySubject = "";
            ResourceCreatedAt = DateTime.Now;//In lack of real creation date
            ResourceUpdatedAt = DateTime.Now;
            ResourceLink = "";
            ResourceAttributes = new Dictionary<string, object>();
            ResourceCustomAttributes = new Dictionary<string, object>();
            ThumbNail = new Dictionary<string, object>();
            ThumbnailId = "";
            ResourceUpdatedBySubject = "";
            SpaceId = "";
            AddThumbNail();
        }

        private void AddThumbNail()
        {
            ThumbNail.Add("href", "");
        }

        [JsonProperty("description")]
        public string Description { get; private set; }

        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("spaceId")]
        public string SpaceId { get; private set; }

        [JsonProperty("resourceId")]
        public string ResourceId { get; private set; }

        [JsonProperty("resourceLink")]
        public string ResourceLink { get; private set; }

        [JsonProperty("resourceType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ResourceType ResourceType { get; private set; }

        [JsonProperty("thumbnailLink")]
        public string ThumbnailLink { get; private set; }

        [JsonProperty("resourceCreatedAt")]
        public DateTime ResourceCreatedAt { get; private set; }

        [JsonProperty("resourceUpdatedAt")]
        public DateTime ResourceUpdatedAt { get; private set; }

        [JsonProperty("resourceCreatedBySubject")]
        public string ResourceCreatedBySubject { get; private set; }

        [JsonProperty("resourceAttributes")]
        public Dictionary<string, object> ResourceAttributes { get; private set; }

        [JsonProperty("thumbnail")]
        public Dictionary<string, object> ThumbNail { get; private set; }

        [JsonProperty("resourceCustomAttributes")]
        public Dictionary<string, object> ResourceCustomAttributes { get; private set; }

        [JsonProperty("resourceUpdatedBySubject")]
        public string ResourceUpdatedBySubject { get; private set; }
        
        [JsonProperty("thumbnailId")]
        public string ThumbnailId { get; private set; }
    }

    public enum ResourceType
    {
        [EnumMember(Value = "qlikview")] Qlikview,
    }
}
