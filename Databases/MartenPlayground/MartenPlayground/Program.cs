using JasperFx;
using Marten;
using MartenPlayground.Users.Domain;
using MartenPlayground.Users.Events;

namespace MartenPlayground;

public partial class Program
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

                //Create Database if not exists - Not to be used in Production generally
                MartenCreateDatabaseIfNotExists(opts, builder);

                //Sample Indexing for JSONB Email
                opts.Schema.For<User>().Duplicate(x => x.Email);

                //View Projections - https://martendb.io/events/projections/
                opts.Projections.Add<UserDashboardViewProjection>(
                    JasperFx.Events.Projections.ProjectionLifecycle.Inline
                );
            })
            .ApplyAllDatabaseChangesOnStartup()
            .UseLightweightSessions();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        //if (app.Environment.IsDevelopment())
        //{
        app.UseSwagger();
        app.UseSwaggerUI();
        //}

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }

    private static void MartenCreateDatabaseIfNotExists(
        StoreOptions opts,
        WebApplicationBuilder builder
    )
    {
        opts.CreateDatabasesForTenants(c =>
        {
            c.MaintenanceDatabase(builder.Configuration.GetConnectionString("DatabaseMigrator"));

            c.ForTenant().CheckAgainstPgDatabase().WithOwner("postgres").WithEncoding("UTF8");
        });
    }
}

//This class is intentionally left blank to ensure that WebApplicationFactory<Program> works like an Internal class in Integration tests (MartenPlaygorund.Tests.Docker)
public partial class Program { }
