using FileHelpers;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitUserBrowser
{
    [DelimitedRecord(",")]
    public class SerializableUser
    {
        public SerializableUser() { }

        public SerializableUser(User usr)
        {
            Login = usr.Login;
            Name = usr.Name;
            Email = usr.Email;
            Company = usr.Company;
            URL = usr.Url;
            Hireable = usr.Hireable.HasValue ? usr.Hireable.ToString() : string.Empty;
        }

        public string Login { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Company { get; set; }
        public string URL { get; set; }
        public string Hireable { get; set; }
    }
}
