using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using WebStalkHard.Models;
using System.Runtime.Serialization;
using System.Xml;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Configuration;
using Facebook;

namespace WebStalkHard.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            ViewData["Permissoes"] = "public_profile,email,user_friends,user_about_me,user_actions.books,user_actions.fitness,user_actions.music,user_actions.news,user_actions.video,user_birthday,user_education_history,user_events,user_games_activity,user_hometown,user_likes,user_location,user_managed_groups,user_photos,user_posts,user_relationships,user_relationship_details,user_religion_politics,user_tagged_places,user_videos,user_website,user_work_history,read_custom_friendlists,read_insights,read_audience_network_insights,read_page_mailboxes,manage_pages,publish_pages,publish_actions,rsvp_event,pages_show_list,pages_manage_cta,pages_manage_instant_articles,ads_read,ads_management,business_management";
            //public_profile,email,user_friends,user_about_me,user_actions.books,user_actions.fitness,user_actions.music,user_actions.news,user_actions.video,user_birthday,user_education_history,user_events,user_games_activity,user_hometown,user_likes,user_location,user_managed_groups,user_photos,user_posts,user_relationships,user_relationship_details,user_religion_politics,user_tagged_places,user_videos,user_website,user_work_history,read_custom_friendlists,read_insights,read_audience_network_insights,read_page_mailboxes,manage_pages,publish_pages,publish_actions,rsvp_event,pages_show_list,pages_manage_cta,pages_manage_instant_articles,ads_read,ads_management,business_management,pages_messaging,pages_messaging_subscriptions,pages_messaging_payments,pages_messaging_phone_number

            return View();
        }

        public JsonResult ValidaUserTwitter(string screenName)
        {
            TwitAuthenticateResponse accessToken = GetAccessTokenTwitter();

            //Retira o '@' caso o usuário tenha inserido no campo
            if (screenName[0].Equals('@'))
            {
                screenName = screenName.Substring(1);
            }

            // Verifica se o usuário informado realmente existe
            var userFormat = "https://api.twitter.com/1.1/users/show.json?screen_name={0}";
            var userUrl = string.Format(userFormat, screenName);

            HttpWebRequest userRequest = (HttpWebRequest)WebRequest.Create(userUrl);
            var userHeaderFormat = "{0} {1}";
            userRequest.Headers.Add("Authorization", string.Format(userHeaderFormat, accessToken.TokenType, accessToken.AccessToken));
            userRequest.Method = "Get";

            try
            {
                WebResponse userResponse = userRequest.GetResponse();
                var userJson = string.Empty;
                using (userResponse)
                {
                    using (var reader = new StreamReader(userResponse.GetResponseStream()))
                    {
                        userJson = reader.ReadToEnd();
                    }

                    dynamic user = new JavaScriptSerializer().DeserializeObject(userJson);

                    if (user["id"] > 0 && !user["protected"])
                    {
                        return Json(true, JsonRequestBehavior.AllowGet);
                    }
                }
            }
            catch(Exception ex) { }

            return Json(false, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("Create")]
        public async Task<ActionResult> CreateAsync(FormCollection form)
        {
            Login login = new Login();
            login.UserFacebook = form["hiddenUserFacebook"];

            //Método que gera um Token de Acesso de longa duração para acesso a API do Facebook
            login.AccessTokenFacebook = GetTokenFacebookLongLife(form["hiddenAccessToken"]);
            
            //Retira o '@' caso o usuário tenha inserido no campo
            var userTwitter = form["inputUserTwitter"];
            if(userTwitter[0].Equals('@'))
            {
                userTwitter = userTwitter.Substring(1);
            }
            login.UserTwitter = userTwitter;

            //Retorna Token de Acesso do Twitter
            var accessTokenTwitter = GetAccessTokenTwitter();
            login.AccessTokenTwitter = accessTokenTwitter;

            login.VisibleSearch = false;
            if (form["checkVisibleSearch"].Contains("true"))
            {
                login.VisibleSearch = true;
            }

            var loginCreated = await DocumentDBRepository<Login>.CreateItemAsync(login);

            string id = "";
            if(loginCreated != null)
            {
                id = loginCreated.Id;
            }

            //Método que cria as respostas para a opção Descobrir Algo do chatterbot, a partir do Twitter
            await SetDiscoverSomethingTwitterAsync(accessTokenTwitter, userTwitter, 0, id);

            return RedirectToAction("Chatterbot", "Home", new { id = id });
        }

        [ActionName("Chatterbot")]
        public async Task<ActionResult> ChatterbotAsync(string id)
        {
            if(!string.IsNullOrEmpty(id))
            {
                string secretKeyBotFramework = ConfigurationManager.AppSettings["secretKeyBotFramework"];
                string tokenBotFramework = await GetTokenBotFrameworkAsync(secretKeyBotFramework);

                var item = await DocumentDBRepository<Login>.GetItemAsync(id);

                if (item != null && !string.IsNullOrEmpty(tokenBotFramework))
                {
                    //Verifica se o chatterbot não expirou, pois após 60 dias o token do Facebook pode ter sido expirado
                    bool dateValid = true;
                    if(item.AccessTokenFacebook.ExpiresIn > 0)
                        dateValid = item.AccessTokenFacebook.DataCreated.AddSeconds(Convert.ToDouble(item.AccessTokenFacebook.ExpiresIn)) >= DateTime.Now.AddDays(1);
                    string nome = "";
                    string urlImage = "";

                    if (dateValid)
                    {
                        var client = new FacebookClient();
                        client.AccessToken = item.AccessTokenFacebook.AccessToken;
                        client.Version = "v2.10";
                        //client.AppId = ConfigurationManager.AppSettings["appIdFacebook"];
                        //client.AppSecret = ConfigurationManager.AppSettings["appSecretFacebook"];

                        dynamic retorno = client.Get("me?fields=name,picture");
                        nome = retorno.name;
                        urlImage = retorno.picture.data.url;
                    }

                    return View(new Chatterbot { Id = item.Id, Token = tokenBotFramework.Replace("\"", ""), DateValid = dateValid, NomeUser = nome, ImageUser = urlImage });
                }
            }

            return View();
        }

        [ActionName("Search")]
        public async Task<ActionResult> SearchAsync(string q)
        {
            ViewData["Search"] = q;

            var items = await DocumentDBRepository<Login>.GetItemsAsync(l => l.VisibleSearch && l.UserFacebook.ToUpper().Contains(q.ToUpper()));

            return View(items);
        }

        public FacebookAuthenticateResponse GetTokenFacebookLongLife(string accessTokenShort)
        {
            string appId = ConfigurationManager.AppSettings["appIdFacebook"];
            string appSecret = ConfigurationManager.AppSettings["appSecretFacebook"];
            string url = "oauth/access_token?grant_type={0}&client_id={1}&client_secret={2}&fb_exchange_token={3}";

            var client = new FacebookClient();
            dynamic retorno = client.Get(string.Format(url, "fb_exchange_token", appId, appSecret, accessTokenShort));

            FacebookAuthenticateResponse facebookAuthenticateResponse = new FacebookAuthenticateResponse();
            facebookAuthenticateResponse.AccessToken = retorno.access_token;
            facebookAuthenticateResponse.TokenType = retorno.token_type;
            if(retorno.expires_in != null)
                facebookAuthenticateResponse.ExpiresIn = retorno.expires_in;
            facebookAuthenticateResponse.DataCreated = DateTime.Now;

            return facebookAuthenticateResponse;
        }

        public async Task<string> GetTokenBotFrameworkAsync(string secretKey)
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = System.Net.Http.HttpMethod.Get;
                request.RequestUri = new Uri("https://webchat.botframework.com/api/tokens");
                request.Headers.TryAddWithoutValidation("Authorization", "BotConnector " + secretKey);
                client.Timeout = TimeSpan.FromSeconds(10);

                HttpResponseMessage response;
                string token = "";
                try
                {
                    response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    token = await response.Content.ReadAsStringAsync();
                }
                catch (HttpException ex) { }

                return token;
            }
        }

        public TwitAuthenticateResponse GetAccessTokenTwitter()
        {
            //Busca o token de acesso para a API do Twitter, vai ser utilizado dentro do chatterbot. Nota-se que não é necessário login

            // You need to set your own keys
            var oAuthConsumerKey = "qWh8ir2uy6jgMILcgFxRitq6R";
            var oAuthConsumerSecret = "cF71OBRKJTNhrncTrH9Ei1HKIoFwOWKodd7AbflDhCRzBKDozh";
            var oAuthUrl = "https://api.twitter.com/oauth2/token";

            // Do the Authenticate
            var authHeaderFormat = "Basic {0}";

            var authHeader = string.Format(authHeaderFormat,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.EscapeDataString(oAuthConsumerKey) + ":" +
                Uri.EscapeDataString((oAuthConsumerSecret)))
            ));

            var postBody = "grant_type=client_credentials";

            HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create(oAuthUrl);
            authRequest.Headers.Add("Authorization", authHeader);
            authRequest.Method = "POST";
            authRequest.ContentType = "application/x-www-form-urlencoded;charset=UTF-8";
            authRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (Stream stream = authRequest.GetRequestStream())
            {
                byte[] content = ASCIIEncoding.ASCII.GetBytes(postBody);
                stream.Write(content, 0, content.Length);
            }

            authRequest.Headers.Add("Accept-Encoding", "gzip");

            WebResponse authResponse = authRequest.GetResponse();
            // deserialize into an object
            TwitAuthenticateResponse twitAuthResponse = new TwitAuthenticateResponse();
            using (authResponse)
            {
                using (var reader = new StreamReader(authResponse.GetResponseStream()))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var objectText = reader.ReadToEnd();
                    twitAuthResponse = JsonConvert.DeserializeObject<TwitAuthenticateResponse>(objectText);
                }
            }

            return twitAuthResponse;
        }

        private DateTime storedTokenTime = DateTime.MinValue;
        private string storedTokenValue = string.Empty;

        public async Task<string> GetAccessTokenTranslateAsync()
        {
            //Busca o token de acesso para ser utilizado na API de Translate da Microsoft. Ele expira a cada 10 minutos, então é necessário uma validação

            // Re-use the cached token if there is one.
            if ((DateTime.Now - storedTokenTime) < new TimeSpan(0, 5, 0))
            {
                return storedTokenValue;
            }

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = System.Net.Http.HttpMethod.Post;
                request.RequestUri = new Uri("https://api.cognitive.microsoft.com/sts/v1.0/issueToken");
                request.Content = new StringContent(string.Empty);
                request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", "efc56f55f859425885cd2a46ed75fb55");
                client.Timeout = TimeSpan.FromSeconds(10);

                HttpResponseMessage response;
                string token = "";
                try
                {
                    response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    token = await response.Content.ReadAsStringAsync();
                }
                catch (HttpException ex) { }

                storedTokenTime = DateTime.Now;
                storedTokenValue = "Bearer " + token;
                return storedTokenValue;
            }
        }

        public async Task SetDiscoverSomethingTwitterAsync(TwitAuthenticateResponse twitAuthResponse, string screenName, long idMax, string idLogin)
        {
            var item = await DocumentDBRepository<Login>.GetItemAsync(idLogin);

            // Busca uma lista de tweets do usuário, neste caso os 100 primeiros. Após isso, é feito por thread, pegando os 100 tweets mais antigos que estes, e assim sucessivamente
            var timelineFormat = "";
            var timelineUrl = "";

            if(idMax > 0)
            {
                timelineFormat = "https://api.twitter.com/1.1/statuses/user_timeline.json?screen_name={0}&max_id={1}&include_rts=0&exclude_replies=1&count=25";
                timelineUrl = string.Format(timelineFormat, screenName, idMax);
            }
            else
            {
                timelineFormat = "https://api.twitter.com/1.1/statuses/user_timeline.json?screen_name={0}&include_rts=0&exclude_replies=1&count=25";
                timelineUrl = string.Format(timelineFormat, screenName);
            }
            
            HttpWebRequest timeLineRequest = (HttpWebRequest)WebRequest.Create(timelineUrl);
            var timelineHeaderFormat = "{0} {1}";
            timeLineRequest.Headers.Add("Authorization", string.Format(timelineHeaderFormat, twitAuthResponse.TokenType, twitAuthResponse.AccessToken));
            timeLineRequest.Method = "Get";
            WebResponse timeLineResponse = timeLineRequest.GetResponse();
            var timeLineJson = string.Empty;
            using (timeLineResponse)
            {
                using (var reader = new StreamReader(timeLineResponse.GetResponseStream()))
                {
                    timeLineJson = reader.ReadToEnd();
                }
            }

            dynamic tweets = new JavaScriptSerializer().DeserializeObject(timeLineJson);

            int count = 0;
            List<string> textTweets = new List<string>();
            List<string> idTweets = new List<string>();

            foreach (dynamic tweet in tweets)
            {
                //Joga todos os textos dos tweets dentro de uma lista, e os ids em outra
                textTweets.Add(tweet["text"]);

                long id = tweet["id"];
                idTweets.Add(id.ToString());

                count++;
            }

            //Busca token de acesso para API de Translate
            string authToken = await GetAccessTokenTranslateAsync();

            //Chama API de Tradução, traduzindo o tweet de pt para en
            string[] translates = TranslateArray(authToken, "pt", "en", textTweets.ToArray());
            string[] ids = idTweets.ToArray();

            //Monta objeto para fazer a chamada da API de análise do texto do tweet
            KeyPhrases KeyPhrases = new KeyPhrases();

            for(int i = 0; i < translates.Length; i++)
            {
                Document document = new Document();
                document.Id = ids[i];
                document.Language = "en";
                document.Text = translates[i];

                KeyPhrases.Documents.Add(document);
            }

            //Chama API de Análise de Texto, para buscar as palavras chave da frase tweetada 
            dynamic keyPhrasesObj = GetKeyPhrases(KeyPhrases);

            List<string> keyPhrases = new List<string>();
            foreach (var doc in keyPhrasesObj["documents"])
            {
                //Coloca num stringão todas as palavras chaves separadas por ", "
                keyPhrases.Add(string.Join(", ", doc["keyPhrases"]));
            }

            //Busca token de acesso para API de Translate
            authToken = await GetAccessTokenTranslateAsync();

            //Chama API de Tradução, agora traduzindo as palavras chave em en para pt para mostrar corretamente pro usuário
            string[] translateskeyPhrases = TranslateArray(authToken, "en", "pt", keyPhrases.ToArray());

            for (int i = 0; i < translateskeyPhrases.Length; i++)
            {
                References reference = new References();
                reference.IdTweet = ids[i];
                //tweet.TextTweet = 

                foreach(string keyPhraseTranslate in translateskeyPhrases[i].Split(new[] { ", " }, StringSplitOptions.None))
                {
                    //Verifica se já tem no objeto uma palavra chave com o mesmo texto
                    if(item.KeyPhrases.Count(k => k.Text == keyPhraseTranslate) > 0)
                    {
                        //Se já existe, somente inclui uma nova referência
                        item.KeyPhrases.FirstOrDefault(k => k.Text == keyPhraseTranslate).References.Add(reference);
                    }
                    else
                    {
                        //Caso não exista, cria uma nova
                        KeyPhrase keyPhrase = new KeyPhrase();
                        keyPhrase.Text = keyPhraseTranslate;
                        keyPhrase.References.Add(reference);

                        item.KeyPhrases.Add(keyPhrase);
                    }
                }
            }

            await DocumentDBRepository<Login>.UpdateItemAsync(idLogin, item);

            //ID do último tweet retornado, para consultar os mais antigos a partir desse, na próxima vez
            var lastTweet = tweets[count - 1];
            long id_max = lastTweet["id"];

            //Inicia thread para ir enchendo a base do usuário com mais tweets
            /*new Thread(async () =>
            {
                await SetDiscoverSomethingTwitterAsync(twitAuthResponse, screenName, id_max - 1, idLogin);
            }).Start();*/
        }

        /*public string Translate(string authToken, string from, string to, string text)
        {
            //Faz a tradução de uma frase. É utilizado pois a API de Análise de Texto só suporta o Inglês, aí traduzo e dps retorno pro Português de novo
            string uri = "https://api.microsofttranslator.com/v2/Http.svc/Translate?text=" + HttpUtility.UrlEncode(text) + "&from=" + from + "&to=" + to;
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add("Authorization", authToken);

            var translation = string.Empty;
            using (WebResponse response = httpWebRequest.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                translation = reader.ReadToEnd();
            }

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(translation);

            XmlNodeList elemList = xmlDocument.GetElementsByTagName("string");

            return elemList[0].InnerXml;
        }*/

        public string[] TranslateArray(string authToken, string from, string to, string[] texts)
        {
            //string[] translateArraySourceTexts = { "The answer lies in machine translation.", "the best machine translation technology cannot always provide translations tailored to a site or users like a human ", "Simply copy and paste a code snippet anywhere " };
            string uri = "http://api.microsofttranslator.com/v2/Http.svc/TranslateArray";
            string body = "<TranslateArrayRequest>" +
                             "<AppId />" +
                             "<From>{0}</From>" +
                             "<Options>" +
                                " <Category xmlns=\"http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2\" />" +
                                 "<ContentType xmlns=\"http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2\">{1}</ContentType>" +
                                 "<ReservedFlags xmlns=\"http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2\" />" +
                                 "<State xmlns=\"http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2\" />" +
                                 "<Uri xmlns=\"http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2\" />" +
                                 "<User xmlns=\"http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2\" />" +
                             "</Options>" +
                             "<Texts>";
                                 foreach(var text in texts)
                                 {
                                    body += "<string xmlns=\"http://schemas.microsoft.com/2003/10/Serialization/Arrays\">" + text + "</string>";
                                 }
                   body +=   "</Texts>" +
                             "<To>{2}</To>" +
                          "</TranslateArrayRequest>";

            string reqBody = string.Format(body, from, "text/plain", to);

            // create the request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Headers.Add("Authorization", authToken);
            request.ContentType = "text/xml";
            request.Method = "POST";

            using (System.IO.Stream stream = request.GetRequestStream())
            {
                byte[] arrBytes = System.Text.Encoding.UTF8.GetBytes(reqBody);
                stream.Write(arrBytes, 0, arrBytes.Length);
            }

            // Get the response
            WebResponse response = null;
            try
            {
                response = request.GetResponse();
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader rdr = new StreamReader(stream, System.Text.Encoding.UTF8))
                    {
                        // Deserialize the response
                        string strResponse = rdr.ReadToEnd();
                        XDocument doc = XDocument.Parse(@strResponse);
                        XNamespace ns = "http://schemas.datacontract.org/2004/07/Microsoft.MT.Web.Service.V2";
                        int soureceTextCounter = 0;
                        foreach (XElement xe in doc.Descendants(ns + "TranslateArrayResponse"))
                        {
                            foreach (var node in xe.Elements(ns + "TranslatedText"))
                            {
                                texts[soureceTextCounter] = node.Value;
                            }
                            soureceTextCounter++;
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }
            }

            return texts;
        }

        public dynamic GetKeyPhrases(KeyPhrases objKeyPhrases)
        {
            //Busca as palavras chave de uma frase. Utilizado aqui, para análise dos sentimentos nos tweets do usuário
            var client = new HttpClient();

            // Request headers
            client.DefaultRequestHeaders
                    .Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "6b92be4a92be453dab8591fe28fb86af");

            // Request parameters
            var uri = "https://westus.api.cognitive.microsoft.com/text/analytics/v2.0/keyPhrases";

            JavaScriptSerializer js = new JavaScriptSerializer();
            string jsonKeyPhrases = JsonConvert.SerializeObject(objKeyPhrases);

            //byte[] byteData = Encoding.UTF8.GetBytes("{\"documents\": [{\"language\": \"en\",\"id\": \"1\",\"text\": \"" + text + "\"}]}");
            byte[] byteData = Encoding.UTF8.GetBytes(jsonKeyPhrases);

            var textAnalytics = "";

            HttpResponseMessage response;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = client.PostAsync(uri, content).Result;

                using (var stream = response.Content.ReadAsStreamAsync().Result)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        textAnalytics = reader.ReadToEnd();
                    }
                }
            }

            dynamic textAnalyticsObj = new JavaScriptSerializer().DeserializeObject(textAnalytics);

            return textAnalyticsObj;
        }
    }
}