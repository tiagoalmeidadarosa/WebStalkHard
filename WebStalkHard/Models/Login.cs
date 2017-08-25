using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace WebStalkHard.Models
{
    public class Login
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "accessTokenFacebook")]
        public string AccessTokenFacebook { get; set; }

        [JsonProperty(PropertyName = "accessTokenTwitter")]
        public string AccessTokenTwitter { get; set; }

        [JsonProperty(PropertyName = "userFacebook")]
        public string UserFacebook { get; set; }

        [JsonProperty(PropertyName = "userTwitter")]
        public string UserTwitter { get; set; }
    }
}