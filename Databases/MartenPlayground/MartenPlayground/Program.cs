using JasperFx;
using Marten;
using MartenPlayground.Users.Domain;

namespace MartenPlayground;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        //Important - User has to create its own database manually in postgres. Marten doesn't not support auto creation of database if not exists
        builder
            .Services.AddMarten(opts =>
            {
                opts.Connection(builder.Configuration.GetConnectionString("Database")!);
                opts.AutoCreateSchemaObjects = AutoCreate.All;

                //Sample Indexing for JSONB Email
                opts.Schema.For<User>().Duplicate(x => x.Email);
            })
            .UseLightweightSessions();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
