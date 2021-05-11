using System;
using System.Security.Principal;

namespace AwesomeService.Classes
{
    class Helpers
    {
        public static string ResolveUsername (string username)
        {
            NTAccount f = new NTAccount(username);
            SecurityIdentifier s = (SecurityIdentifier)f.Translate(typeof(SecurityIdentifier));
            String sidString = s.ToString();
            return sidString;
        }
    }
}
