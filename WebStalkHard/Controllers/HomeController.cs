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

        [HttpPost]
        [ActionName("Create")]
        public async Task<ActionResult> CreateAsync(FormCollection form)
        {
            Login login = new Login();
            login.UserFacebook = form["inputUserFacebook"];
            login.AccessTokenFacebook = form["hiddenAccessToken"];
            
            var userTwitter = form["inputUserTwitter"];
            if(userTwitter[0].Equals('@'))
            {
                userTwitter = userTwitter.Substring(1);
            }
            login.UserTwitter = userTwitter;

            var accessTokenTwitter = GetAccessTokenTwitter();
            login.AccessTokenTwitter = accessTokenTwitter;

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
            var item = await DocumentDBRepository<Login>.GetItemAsync(id);

            return View(item);
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
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri("https://api.cognitive.microsoft.com/sts/v1.0/issueToken");
                request.Content = new StringContent(string.Empty);
                request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", "efc56f55f859425885cd2a46ed75fb55");
                client.Timeout = TimeSpan.FromSeconds(2);

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
            // Busca uma lista de tweets do usuário, neste caso os 100 primeiros. Após isso, é feito por thread, pegando os 100 tweets mais antigos que estes, e assim sucessivamente
            var timelineFormat = "";
            var timelineUrl = "";

            if(idMax > 0)
            {
                timelineFormat = "https://api.twitter.com/1.1/statuses/user_timeline.json?screen_name={0}&max_id={1}&include_rts=0&exclude_replies=1&count=100";
                timelineUrl = string.Format(timelineFormat, screenName, idMax);
            }
            else
            {
                timelineFormat = "https://api.twitter.com/1.1/statuses/user_timeline.json?screen_name={0}&include_rts=0&exclude_replies=1&count=100";
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

            KeyPhrases KeyPhrases = new KeyPhrases();

            int count = 0;
            dynamic tweets = new JavaScriptSerializer().DeserializeObject(timeLineJson);
            foreach(dynamic tweet in tweets)
            {
                count++;

                //Busca token de acesso para API de Translate
                string authToken = await GetAccessTokenTranslateAsync();

                //Chama API de Tradução, traduzindo o tweet de pt para en
                string traducao = Translate(authToken, "pt", "en", tweet["text"]);

                Document document = new Document();
                document.Id = count.ToString();
                document.Language = "en";
                document.Text = traducao;

                KeyPhrases.Documents.Add(document);
            }

            //Chama API de Análise de Texto, para buscar as palavras chave da frase tweetada 
            dynamic keyPhrasesObj = GetKeyPhrases(KeyPhrases);

            foreach (var doc in keyPhrasesObj["documents"])
            {
                //Busca token de acesso para API de Translate
                string authToken = await GetAccessTokenTranslateAsync();

                //Coloca num stringão todas as palavras chaves separadas por ", "
                string keyPhrases = string.Join(", ", doc["keyPhrases"]);
                //Chama API de Tradução, agora traduzindo as palavras chave em en para pt para mostrar corretamente pro usuário
                var traducaokeyPhrases = Translate(authToken, "en", "pt", keyPhrases);

                //Todo: Inserir de alguma forma no banco as keyPhrases
            }

            //ID do último tweet retornado, para consultar os mais antigos a partir desse, na próxima vez
            var lastTweet = tweets[count - 1];
            long id_max = lastTweet["id"];

            //Inicia thread para ir enchendo a base do usuário com mais tweets
            /*new Thread(async () =>
            {
                await SetDiscoverSomethingTwitterAsync(twitAuthResponse, screenName, id_max - 1, idLogin);
            }).Start();*/
        }

        public string Translate(string authToken, string from, string to, string text)
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