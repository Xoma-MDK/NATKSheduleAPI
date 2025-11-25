
namespace NATKScheduleAPI;

public class Program
{
    public static void Main()
    {
        var builder = WebApplication.CreateBuilder();


        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();

        app.Run();
    }
}
