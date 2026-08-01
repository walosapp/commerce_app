using Walos.Application.DTOs.Suppliers;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Application.Services;

namespace Walos.Application.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;

    public PurchaseOrderService(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<PurchaseOrderResponse>> GetAllAsync(long companyId, long? supplierId = null)
        => _repository.GetAllAsync(companyId, supplierId);

    public Task<PurchaseOrderResponse?> GetByIdAsync(long id, long companyId)
        => _repository.GetByIdAsync(id, companyId);

    public async Task<PurchaseOrderResponse> CreateAsync(long companyId, long userId, CreatePurchaseOrderRequest request)
    {
        if (request.Items.Count == 0)
            throw new ValidationException("El pedido debe tener al menos un producto");

        return await _repository.CreateAsync(companyId, userId, request);
    }

    public Task<PurchaseOrderResponse> ReceiveAsync(long id, long companyId, long branchId, long userId, ReceivePurchaseOrderRequest request)
        => _repository.ReceiveAsync(id, companyId, branchId, userId, request);

    public Task<bool> CancelAsync(long id, long companyId)
        => _repository.CancelAsync(id, companyId);
}

