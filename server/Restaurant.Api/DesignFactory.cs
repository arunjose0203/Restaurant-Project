using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Restaurant.Api;
public class DesignFactory:IDesignTimeDbContextFactory<RestaurantDb>{public RestaurantDb CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<RestaurantDb>().UseNpgsql("Host=localhost;Database=design_only;Username=design_only;Password=design_only").Options);}
