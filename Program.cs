using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using cafApi.Contexts;
using cafApi.Services;
using cafApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configurar WebRootPath se não estiver definido


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        Environment.GetEnvironmentVariable("CONNECTION_STRING") 
        ?? builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 36))
    )
);

builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<ICorService, CorService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IProdutoUploadService, ProdutoUploadService>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<ITransacaoService, TransacaoService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPagarmeService, PagarmeService>();
builder.Services.AddScoped<IGoogleAnalyticsService, GoogleAnalyticsService>();
builder.Services.AddScoped<ICorreiosService, CorreiosService>();
builder.Services.AddScoped<IRotuloAutomaticoService, RotuloAutomaticoService>();
builder.Services.AddSingleton<IActiveClientsTracker, ActiveClientsTracker>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddHttpClient(); // Necessário para PagarmeService

// 🔐 Configuração JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") 
                     ?? builder.Configuration["Jwt:Key"]!;
                     
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    var devUrls = Environment.GetEnvironmentVariable("FRONTEND_DEVELOPMENT_URLS") 
                  ?? builder.Configuration["Frontend:DevelopmentUrls"] 
                  ?? "http://localhost:3000,http://localhost:3001";
                  
    var prodUrl = Environment.GetEnvironmentVariable("FRONTEND_PRODUCTION_URL") 
                  ?? builder.Configuration["Frontend:ProductionUrl"] 
                  ?? "https://chaseaflare.com.br";

    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins(devUrls.Split(','))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(prodUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Chase a Flare API", Version = "v1" });
    
    // 🔐 Configuração para JWT Bearer
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
    });
    
    c.AddSecurityDefinition("Cookie", new()
    {
        Name = "Cookie",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Authentication via cookie. Login first to get the auth_token cookie."
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Cookie"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (string.IsNullOrEmpty(builder.Environment.WebRootPath))
{
    var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    if (!Directory.Exists(wwwrootPath))
    {
        Directory.CreateDirectory(wwwrootPath);
    }
    builder.Environment.WebRootPath = wwwrootPath;
}

// 🔹 Pipeline padrão
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em desenvolvimento, mantenha HTTP para evitar problemas de cookie Secure e mismatches de esquema
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Configurar CORS baseado no ambiente
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

// 🍪 Middleware para ler token de cookie (DEVE vir ANTES da autenticação)
app.UseMiddleware<CookieAuthenticationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// 🛡️ Middleware para tratar acesso negado de forma segura
app.UseMiddleware<AccessDeniedMiddleware>();

// 🔹 Aplicar migrations automaticamente
if (!app.Environment.IsEnvironment("DesignTime"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            await context.Database.MigrateAsync();
            Console.WriteLine("✓ Migrations aplicadas com sucesso");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao aplicar migrations: {ex.Message}");
        }

        var seedService = scope.ServiceProvider.GetRequiredService<SeedService>();
        await seedService.SeedAsync();
    }
}

app.MapControllers();

app.Run();
