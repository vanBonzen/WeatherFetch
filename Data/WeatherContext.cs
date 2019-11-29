using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore;
using WeatherFetchService.Model;

namespace WeatherFetchService.Data
{
    class WeatherContext : DbContext
    {
        public DbSet<Weather> Weather { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseMySql("Server=XXXXXX;Port=XXXXXX;Database=weather;User=XXXXXX;Password=XXXXXX");
    }
}
