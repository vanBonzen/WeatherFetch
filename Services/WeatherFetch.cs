using System;

using System.IO;
using System.IO.Compression;

using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;


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

            // Creating Directorys if not existent
            _logger.LogInformation("Creating Directorys if not existent...");
            System.IO.Directory.CreateDirectory(@".\tmp\");
            System.IO.Directory.CreateDirectory(@".\output\");

            #region Delete old fetched Files
            _logger.LogInformation("Deleting old Files if exist...");
            // Search for Files
            FileInfo[] filesInDir = new DirectoryInfo(@".\tmp\").GetFiles("*" + "_10637" + "*");

            // Delete all found Files
            foreach (FileInfo foundFile in filesInDir)
            {
                _logger.LogInformation("Found old temporary Data - deleting...");
                foundFile.Delete();
            }

            if (File.Exists(@".\output\WeatherData.csv"))
            {
                // If file found, delete it    
                File.Delete(@".\output\WeatherData.csv");
                _logger.LogInformation("Found old WeatherData.csv - deleting...");
            }
            #endregion

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
                                .Elements(dwd + "TimeStep");

                List<DateTime> dateTimes = new List<DateTime>();

                // Convert TimeSteps to DateTime
                foreach (var timeStep in timeSteps)
                {
                    DateTime date = Convert.ToDateTime(timeStep.Value);

                    // Add converted TimeStep to dateTimes List
                    dateTimes.Add(date);
                }

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

                loadedKml.Close();

                // Combine TimeSteps with Temperatures
                _logger.LogInformation("Combine TimeSteps with Temperatures...");
                Dictionary<DateTime, double> temps = new Dictionary<DateTime, double>();

                // For 72h combine
                for (var i = 0; i < 72; i++)
                {
                    temps.Add(dateTimes[i], temperatures[i]);
                }

                // Convert Dictionary to CSV
                _logger.LogInformation("Converting Dictionary to CSV...");
                String csv = String
                                .Join(
                                Environment.NewLine,
                                temps.Select(d => $"{d.Key};{d.Value};")
                                ).Insert(0, "Datum;Temperatur [C]" + Environment.NewLine);

                // Get written DateTime of .KML File
                string lastWrittenDate = File.GetLastWriteTime(fileToLoad[0].ToString()).ToString("dd-MM-yyyy_HH-mm");

                WriteCsvFile(lastWrittenDate, csv);

                // Cleaning tmp Files
                _logger.LogInformation("Deleting '.\\tmp\\' Directory...");
                Directory.Delete(@".\tmp\", true);

                // Success Message
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Environment.NewLine + "          Sucessfull - Press any key to exit..." + Environment.NewLine);
                Console.ForegroundColor = ConsoleColor.White;
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

