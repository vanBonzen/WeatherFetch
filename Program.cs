using Microsoft.Extensions.Logging;
using System;
using WeatherFetchService.Services;

namespace WeatherFetchService
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "WeatherFetch Service";
            Console.SetWindowSize(64, 38);
            Console.SetBufferSize(64, 76);

            // Weather Fetch Service
            IWeatherFetch weatherFetch = new WeatherFetch();

            // Intro
            weatherFetch.Intro();

            // Getting Weather
            weatherFetch.GetWeather();

            Console.ReadKey();
        }
    }
}
