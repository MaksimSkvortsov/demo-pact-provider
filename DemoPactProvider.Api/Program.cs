var app = Program.BuildApp(args);

app.Run();

public partial class Program
{
    public static WebApplication BuildApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });

        builder.Services.AddControllers();

        var app = builder.Build();

        app.MapControllers();

        return app;
    }
}
