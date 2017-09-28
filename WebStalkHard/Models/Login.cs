using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace WebStalkHard.Models
{
    public class Login
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "accessTokenFacebook")]
        public FacebookAuthenticateResponse AccessTokenFacebook { get; set; }

        [JsonProperty(PropertyName = "accessTokenTwitter")]
        public TwitAuthenticateResponse AccessTokenTwitter { get; set; }

        [JsonProperty(PropertyName = "userFacebook")]
        public string UserFacebook { get; set; }

        [JsonProperty(PropertyName = "userTwitter")]
        public string UserTwitter { get; set; }

        [JsonProperty(PropertyName = "visibleSearch")]
        public bool VisibleSearch { get; set; }

        [JsonProperty(PropertyName = "keyPhrases")]
        public List<KeyPhrase> KeyPhrases { get; set; } = new List<KeyPhrase>();
    }

    public class FacebookAuthenticateResponse
    {
        [JsonProperty(PropertyName = "access_token")]
        public string AccessToken { get; set; }

        [JsonProperty(PropertyName = "token_type")]
        public string TokenType { get; set; }

        [JsonProperty(PropertyName = "expires_in")] //The number of seconds until this access token expires.
        public long ExpiresIn { get; set; }

        [JsonProperty(PropertyName = "data_created")]
        public DateTime DataCreated { get; set; }
    }

    public class TwitAuthenticateResponse
    {
        [JsonProperty(PropertyName = "token_type")]
        public string TokenType { get; set; }

        [JsonProperty(PropertyName = "access_token")]
        public string AccessToken { get; set; }
    }

    public class KeyPhrase
    {
        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        [JsonProperty(PropertyName = "references")]
        public List<References> References { get; set; } = new List<References>();
    }

    public class References
    {
        [JsonProperty(PropertyName = "idTweet")]
        public string IdTweet { get; set; }

        //[JsonProperty(PropertyName = "textTweet")]
        //public string TextTweet { get; set; }

        //[JsonProperty(PropertyName = "idPost")]
        //public string IdPost { get; set; }
    }
}