using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebStalkHard.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            ViewData["Permissoes"] = "public_profile,email,user_friends";
            //Necessário solicitar ao facebook permissão pra utilizar várias permissões
            //,user_about_me,user_actions.books,user_actions.fitness,user_actions.music,user_actions.news,user_actions.video,user_birthday,user_education_history,user_events,user_games_activity,user_hometown,user_likes,user_location,user_managed_groups,user_photos,user_posts,user_relationships,user_relationship_details,user_religion_politics,user_tagged_places,user_videos,user_website,user_work_history,read_custom_friendlists,read_insights,read_audience_network_insights,read_page_mailboxes,manage_pages,publish_pages,publish_actions,rsvp_event,pages_show_list,pages_manage_cta,pages_manage_instant_articles,ads_read,ads_management,business_management,pages_messaging,pages_messaging_subscriptions,pages_messaging_payments,pages_messaging_phone_number

            return View();
        }

        [HttpPost]
        public ActionResult Chatterbot(FormCollection form)
        {
            return View();
        }
    }
}