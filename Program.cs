using Microsoft.Extensions.Logging;
using System;
using WeatherFetchService.Services;

namespace WeatherFetchService
{
    class Program
    {
        static void Main(string[] args)
        {
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
