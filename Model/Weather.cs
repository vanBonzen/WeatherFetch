using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace WeatherFetchService.Model
{
    class Weather
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Location { get; set; }
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public int PrecipitationProbability { get; set; }
        // TODO: Add those 2:
        public int WindSpeed { get; set; }
        public int WindDirection { get; set; }

        public Weather()
        {

        }

        public Weather(DateTime time, double temp, int percipitation, string location)
        {
            Time = time;
            Temperature = temp;
            PrecipitationProbability = (int) percipitation;
            Location = location;
        }
    }

}
