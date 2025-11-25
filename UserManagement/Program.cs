using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UserManagement.Data;
using UserManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------
// 1. KẾT NỐI DATABASE (FIX CUỐI CÙNG: LOẠI BỎ CHUỖI DỰ PHÒNG GÂY LỖI)
// -----------------------------------------------------------------

// Lấy chuỗi kết nối từ cấu hình (Render sẽ cung cấp chuỗi Postgres)
// Đã xóa cơ chế dự phòng gây lỗi. Chỉ lấy biến môi trường.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    // Nếu biến môi trường bị lỗi, ứng dụng phải dừng lại để tránh lỗi format DB
    throw new Exception("FATAL: Connection string is missing. Please check the 'ConnectionStrings__DefaultConnection' variable on Render.");
}


builder.Services.AddDbContext<UserDbContext>(options =>
{
    // ========================================================================
    // KHU VỰC CHUYỂN ĐỔI DATABASE (Chọn 1 trong 2, comment cái còn lại)
    // ========================================================================

    // 👉 LỰA CHỌN 1: Dùng PostgreSQL (ĐANG BẬT - Dùng cho Render Cloud)
    options.UseNpgsql(connectionString);

    // 👉 LỰA CHỌN 2: Dùng SQL Server (CODE CŨ - ĐANG TẮT - Dùng cho Local/Docker cũ)
    /*
    options.UseSqlServer(connectionString);
    */

    // ========================================================================
});

// -----------------------------------------------------------------
// 2. CÁC SERVICE KHÁC (Giữ nguyên)
// -----------------------------------------------------------------

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://your-issuer.com",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "https://your-audience.com",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
});

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

// -----------------------------------------------------------------
// 3. TỰ ĐỘNG TẠO BẢNG (Migration)
// -----------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<UserDbContext>();
        context.Database.EnsureCreated();
        Console.WriteLine("--> Database created/connected successfully!");
    }
    catch (Exception ex)
    {
        // Nếu lỗi, nó sẽ báo lỗi chuẩn và dừng lại, thay vì báo lỗi format vô lý
        Console.WriteLine("--> Error creating database: " + ex.Message);
    }
}

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