using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace qlik_qv_export
{
    public class StringToListConverter : JsonConverter<Dictionary<string, object>>
    {
        public override void WriteJson(JsonWriter writer, Dictionary<string, object> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }

        public override Dictionary<string, object> ReadJson(JsonReader reader, Type objectType, Dictionary<string, object> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var result = serializer.Deserialize<Dictionary<string, object>>(reader);
            if (result != null)
            {
                foreach (var kv in result.Where(kv => kv.Value is string).ToList())
                {
                    result[kv.Key] = new List<string> { (kv.Value as string) };
                }
            }

            return result;
        }
    }
}