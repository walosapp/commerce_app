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

    public Task<IEnumerable<PurchaseOrderResponse>> GetAllAsync(long companyId, long? branchId, long? supplierId = null)
        => _repository.GetAllAsync(companyId, branchId, supplierId);

    public Task<PurchaseOrderResponse?> GetByIdAsync(long id, long companyId, long? branchId)
        => _repository.GetByIdAsync(id, companyId, branchId);

    public async Task<PurchaseOrderResponse> CreateAsync(long companyId, long branchId, long userId, CreatePurchaseOrderRequest request)
    {
        if (request.Items is not { Count: > 0 })
            throw new ValidationException("El pedido debe tener al menos un producto");

        if (branchId <= 0)
            throw new ValidationException("La sucursal es obligatoria");

        if (request.Items.Any(item => item.Quantity <= 0))
            throw new ValidationException("La cantidad de cada producto debe ser mayor a cero");

        if (request.Items.Any(item => item.UnitCost < 0))
            throw new ValidationException("El costo unitario no puede ser negativo");

        return await _repository.CreateAsync(companyId, branchId, userId, request);
    }

    public Task<PurchaseOrderResponse> ReceiveAsync(long id, long companyId, long branchId, long userId, ReceivePurchaseOrderRequest request)
        => _repository.ReceiveAsync(id, companyId, branchId, userId, request);

    public Task<bool> CancelAsync(long id, long companyId, long? branchId)
        => _repository.CancelAsync(id, companyId, branchId);
}

