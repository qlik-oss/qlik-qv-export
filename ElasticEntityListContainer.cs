using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace qlik_qv_export
{
    [DataContract]
    public class ElasticEntityListContainer<T>
    {

        public ElasticEntityListContainer()
        {
        }

        [DataMember(Name = "data", IsRequired = false, EmitDefaultValue = false)]
        public IList<T> Data = new List<T>();

        [DataMember(Name = "links", IsRequired = false, EmitDefaultValue = false)]
        public Links Links;

        public string NextUrl
        {
            get
            {
                Links.Next.TryGetValue("href",
                    out var nextString);
                if (nextString == null) return null;
                
                var startIndex = nextString.IndexOf("api/v1/", StringComparison.OrdinalIgnoreCase);
                if (startIndex < 0)
                {
                    //LogAndThrowError($"Collections-service returned next link with unexpected path. Failed to parse the next link: '{nextString}'");
                }

                nextString = nextString.Substring(startIndex);

                return nextString;
            }
        }
    }

    [DataContract]
    public class Links
    {
        [DataMember(Name = "next", IsRequired = false, EmitDefaultValue = true)]
        public Dictionary<string, string> Next = new Dictionary<string, string>();
    }
}