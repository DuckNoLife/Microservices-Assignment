using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SETUP ---
// Nạp file cấu hình ocelot.json
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Đăng ký dịch vụ Ocelot
builder.Services.AddOcelot(builder.Configuration);

// (Tùy chọn) Add CORS nếu muốn Frontend gọi thoải mái
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// --- 2. PIPELINE ---
app.UseCors("AllowAll");

// Tắt HTTPS Redirection để chạy Docker nội bộ cho dễ
// app.UseHttpsRedirection(); 

// QUAN TRỌNG: Kích hoạt Ocelot Middleware
await app.UseOcelot();

app.Run();