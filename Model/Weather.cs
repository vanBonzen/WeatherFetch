using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherFetchService.Model
{
    class Weather
    {
        public DateTime Time { get; set; }

        public double Temperature { get; set; }

        public int PrecipitationProbability { get; set; }


        public Weather(DateTime time, double temp, int percipitation)
        {
            Time = time;
            Temperature = temp;
            PrecipitationProbability = (int) percipitation;
        }
    }

}
