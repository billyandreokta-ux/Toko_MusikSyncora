
using TokoMusik.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// MVC dan Razor Views
builder.Services.AddControllersWithViews();

// Swagger untuk pengujian REST API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrasi koneksi database
builder.Services.AddSingleton<Koneksi>();

// Authorization akan digunakan untuk pembatasan role
builder.Services.AddAuthorization();

var app = builder.Build();

// Konfigurasi environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// REST API
app.MapControllers();

// Routing halaman MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
