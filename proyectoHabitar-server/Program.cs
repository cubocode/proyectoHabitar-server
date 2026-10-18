using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using proyectoHabitar_server.Datos;
using proyectoHabitar_server.Servicios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opciones =>
    {
        // Los errores de validación vuelven como { mensaje } igual que el resto de los errores,
        // así la app de escritorio los muestra tal cual.
        opciones.InvalidModelStateResponseFactory = contexto =>
        {
            var errores = contexto.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Formato de dato inválido." : e.ErrorMessage)
                .Distinct();
            return new BadRequestObjectResult(new { mensaje = "Revisá los datos:\n• " + string.Join("\n• ", errores) });
        };
    });

builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<AlgoritmoPriorizacion>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddHttpClient<GeocodificacionService>();

string claveJwt = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key en appsettings.json");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveJwt)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HabitAR API", Version = "v1" });

    // Botón "Authorize" en Swagger: pegá el token que devuelve POST /api/Auth/login.
    var esquema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá solo el token (sin la palabra Bearer).",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", esquema);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { esquema, Array.Empty<string>() } });
});

var app = builder.Build();

// Cualquier excepción no controlada vuelve como 500 con { mensaje } en lugar de una página de error.
app.UseExceptionHandler(manejador => manejador.Run(async contexto =>
{
    var error = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(error, "Error no controlado");
    contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await contexto.Response.WriteAsJsonAsync(new { mensaje = $"Error interno del servidor: {error?.Message}" });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
