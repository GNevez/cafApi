using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using cafApi.Contexts;
using cafApi.Services;
using cafApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        Environment.GetEnvironmentVariable("CONNECTION_STRING") 
        ?? builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 36))
    )
);

builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<SeedService>();

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
    var devUrl = Environment.GetEnvironmentVariable("FRONTEND_DEVELOPMENT_URL") 
                 ?? builder.Configuration["Frontend:DevelopmentUrl"] 
                 ?? "http://localhost:3000";
                 
    var prodUrl = Environment.GetEnvironmentVariable("FRONTEND_PRODUCTION_URL") 
                  ?? builder.Configuration["Frontend:ProductionUrl"] 
                  ?? "https://chaseaflare.com.br";

    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins(devUrl)
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

// 🔹 Pipeline padrão
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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

// 🔹 Executar seed de dados iniciais
using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seedService.SeedAsync();
}

app.MapControllers();

app.Run();
