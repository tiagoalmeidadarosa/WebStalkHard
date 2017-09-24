using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebStalkHard.Models
{
    public class Chatterbot
    {
        public string Id { get; set; }
        public string Token { get; set; }
        public bool DateValid { get; set; }
        public string NomeUser { get; set; }
        public string ImageUser { get; set; }
    }
}