using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using AwesomeService.Classes;

namespace AwesomeService
{
    public class AwesomeWorker : BackgroundService
    {
        private readonly ILogger<AwesomeWorker> _logger;
        private readonly CommandLineOptions _options;

        public AwesomeWorker(ILogger<AwesomeWorker> logger, CommandLineOptions options)
        {
            _logger = logger;
            _options = options;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FileAccessWatcher.FileFinishedCopying += FileAccessWatcher_FileFinishedCopying;

            OnLoadVariables.DefaultScreenshotPath = @"\Pictures\Screenshot";
            var watchers = new List<FileSystemWatcher>();

            _logger.LogInformation("Service started");
            foreach (var User in GetScreenshotLocations())
            {
                var watcher = new FileSystemWatcher(User.ScreenshotFolder) { 
                    Filter = "*.png*",
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                watcher.Created += async (object sender, FileSystemEventArgs e) =>
                {
                    await Task.Delay(1000);
                    await ProcessFileAsync(e.FullPath, User);
                };
                watcher.Changed += new FileSystemEventHandler(OnChanged);
                watchers.Add(watcher);
            }

            /// listen for service cancelation
            var tcs = new TaskCompletionSource<bool>();
            stoppingToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            await tcs.Task;

            _logger.LogInformation("Service stopped");
        }
        private List<Models.User> GetScreenshotLocations()
        {
            List<Models.User> result = new();
            string SystemDrive = Path.GetPathRoot(Environment.SystemDirectory);

            //Look for screenshot folder foreach user profile
            foreach (var Userfolder in Directory.EnumerateDirectories(SystemDrive + @"Users", "*"))
            {
                try
                {
                    string baselocation = Directory.EnumerateDirectories(Userfolder + @"\AppData\Local\Packages\", "MicrosoftWindows.Client.CBS*").FirstOrDefault();
                    string ScreenshotLocation = Directory.EnumerateDirectories(baselocation + "\\TempState", "ScreenClip*").FirstOrDefault();
                    if (ScreenshotLocation == null) { break; }
                    var UserName = new DirectoryInfo(Userfolder).Name;

                    var UserSpesificScreenshotFolder = RegistryManagement.ReadOfflineHive(UserName, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "My Pictures");
                    if (UserSpesificScreenshotFolder == null)
                    {
                        string sid = Helpers.ResolveUsername(UserName);
                        UserSpesificScreenshotFolder = RegistryManagement.ReadUserRegistryKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders", "My Pictures", sid);
                    }
                    UserSpesificScreenshotFolder = UserSpesificScreenshotFolder + "\\Screenshots";
                    Models.User User = new Models.User
                    {
                        Name = UserName,
                        UserFolder = Userfolder,
                        ScreenshotFolder = ScreenshotLocation.ToString(),
                        UserSpesificScreenshotFolder = UserSpesificScreenshotFolder
                    };

                    result.Add(User);
                    _logger.LogInformation($"Screenshot location found: " + ScreenshotLocation.ToString());
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogInformation(ex.Message);
                }
                catch (PathTooLongException ex)
                {
                    _logger.LogInformation(ex.Message);
                }
                catch (DirectoryNotFoundException ex)
                {
                    _logger.LogInformation(ex.Message);
                }
            }
            OnLoadVariables.Users = result;
            return result;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // start monitoring the file (put this inside the OnChanged event handler of the FileSystemWatcher
            FileAccessWatcher.RegisterWaitForFileAccess(e.FullPath);
        }

        private async Task<bool> ProcessFileAsync(string filePath, Models.User User)
        {
            FileAccessWatcher.RegisterWaitForFileAccess(filePath);
            return false; // Not enough confidence or error
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
            File.Copy(e.FullPath, Path.Combine(destination + "\\" + nume + "." + format), true);
            //Copies file to another directory.
        }
    }
    public class FileAccessWatcher
    {
        // this list keeps track of files being watched
        private static ConcurrentDictionary<string, FileAccessWatcher> watchedFiles = new ConcurrentDictionary<string, FileAccessWatcher>();

        public static void RegisterWaitForFileAccess(string filePath)
        {
            // if the file is already being watched, don't do anything
            if (watchedFiles.ContainsKey(filePath))
            {
                return;
            }
            // otherwise, start watching it
            FileAccessWatcher accessWatcher = new FileAccessWatcher(filePath);
            watchedFiles[filePath] = accessWatcher;
            accessWatcher.StartWatching();
        }

        /// <summary>
        /// Event triggered when the file is finished copying or when the file size has not increased in the last 5 minutes.
        /// </summary>
        public static event FileSystemEventHandler FileFinishedCopying;

        private static readonly TimeSpan MaximumIdleTime = TimeSpan.FromMinutes(5);

        private readonly FileInfo file;


        private long lastFileSize = 0;

        private DateTime timeOfLastFileSizeIncrease = DateTime.Now;

        private FileAccessWatcher(string filePath)
        {
            this.file = new FileInfo(filePath);
        }

        private Task StartWatching()
        {
            return Task.Factory.StartNew(this.RunLoop);
        }

        private void RunLoop()
        {
            while (this.IsFileLocked())
            {
                long currentFileSize = this.GetFileSize();
                if (currentFileSize > this.lastFileSize)
                {
                    this.lastFileSize = currentFileSize;
                    this.timeOfLastFileSizeIncrease = DateTime.Now;
                }

                // if the file size has not increased for a pre-defined time limit, cancel
                if (DateTime.Now - this.timeOfLastFileSizeIncrease > MaximumIdleTime)
                {
                    break;
                }
            }
            this.RemoveFromWatchedFiles();
            this.RaiseFileFinishedCopyingEvent();
        }

        private void RemoveFromWatchedFiles()
        {
            FileAccessWatcher accessWatcher;
            watchedFiles.TryRemove(this.file.FullName, out accessWatcher);
        }

        private void RaiseFileFinishedCopyingEvent()
        {
            FileFinishedCopying?.Invoke(this,
                new FileSystemEventArgs(WatcherChangeTypes.Changed, this.file.Directory.FullName, this.file.Name));
        }

        private long GetFileSize()
        {
            return this.file.Length;
        }

        private bool IsFileLocked()
        {
            try
            {
                using (this.file.Open(FileMode.Open)) { }
            }
            catch (IOException e)
            {
                var errorCode = Marshal.GetHRForException(e) & ((1 << 16) - 1);

                return errorCode == 32 || errorCode == 33;
            }
            return false;
        }
    }

}
