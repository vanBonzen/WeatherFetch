using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using WeatherFetchService.Services;

namespace WeatherFetchService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Set Console Properties
            Console.Title = "WeatherFetch Service";

            // Weather Fetch Service
            IWeatherFetch weatherFetch = new WeatherFetch();

            // Getting Weather
            await weatherFetch.GetWeather();

            Console.ReadKey();
        }
    }
}
