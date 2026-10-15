using LinkShortener.Api.Data;
using LinkShortener.Api.Endpoints;
using LinkShortener.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
	?? "Data Source=linkshortener.db";
builder.Services.AddDbContext<LinkDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ILinkRepository, LinkRepository>();
builder.Services.AddSingleton<IDnsResolver, SystemDnsResolver>();
builder.Services.AddScoped<TargetUrlValidator>();
builder.Services.AddSingleton<Func<DateTime>>(() => DateTime.UtcNow);
builder.Services.AddSingleton<Func<string>>(CodeGenerator.GenerateCode);
builder.Services.AddScoped<ILinkService, LinkService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
	await dbContext.Database.MigrateAsync();
}

app.MapLinkEndpoints();

app.Run();

public partial class Program;