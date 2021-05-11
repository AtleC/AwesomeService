using System.Collections.Generic;

namespace AwesomeService
{
    class OnLoadVariables
    {
        public static string DefaultScreenshotPath { get; internal set; }
        public static List<Models.User> Users { get; internal set; }
    }
}
