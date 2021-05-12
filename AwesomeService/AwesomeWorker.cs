using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using AwesomeService.Classes;
using Serilog;

namespace AwesomeService
{
    public class AwesomeWorker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Logger = new LoggerConfiguration()
            .WriteTo.EventLog("Awesome Service", manageEventSource: true)
            .MinimumLevel.Debug()
            .CreateLogger();

            FileAccessWatcher.FileFinishedCopying += FileAccessWatcher_FileFinishedCopying;
            OnLoadVariables.DefaultScreenshotPath = @"\Pictures\Screenshot";

            var watchers = new List<FileSystemWatcher>();
            var ScreenShotManagement = new ScreenShotManagement();

            Log.Information("Service started");
            foreach (var User in ScreenShotManagement.GetScreenshotLocations())
            {
                var watcher = new FileSystemWatcher(User.ScreenshotFolder) { 
                    Filter = "*.png*",
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                watcher.Changed += new FileSystemEventHandler(FileAccessWatcher_OnChanged);
                watchers.Add(watcher);
            }

            /// listen for service cancelation
            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            await tcs.Task;

            Log.Information("Service stopped");
        }

        private void FileAccessWatcher_OnChanged(object source, FileSystemEventArgs e)
        {
            // start monitoring the file (put this inside the OnChanged event handler of the FileSystemWatcher
            FileAccessWatcher.RegisterWaitForFileAccess(e.FullPath);
        }

        private void FileAccessWatcher_FileFinishedCopying(object sender, FileSystemEventArgs e)
        {
            var user = OnLoadVariables.Users.Find(x => x.ScreenshotFolder == new DirectoryInfo(e.FullPath).Parent.FullName);
            string destination = user.UserSpesificScreenshotFolder;
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            using (FileStream fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read))
            {
                using (Image img = Image.FromStream(fs))
                {
                    if (img.Width == 364 || img.Height == 180)
                    {
                        return;
                    }
                }
            }

            string format = "png";
            long nume = DateTime.Now.ToFileTime();
            string screenshotpath =  Path.Combine(destination + "\\" + nume + "." + format);
            File.Copy(e.FullPath, screenshotpath, true);
            Log.Information("Screenshot " + screenshotpath + " copied");
        }
    }

}
