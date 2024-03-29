﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using WeatherFetchService.Data;
using WeatherFetchService.Model;

namespace WeatherFetchService.Services
{
    public class WeatherFetch : IWeatherFetch
    {
        private HttpClient _httpClient { get; set; }
        private ILogger _logger;

        private readonly XNamespace kml = "http://www.opengis.net/kml/2.2";
        private readonly XNamespace dwd = "https://opendata.dwd.de/weather/lib/pointforecast_dwd_extension_V1_0.xsd";

        public WeatherFetch()
        {
            _httpClient = new HttpClient();
        }

        public async Task GetWeather()
        {
            // Generate logger
            _logger = GenerateLogger();

            // Add Stations to fetch
            List<KeyValuePair<string, string>> weatherLocations = new List<KeyValuePair<string, string>>();
            weatherLocations.Add(new KeyValuePair<string, string>("N2254", "Rüsselsheim"));
            weatherLocations.Add(new KeyValuePair<string, string>("P0361", "Hochheim"));
            weatherLocations.Add(new KeyValuePair<string, string>("10637", "Frankfurt"));
            weatherLocations.Add(new KeyValuePair<string, string>("L829", "Raunheim"));

            // String for later comparison if WeatherData is outdated
            string timeWritten = "aloha";

            // Iterate over all Stations
            foreach (KeyValuePair<string,string> station in weatherLocations)
            {
                // Check if we have current Weather Data
                _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Fetching Weather for " + station.Value);
                var uri = "https://opendata.dwd.de/weather/local_forecasts/mos/MOSMIX_L/single_stations/" + station.Key + "/kml/MOSMIX_L_LATEST_" + station.Key + ".kmz";
                var headers = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri));

                // Get last modified Date
                timeWritten = DateTime.Parse(headers.Content.Headers.LastModified.ToString()).ToString("dd-MM-yyyy_HH-mm");

