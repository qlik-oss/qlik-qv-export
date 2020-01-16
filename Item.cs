using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace qlik_qv_export
{
    class Item
    {

        public class Itemobject
        {
            public string name { get; set; }
            public string resourceId { get; set; }
            public string resourceType { get; set; }
            public string description { get; set; }
            public Resourceattributes resourceAttributes { get; set; }
            public Resourcecustomattributes resourceCustomAttributes { get; set; }
            public DateTime resourceCreatedAt { get; set; }
            public string resourceCreatedBySubject { get; set; }
        }

        public class Resourceattributes
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string thumbnail { get; set; }
            public string lastReloadTime { get; set; }
            public DateTime createdDate { get; set; }
            public DateTime modifiedDate { get; set; }
            public string owner { get; set; }
            public string ownerId { get; set; }
            public string dynamicColor { get; set; }
            public bool published { get; set; }
            public string publishTime { get; set; }
            public bool hasSectionAccess { get; set; }
            public bool encrypted { get; set; }
            public string originAppId { get; set; }
            public string _resourcetype { get; set; }
        }

        public class Resourcecustomattributes
        {
        }

    }
}
