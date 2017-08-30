using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace WebStalkHard.Models
{
    public class KeyPhrases
    {
        [JsonProperty(PropertyName = "documents")]
        public List<Document> Documents { get; set; } = new List<Document>();
    }

    public class Document
    {
        [JsonProperty(PropertyName = "language")]
        public string Language { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }
    }
}