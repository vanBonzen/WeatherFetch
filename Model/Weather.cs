using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherFetchService.Model
{
    class Weather
    {
        public DateTime Time { get; set; }

        public double Temperature { get; set; }


        public Weather(DateTime time, double temp)
        {
            Time = time;
            Temperature = temp;
        }
    }

}
