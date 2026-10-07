using BankingAgent.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BankingAgent.Api.Data;

/// <summary>
/// DesignTimeDbContextFactory is a helper class that only exists for EF Core tooling.
///
/// WHY IS THIS NEEDED?
/// ───────────────────
/// When you run "dotnet ef migrations add ...", the EF Core tool needs to create
/// an AppDbContext instance to figure out what the database schema looks like.
///
/// Normally EF Core would try to use the running application (Program.cs) to get
/// the context. But our Program.cs reads the connection string from appsettings.json
/// which is fine, but the migration tool sometimes can't find the services properly.
///
/// This factory class tells EF Core exactly how to create an AppDbContext
/// just for migration purposes. It won't affect your running application at all.
///
/// This class is only used by "dotnet ef migrations add" and "dotnet ef database update".
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Hardcode the connection string just for migration tooling.
        // This is the same string as in appsettings.json.
        // It's safe here because this class is only used by the CLI tools, not the app.
        optionsBuilder.UseSqlServer(
            "Server=DESKTOP-HCE5B2A\\SQLEXPRESS;Database=BankingAgentDb;Trusted_Connection=True;TrustServerCertificate=True");

        return new AppDbContext(optionsBuilder.Options);
    }
}
