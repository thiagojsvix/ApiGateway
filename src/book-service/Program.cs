using BookService.Settings;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace product_service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", true, true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                        {
                            var serviceSettings = configuration.GetSection("ServiceSettings").Get<ServiceSettings>();

                            options.AddServerHeader = false;
                            options.ListenAnyIP(serviceSettings.ServicePort);
                           
                        }).UseStartup<Startup>();
                });
        }
    }
}