using Microsoft.Extensions.Logging;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public partial class FinanceRepository : IFinanceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FinanceRepository> _logger;

    public FinanceRepository(IDbConnectionFactory connectionFactory, ILogger<FinanceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }
}
