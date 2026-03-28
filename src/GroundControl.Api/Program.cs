var builder = WebApplication.CreateBuilder(args);
var app = builder.BuildWebApiModules();
app.Run();