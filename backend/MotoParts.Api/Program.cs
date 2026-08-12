using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using MotoParts.Api.Data;
using MotoParts.Api.Services;

using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<OAuthService>();
builder.Services.AddScoped<YooKassaService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        };
    });
builder.Services.AddAuthorization();

// ng serve по умолчанию слушает только localhost, поэтому localhost:4200 разрешён
// наравне с сетевым адресом из Frontend:BaseUrl — иначе локальная разработка упирается в CORS.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(
        builder.Configuration["Frontend:BaseUrl"] ?? "http://192.168.88.122:4200",
        "http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Применяем миграции при старте (Code First)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.GetMigrations().Any())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated(); // fallback, пока миграции не сгенерированы

    await DbSeeder.SeedAsync(db, app.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
// Фото запчастей лежат в wwwroot/ZipPhotos и отдаются этим же вызовом
// (wwwroot — стандартный web root, поэтому файл доступен по /ZipPhotos/...).
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
