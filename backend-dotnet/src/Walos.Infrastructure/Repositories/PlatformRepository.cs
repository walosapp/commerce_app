using Microsoft.Extensions.Logging;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public partial class PlatformRepository : IPlatformRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PlatformRepository> _logger;

    public PlatformRepository(IDbConnectionFactory connectionFactory, ILogger<PlatformRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }
}
