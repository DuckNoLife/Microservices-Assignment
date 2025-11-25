// File: UserManagement/Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UserManagement.Data;
using UserManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. KẾT NỐI DATABASE (Có dự phòng lỗi null)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=(localdb)\\ProjectModels;Database=UserManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<UserDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 2. ĐĂNG KÝ SERVICES
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 3. CẤU HÌNH SWAGGER (Để test Admin tiện hơn)
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập token Admin vào đây"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// 4. CẤU HÌNH JWT (FIX LỖI 401 Ở ĐÂY)
// Key dự phòng khớp với appsettings.json của bạn
var jwtKey = builder.Configuration["Jwt:Key"] ?? "1234567890qwertyuiopgsdgsdgsdgsdgsdgsdgsdgdsgsdgsdgsdgdsgsdgsdgdsgsdrewwetwetewtwetewtewtwetwetwetewweewrwererwerwerewrwerwerwerwe";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://your-issuer.com", // Dự phòng
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "https://your-audience.com", // Dự phòng
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role // Quan trọng để nhận diện Admin
    };
});

// 5. CORS (Cho phép Frontend gọi vào)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// --- THÊM ĐOẠN NÀY ĐỂ TỰ TẠO DATABASE KHI CHẠY DOCKER ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<UserDbContext>();
        // Tự động tạo Database nếu chưa có (thay thế cho Update-Database)
        context.Database.EnsureCreated();
        Console.WriteLine("--> Database created successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine("--> Error creating database: " + ex.Message);
    }
}
//

// MIDDLEWARE
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();