using Microsoft.EntityFrameworkCore;
using WeatherFetchService.Model;

namespace WeatherFetchService.Data
{
    class WeatherContext : DbContext
    {
        public DbSet<Weather> Weather { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseMySql("Server=XXXXXX;Port=XXXXXX;Database=weather;User=XXXXXX;Password=XXXXXX");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<Weather>()
                .HasIndex(b => b.Location);
           
            modelBuilder.Entity<Weather>()
                .HasIndex(b => b.Time);

        }
    }
}