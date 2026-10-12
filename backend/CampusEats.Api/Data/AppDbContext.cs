using Microsoft.EntityFrameworkCore;
namespace CampusEats.Api.Data;
public sealed class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = "";
    public bool IsVegetarian { get; set; }
    public int PreparationMinutes { get; set; }
}
public sealed class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<AppUser> Users => Set<AppUser>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<MenuItem>().Property(x => x.Price).HasPrecision(10, 2);
        model.Entity<MenuItem>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<MenuItem>().Property(x => x.Category).HasMaxLength(60);
        model.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        model.Entity<AppUser>().Property(x => x.Email).HasMaxLength(254);
        model.Entity<MenuItem>().HasData(
            Dish(1,"Chicken rice bowl","Seasoned chicken, fluffy rice and a fresh side salad.","Meals",650,"photo-1546069901-ba9599a7e63c",false,12),
            Dish(2,"Campus cheeseburger","Grilled patty, melted cheese and our house sauce.","Burgers",750,"photo-1568901346375-23c9450c58cd",false,15),
            Dish(3,"Garden salad bowl","Crunchy greens, seasonal vegetables and citrus dressing.","Meals",450,"photo-1512621776951-a57141f2eefd",true,8),
            Dish(4,"Crispy fries","Golden potato fries with a little sea salt.","Snacks",300,"photo-1573080496219-bb080dd4f877",true,7),
            Dish(5,"Iced latte","Espresso and chilled milk over ice. Your lecture companion.","Drinks",380,"photo-1461023058943-07fcbe16d735",true,5),
            Dish(6,"Chocolate brownie","A rich chocolate square for a well-earned study break.","Desserts",280,"photo-1606313564200-e75d5e30476c",true,3),
            Dish(7,"Veggie burger","A hearty vegetable patty with crisp lettuce and tomato.","Burgers",600,"photo-1550547660-d9450f859349",true,12),
            Dish(8,"Fresh orange juice","A bright, refreshing glass of freshly squeezed orange.","Drinks",350,"photo-1613478223719-2ab802602423",true,4)
        );
    }
    private static MenuItem Dish(int id,string name,string description,string category,decimal price,string photo,bool vegetarian,int minutes) => new()
    { Id=id, Name=name, Description=description, Category=category, Price=price, ImageUrl=$"https://images.unsplash.com/{photo}?auto=format&fit=crop&w=800&q=80", IsVegetarian=vegetarian, PreparationMinutes=minutes };
}
