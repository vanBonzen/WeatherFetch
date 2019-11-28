
using System.Collections.Generic;

namespace WeatherFetchService.Model
{
    public class SignificantWeather
    {

        Dictionary<int, string> significant = new Dictionary<int, string>();
        
        public void Initialize()
        {
            significant.Add(95, "leichtes oder mäßiges Gewitter mit Regen oder Schnee");
            significant.Add(57, "mäßiger oder starker gefrierender Sprühregen");
            significant.Add(56, "leichter gefrierender Sprühregen");
            significant.Add(67, "mäßiger bis starker gefrierender Regen");
            significant.Add(66, "leichter gefrierender Regen");
            significant.Add(86, "mäßiger bis starker Schneeschauer");
            significant.Add(85, "leichter Schneeschauer");
            significant.Add(84, "mäßiger oder starker Schneeregenschauer");
        }
    }
}
