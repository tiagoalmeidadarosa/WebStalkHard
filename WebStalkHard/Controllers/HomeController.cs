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
            string authToken = await GetAccessTokenTranslateAsync();
            var traducao = Translate(authToken, "pt", "en", "Eu odeio futebol.");

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

            SetDiscoverSomethingTwitter(accessTokenTwitter, userTwitter, 0, id);

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

        public void SetDiscoverSomethingTwitter(TwitAuthenticateResponse twitAuthResponse, string screenName, long idMax, string idLogin)
        {
            // Do the timeline
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

            dynamic timeLineObj = JsonConvert.DeserializeObject(timeLineJson);
            foreach(var tweet in timeLineObj)
            {
                //Todo: Fazer análise do tweet.text.value
            }

            long id_max = timeLineObj[99].id;

            //new Thread(SetDiscoverSomethingTwitter(twitAuthResponse, screenName, id_max - 1, idLogin)).Start();

            //Todo: Ir enviando o resto por thread
            //Todo: E salvar no banco
        }

        private DateTime storedTokenTime = DateTime.MinValue;
        private string storedTokenValue = string.Empty;

        public async Task<string> GetAccessTokenTranslateAsync()
        {
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
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var token = await response.Content.ReadAsStringAsync();
                storedTokenTime = DateTime.Now;
                storedTokenValue = "Bearer " + token;
                return storedTokenValue;
            }
        }

        public string Translate(string authToken, string from, string to, string text)
        {
            string uri = "https://api.microsofttranslator.com/v2/Http.svc/Translate?text=" + HttpUtility.UrlEncode(text) + "&from=" + from + "&to=" + to;
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Headers.Add("Authorization", authToken);

            var translation = string.Empty;
            using (WebResponse response = httpWebRequest.GetResponse())
            /*using (Stream stream = response.GetResponseStream())
            {
                DataContractSerializer dcs = new DataContractSerializer(Type.GetType("System.String"));
                string translation = (string)dcs.ReadObject(stream);
                Console.WriteLine("Translation for source text '{0}' from {1} to {2} is", text, "en", "de");
                Console.WriteLine(translation);
            }*/
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                translation = reader.ReadToEnd();
            }

            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(translation);

            XmlNodeList elemList = xmlDocument.GetElementsByTagName("string");

            return elemList[0].InnerXml;
        }
    }
}