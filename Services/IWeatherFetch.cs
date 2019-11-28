using System.Threading.Tasks;

namespace WeatherFetchService.Services
{
    public interface IWeatherFetch
    {
        Task GetWeather();
        Task<char> Intro();
    }
}