using System;

using System.IO;
using System.IO.Compression;

using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using WeatherFetchService.Model;

namespace WeatherFetchService.Services
{
    public class WeatherFetch : IWeatherFetch
    {
        private HttpClient _httpClient { get; set; }
        private ILogger _logger;

        public WeatherFetch()
        {
            _httpClient = new HttpClient();
        }

        public async Task GetWeather()
        {

            _logger = GenerateLogger();

            // Creating output Directory if not existent
            if (!Directory.Exists(@".\output\"))
            {
                _logger.LogInformation("Creating output Directory...");
                System.IO.Directory.CreateDirectory(@".\output\");
            }

            // Check if we have current Weather Data
            var headers = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://opendata.dwd.de/weather/local_forecasts/mos/MOSMIX_L/single_stations/10637/kml/MOSMIX_L_LATEST_10637.kmz"));
            DateTime lastModifiedOnServer = DateTime.Parse(headers.Content.Headers.LastModified.ToString());

            if (File.Exists(@".\output\WeatherData-" + lastModifiedOnServer.ToString("dd-MM-yyyy_HH-mm") + ".csv"))
            {
                if (Directory.Exists(@".\tmp\")) Directory.Delete(@".\tmp\", true);

                // Success Message
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Environment.NewLine + "File already up to date, nothing to do - Press any key to exit..." + Environment.NewLine);
                Console.ForegroundColor = ConsoleColor.White;
            } else // Fetch new Data
            {
                // Create tmp Directory
                System.IO.Directory.CreateDirectory(@".\tmp\");

                // Get .KMZ Archive from DWD
                _logger.LogInformation("Getting .KMZ Archive from DWD...");
                var response = _httpClient.GetAsync(@"https://opendata.dwd.de/weather/local_forecasts/mos/MOSMIX_L/single_stations/10637/kml/MOSMIX_L_LATEST_10637.kmz");

                // Check for Success
                if (response.Result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (var stream = await response.Result.Content.ReadAsStreamAsync())
                    {
                        // Write to File
                        var fileInfo = new FileInfo(@".\tmp\MOSMIX_L_LATEST_10637.kmz");
                        using (var fileStream = fileInfo.OpenWrite())
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }

                    // Extract .KMZ, so that we get the .KML
                    _logger.LogInformation("Extracting .KMZ...");
                    ZipFile.ExtractToDirectory(@".\tmp\MOSMIX_L_LATEST_10637.kmz", @".\tmp");

                    // Find current .KML
                    FileInfo[] fileToLoad = new DirectoryInfo(@".\tmp\").GetFiles("*" + "_10637" + ".kml");

                    // Import Namespaces for .KML processing
                    _logger.LogInformation("Importing Namespaces...");
                    XNamespace kml = "http://www.opengis.net/kml/2.2";
                    XNamespace dwd = "https://opendata.dwd.de/weather/lib/pointforecast_dwd_extension_V1_0.xsd";

                    // Load into var to close stream after processing
                    FileStream loadedKml = fileToLoad[0].OpenRead();

                    // Get all TimeSteps
                    _logger.LogInformation("Getting TimeSteps...");
                    var doc = XDocument.Load(loadedKml);
                    var timeSteps = doc.Root
                                    .Element(kml + "Document")
                                    .Element(kml + "ExtendedData")
                                    .Element(dwd + "ProductDefinition")
                                    .Element(dwd + "ForecastTimeSteps")
                                    .Elements(dwd + "TimeStep").ToList();

                    // Get all Temperatures
                    _logger.LogInformation("Getting Temperatures...");
                    var temperatures = doc.Root
                                        .Element(kml + "Document")
                                        .Element(kml + "Placemark")
                                        .Element(kml + "ExtendedData")
                                        .Elements(dwd + "Forecast")
                                        .Where(x => x.Attribute(dwd + "elementName").Value.Equals("TTT"))
                                        .FirstOrDefault().Value.Split(" ")
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .Select(y =>
                                        {
                                            double r;
                                            if (double.TryParse(y, out r))
                                                return Math.Round(((r - 27315) / 100), 2);
                                            else
                                                return -1;
                                        })
                                        .ToList();

                    // Get Percipitation Probability
                    _logger.LogInformation("Getting Percipitation Probability...");
                    var percipitation = doc.Root
                                        .Element(kml + "Document")
                                        .Element(kml + "Placemark")
                                        .Element(kml + "ExtendedData")
                                        .Elements(dwd + "Forecast")
                                        .Where(x => x.Attribute(dwd + "elementName").Value.Equals("wwP"))
                                        .FirstOrDefault().Value.Split(" ")
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();

                    loadedKml.Close();

                    // Generating Weather Objects out of Lists
                    List<Weather> weatherList = new List<Weather>();
                    for (int i = 0; i < timeSteps.Count(); i++)
                    {
                        DateTime date = Convert.ToDateTime(timeSteps[i].Value);
                        double temperature = temperatures[i];
                        int percipitationProb = Convert.ToInt32(percipitation[i].Replace(".", string.Empty)) / 100;

                        weatherList.Add(new Weather(date, temperature, percipitationProb));
                    }

                    // Converting Weather-Objects to CSV String
                    _logger.LogInformation("Converting Weather-Objects to CSV String...");
                    String csv = String
                                    .Join(
                                    Environment.NewLine,
                                    weatherList.Select(d => $"{d.Time.ToString()};{d.Temperature.ToString()};{d.PrecipitationProbability.ToString()};")
                                    ).Insert(0, "Datum;Temperatur [C];Niederschlagswahrscheinlichkeit [%]" + Environment.NewLine);

                    // Get written DateTime of .KML File
                    string lastWrittenDate = lastModifiedOnServer.ToString("dd-MM-yyyy_HH-mm");

                    if (!File.Exists(@".\output\WeatherData-" + lastWrittenDate + ".csv"))
                    {
                        // Try to write CSV File
                        WriteCsvFile(lastWrittenDate, csv);
                    }
                    else
                    {
                        _logger.LogInformation("Weather Data already up to Date!");
                    }

                    // Cleaning tmp Files
                    _logger.LogInformation("Deleting '.\\tmp\\' Directory...");
                    Directory.Delete(@".\tmp\", true);

                    // Success Message
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(Environment.NewLine + "          Sucessfull - Press any key to exit..." + Environment.NewLine);
                    Console.ForegroundColor = ConsoleColor.White;

                }
            }
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
            Console.WriteLine(Environment.NewLine + "          Press any key to fetch Weather Data...");
            Console.ForegroundColor = ConsoleColor.White;

            Console.ReadKey();
            Console.Write(Environment.NewLine);
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

        // Check if File is locked
        private bool IsFileLocked(string filename)
        {
            bool Locked = false;
            try
            {
                FileStream fs =
                    File.Open(filename, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
                fs.Close();
            }
            catch (IOException ex)
            {
                Locked = true;
            }
            return Locked;
        }

        // Export CSV as File
        private async Task WriteCsvFile(string lastWrittenDate, string csv)
        {
            if (!IsFileLocked(@".\output\WeatherData-" + lastWrittenDate + ".csv"))
            {
                _logger.LogInformation("Writing CSV to /output/WeatherData-" + lastWrittenDate + ".csv...");
                System.IO.File.WriteAllText(@".\output\WeatherData-" + lastWrittenDate + ".csv", csv);
            }
            else
            {
                _logger.LogWarning("File: /output/WeatherData-" + lastWrittenDate + ".csv is in use." + Environment.NewLine + "Close and press any key to try again");
                Console.ReadKey();
                Console.Write(Environment.NewLine);
                WriteCsvFile(lastWrittenDate, csv);
            }
        }
    }
}

