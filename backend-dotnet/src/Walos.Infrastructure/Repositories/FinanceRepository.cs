using Microsoft.Extensions.Logging;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public partial class FinanceRepository : IFinanceRepository
{
    // Invariantes de aislamiento multi-tenant usadas en los queries parciales:
    // INNER JOIN finance.categories c ON c.id = e.category_id AND c.company_id = e.company_id
    // LEFT JOIN core.branches b ON b.id = e.branch_id AND b.company_id = e.company_id

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<FinanceRepository> _logger;

    public FinanceRepository(IDbConnectionFactory connectionFactory, ILogger<FinanceRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }
}
