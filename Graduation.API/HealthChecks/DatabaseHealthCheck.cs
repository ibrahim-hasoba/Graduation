using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Graduation.DAL.Data;

namespace Graduation.API.HealthChecks
{
  public class DatabaseHealthCheck : IHealthCheck
  {
    private readonly DatabaseContext _db;

    public DatabaseHealthCheck(DatabaseContext db)
    {
      _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
      try
      {
        var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
        return canConnect ? HealthCheckResult.Healthy("Database reachable") : HealthCheckResult.Unhealthy("Database unreachable");
      }
      catch (System.Exception ex)
      {
        return HealthCheckResult.Unhealthy(ex.Message);
      }
    }
  }
}
