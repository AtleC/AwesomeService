using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AwesomeService.Classes
{
    internal class ScreenShotManagement
    {
        internal List<Models.User> GetScreenshotLocations()
        {
            List<Models.User> result = new();
            string SystemDrive = Path.GetPathRoot(Environment.SystemDirectory);

            //Look for screenshot folder foreach user profile
            foreach (var Userfolder in Directory.EnumerateDirectories(SystemDrive + @"Users", "*"))
            {
                try
                {
                    var UserName = new DirectoryInfo(Userfolder).Name;
                    if (UserName.ToLower() == "default user" || UserName.ToLower() == "default" || UserName.ToLower() == "public" || UserName.ToLower() == "all users") 
                    {
                        // These are default account that it not used, skip
                        continue;
                    }
                    string baselocation = Directory.EnumerateDirectories(Userfolder + @"\AppData\Local\Packages\", "MicrosoftWindows.Client.CBS*").FirstOrDefault();
                    string ScreenshotLocation = Directory.EnumerateDirectories(baselocation + "\\TempState", "ScreenClip*").FirstOrDefault();
                    if (ScreenshotLocation == null) 
                    {
                        // This usually means that the user have never before used the application
                        Log.Warning(Properties.Resources.PathNotFoundError, baselocation + "\\TempState\\ScreenClip");
                        continue; 
                    }

                    var UserSpesificScreenshotFolder = RegistryManagement.ReadOfflineHive(UserName, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "My Pictures");
                    if (UserSpesificScreenshotFolder == null)
                    {
                        string sid = Helpers.ResolveUsername(UserName);
                        UserSpesificScreenshotFolder = RegistryManagement.ReadUserRegistryKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "My Pictures", sid);
                    }
                    UserSpesificScreenshotFolder = UserSpesificScreenshotFolder + "\\Screenshots";
                    Models.User User = new()
                    {
                        Name = UserName,
                        UserFolder = Userfolder,
                        ScreenshotFolder = ScreenshotLocation.ToString(),
                        UserSpesificScreenshotFolder = UserSpesificScreenshotFolder
                    };

                    result.Add(User);
                    Log.Information(Properties.Resources.ScreenshotPathFound, ScreenshotLocation.ToString());
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Warning(ex,Properties.Resources.AccessDeniedError);
                }
                catch (PathTooLongException ex)
                {
                    Log.Warning(ex, Properties.Resources.PathTooLongError);
                }
                catch (DirectoryNotFoundException ex)
                {
                    Log.Warning(ex, Properties.Resources.PathNotFoundError);
                }
            }
            OnLoadVariables.Users = result;
            return result;
        }

    }
}