                if (File.Exists(@".\tmp\MOSMIX_L_LATEST_" + station.Key + "_" + timeWritten + ".kmz"))
                {
                    // Success Message
                    Console.ForegroundColor = ConsoleColor.Green;
                    _logger.LogInformation(Environment.NewLine + "Data already up to date, nothing to do..." + Environment.NewLine);
                    Console.ForegroundColor = ConsoleColor.White;

                } else
                {
                    // Create tmp Directory
                    System.IO.Directory.CreateDirectory(@".\tmp\");

                    // Get .KMZ Archive from DWD
                    _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Getting .KMZ Archive from DWD...");
                    var response = _httpClient.GetAsync(@"https://opendata.dwd.de/weather/local_forecasts/mos/MOSMIX_L/single_stations/" + station.Key + "/kml/MOSMIX_L_LATEST_" + station.Key + ".kmz");

                    // Check for Success
                    if (response.Result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        using (var stream = await response.Result.Content.ReadAsStreamAsync())
                        {
                            // Write to File
                            var fileInfo = new FileInfo(@".\tmp\MOSMIX_L_LATEST_" + station.Key + "_" + timeWritten + ".kmz");
                            using (var fileStream = fileInfo.OpenWrite())
                            {
                                await stream.CopyToAsync(fileStream);
                            }
                        }

                        // Extract .KMZ, so that we get the .KML
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Extracting .KMZ...");
                        ZipFile.ExtractToDirectory(@".\tmp\MOSMIX_L_LATEST_" + station.Key + "_" + timeWritten + ".kmz", @".\tmp", true);

                        // Find current .KML
                        FileInfo[] fileToLoad = new DirectoryInfo(@".\tmp\").GetFiles("*" + "_" + station.Key + ".kml");

                        // Load into var to close stream after processing
                        FileStream loadedKml = fileToLoad[0].OpenRead();

                        // Get all TimeSteps
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Getting TimeSteps...");
                        var doc = XDocument.Load(loadedKml);
                        var time = doc.Root
                                            .Element(kml + "Document")
                                            .Element(kml + "ExtendedData")
                                            .Element(dwd + "ProductDefinition")
                                            .Element(dwd + "ForecastTimeSteps")
                                            .Elements(dwd + "TimeStep").ToList();

                        // Get all Temperatures
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Getting Temperatures...");
                        var temperatures = await GetWeatherFeature(doc, "TTT");

                        // Get Percipitation Probability
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Getting Percipitation Probability...");
                        var percipitation = await GetWeatherFeature(doc, "wwP");

                        // Get Windspeed
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Getting Windspeed...");
                        var speed = await GetWeatherFeature(doc, "FF");

                        // Get Winddirection
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Getting Winddirection...");
                        var wind = await GetWeatherFeature(doc, "DD");

                        // Close Filestream
                        loadedKml.Close();

                        // Generating Weather Objects out of Lists
                        List<Weather> weatherList = new List<Weather>();
                        for (int i = 0; i < time.Count(); i++)
                        {
                            DateTime date = Convert.ToDateTime(time[i].Value);
                            double windSpeed = Convert.ToDouble(speed[i]) / 100;
                            double temperature = (Convert.ToDouble(temperatures[i]) - 27315) / 100;
                            int windDirection = Convert.ToInt32(wind[i].Replace(".", string.Empty)) / 100;
                            int percipitationProb = Convert.ToInt32(percipitation[i].Replace(".", string.Empty)) / 100;

                            weatherList.Add(new Weather(date, temperature, percipitationProb, station.Value, windSpeed, windDirection));
                        }

                        // Writing to DB
                        _logger.LogInformation(DateTime.Now.ToString("dd-MM-yyyy HH:mm: ") + "Updating Database...");
                        using (var db = new WeatherContext())
                        {
                            foreach (Weather weather in weatherList)
                            {
                                Weather weatherFromDb = db.Weather.Where(o => o.Time == weather.Time && o.Location == weather.Location).SingleOrDefault();
                                if (weatherFromDb != null)
                                {
                                    // Check for differencys, if any set new Values
                                    if (weatherFromDb.WindDirection != weather.WindDirection) weatherFromDb.WindDirection = weather.WindDirection;
                                    if (weatherFromDb.Temperature != weather.Temperature) weatherFromDb.Temperature = weather.Temperature;
                                    if (weatherFromDb.WindSpeed != weather.WindSpeed) weatherFromDb.WindSpeed = weather.WindSpeed;
                                }
                                else
                                {
                                    // New Wheather to add
                                    db.Weather.Add(weather);
                                }
                            }
                            // Save changes to Database
                            db.SaveChanges();
                        }
                        // Success Message
                        _logger.LogInformation(Environment.NewLine + station.Value + " updated sucessfull" + Environment.NewLine);
                    }
                }
            }
            // Cleaning old .KMZ Files
            List<string> kmzFiles = Directory.GetFiles(@"./tmp/", "*.kmz").ToList();

            foreach (string kmzFile in kmzFiles)
            {
                if (!kmzFile.Contains(timeWritten))
                {
                    File.Delete(kmzFile);
                }
            }
            // Cleaning old .KML Files
            List<string> kmlFiles = Directory.GetFiles(@"./tmp/", "*.kml").ToList();

            foreach (string kmlFile in kmlFiles)
            {
                File.Delete(kmlFile);
            }
        }

        private async Task<List<string>> GetWeatherFeature(XDocument input, string feature)
        {
            return input.Root
                        .Element(kml + "Document")
                        .Element(kml + "Placemark")
                        .Element(kml + "ExtendedData")
                        .Elements(dwd + "Forecast")
                        .Where(x => x.Attribute(dwd + "elementName").Value.Equals(feature))
                        .FirstOrDefault().Value.Split(" ")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }

        public async Task Intro()
        {
            // Console Art
            Console.WriteLine(" __    __           _   _                ___    _       _     ");
            Console.WriteLine("/ / /\\ \\ \\___  __ _| |_| |__   ___ _ __ / __\\__| |_ ___| |__  ");
            Console.WriteLine("\\ \\/  \\/\\/ _ \\/ _` | __| '_ \\ / _ \\ '__/ _\\/ _ \\ __/ __| '_ \\ ");
            Console.WriteLine(" \\  /\\  /  __/ (_| | |_| | | |  __/ | / / |  __/ || (__| | | |");
            Console.WriteLine("  \\/  \\/ \\___|\\__,_|\\__|_| |_|\\___|_| \\/   \\___|\\__\\___|_| |_|");


            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(Environment.NewLine + "                Copyright by vanBonzen");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(Environment.NewLine);
        }

        private static ILogger GenerateLogger()
        {
            // Logger Factory
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            });

            ILogger logger = loggerFactory.CreateLogger("Weather Fetch Service");
            return logger;
        }
    }
}

