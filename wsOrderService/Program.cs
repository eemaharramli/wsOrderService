using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using wsOrderService.Data;
using wsOrderService.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSingleton<RabbitMQProducer>();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var consumer = new RabbitMQConsumer("delivery_status_updated");
_ = Task.Run(async () =>
{
    await consumer.StartListeningAsync(async message =>
    {
        Console.WriteLine($"[x] Processing message: {message}");

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var data = message.Split(",");
        var orderId = int.Parse(data[0].Split(":")[1]);
        var status = data[1].Split(":")[1];

        var order = await dbContext.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Status = status;
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[x] Order {orderId} status updated to {status}");
        }
        else
        {
            Console.WriteLine($"[!] Order {orderId} not found.");
        }
    });
});

app.Run();
