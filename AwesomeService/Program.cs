using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
namespace AwesomeService
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            return await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(async (CommandLineOptions opts) =>
                {
                    // We have the parsed arguments, so let's just pass them down
                    await CreateHostBuilder(args, opts).Build().RunAsync();
                    return 0;
                },
                errs => Task.FromResult(-1)); // Invalid arguments
        }

        public static IHostBuilder CreateHostBuilder(string[] args, CommandLineOptions opts) =>
            Host.CreateDefaultBuilder(args)
                
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(opts);
                    services.AddHostedService<AwesomeWorker>();
                        
                }).UseWindowsService();
    }
}

