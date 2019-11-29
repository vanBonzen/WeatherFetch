using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WeatherFetchService.Services;

namespace WeatherFetchService
{
    class Program
    {
        static System.Timers.Timer timer = new System.Timers.Timer();
        public static IWeatherFetch weatherFetch = new WeatherFetch();


        static async Task Main(string[] args)
        {
            // Set Console Properties
            Console.Title = "WeatherFetch Service";

            CancellationToken token = new CancellationToken();
            int intervall = 30;
            await weatherFetch.Intro();
            await StartTimer(token, intervall);
        }

        public static async Task StartTimer(CancellationToken cancellationToken, int intervall)
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    await weatherFetch.GetWeather();
                    await Task.Delay(200);
                    Console.WriteLine("Waiting " + intervall.ToString() + " seconds to fetch next Data..." + Environment.NewLine);
                    await Task.Delay(1000 * intervall, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        break;
                }
            });
        }
    }
}
