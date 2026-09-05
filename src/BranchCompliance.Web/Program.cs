var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
var app = builder.Build();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Text("Healthy"));
app.MapGet("/", () => Results.Text("Branch Compliance & Rating — independent portfolio application"));
app.Run();

public partial class Program;
