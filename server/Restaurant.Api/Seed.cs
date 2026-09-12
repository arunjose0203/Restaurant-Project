using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Restaurant.Api;
public static class Seed {
 public static async Task Run(RestaurantDb db,IPasswordHasher<User> hasher,IConfiguration config){
 if(!await db.Users.AnyAsync()){var password=config["Bootstrap:Password"]??"";if(password.Length<12)throw new InvalidOperationException("Set Bootstrap:Password to at least 12 characters before initial migration.");var user=new User{Name="Restaurant admin",Email=(config["Bootstrap:Email"]??"admin@tableflow.local").ToLower(),Role="Admin"};user.PasswordHash=hasher.HashPassword(user,password);db.Users.Add(user);}
 if(!await db.Categories.AnyAsync()){
 var starters=new Category{Name="Starters"};var mains=new Category{Name="Main course"};var breads=new Category{Name="Breads & sides"};var drinks=new Category{Name="Beverages"};var desserts=new Category{Name="Desserts"};db.Categories.AddRange(starters,mains,breads,drinks,desserts);await db.SaveChangesAsync();
 db.MenuItems.AddRange(new MenuItem{Name="Paneer tikka",Description="Smoky tandoor · mint chutney",Price=280,CategoryId=starters.Id},new MenuItem{Name="Crispy corn",Description="Golden corn · pepper & lime",Price=180,CategoryId=starters.Id},new MenuItem{Name="Butter chicken",Description="Tandoori chicken · creamy tomato",Price=360,CategoryId=mains.Id,Vegetarian=false},new MenuItem{Name="Dal makhani",Description="Slow-cooked black lentils",Price=260,CategoryId=mains.Id},new MenuItem{Name="Veg dum biryani",Description="Fragrant basmati · garden vegetables",Price=290,CategoryId=mains.Id},new MenuItem{Name="Garlic naan",Description="Tandoor baked · garlic butter",Price=70,CategoryId=breads.Id},new MenuItem{Name="Jeera rice",Description="Basmati rice · roasted cumin",Price=150,CategoryId=breads.Id},new MenuItem{Name="Fresh lime soda",Description="Fresh lime · chilled soda",Price=90,CategoryId=drinks.Id},new MenuItem{Name="Mango lassi",Description="Alphonso mango · creamy yogurt",Price=140,CategoryId=drinks.Id},new MenuItem{Name="Gulab jamun",Description="Two warm dumplings · rose syrup",Price=120,CategoryId=desserts.Id});
 for(var i=1;i<=12;i++)db.Tables.Add(new(){Name=$"Table {i:00}",Seats=i>8?6:4});db.PaymentMethods.AddRange(new PaymentMethod{Name="Cash"},new PaymentMethod{Name="Card"},new PaymentMethod{Name="UPI"});
 } await db.SaveChangesAsync();
 foreach(var user in await db.Users.ToListAsync()){if(!await db.StaffBranches.AnyAsync(m=>m.UserId==user.Id&&m.BranchId==1))db.StaffBranches.Add(new(){UserId=user.Id,BranchId=1,Role=user.Role});}
 if(!await db.Users.AnyAsync(u=>u.Owner)){var owner=await db.Users.FirstOrDefaultAsync(u=>u.Role=="Admin"&&u.Active);if(owner!=null)owner.Owner=true;}
 await db.SaveChangesAsync();
 await db.Database.ExecuteSqlRawAsync("""
    ALTER TABLE "Bills" ADD COLUMN IF NOT EXISTS "InvoiceNumber" text NOT NULL DEFAULT '';
    ALTER TABLE "Settings" ADD COLUMN IF NOT EXISTS "RoundingRule" text NOT NULL DEFAULT 'None';
    CREATE TABLE IF NOT EXISTS "Refunds" (
        "Id" uuid NOT NULL PRIMARY KEY,
        "BranchId" integer NOT NULL DEFAULT 1,
        "SessionId" uuid NOT NULL,
        "CashierId" uuid NOT NULL,
        "Amount" numeric(12,2) NOT NULL,
        "Reason" text NOT NULL,
        "PaymentMethodId" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "CK_Refund_Positive" CHECK ("Amount" > 0),
        CONSTRAINT "FK_Refunds_Sessions" FOREIGN KEY ("SessionId") REFERENCES "Sessions" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Refunds_PaymentMethods" FOREIGN KEY ("PaymentMethodId") REFERENCES "PaymentMethods" ("Id") ON DELETE RESTRICT
    );
    CREATE INDEX IF NOT EXISTS "IX_Bills_Branch_Invoice" ON "Bills" ("BranchId", "InvoiceNumber");
  """);
 }
}
