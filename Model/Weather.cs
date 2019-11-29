using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public double WindSpeed { get; set; }
        public int WindDirection { get; set; }

        public Weather()
        {

        }

        public Weather(DateTime time, double temp, int percipitation, string location, double speed, int direction)
        {
            Time = time;
            Temperature = temp;
            PrecipitationProbability = (int) percipitation;
            Location = location;
            WindSpeed = speed;
            WindDirection = direction;
        }
    }

}
