using Walos.Domain.Entities;

namespace Walos.Domain.Interfaces;

public interface ICheckoutRepository
{
    Task<CheckoutResult> ProcessAsync(CheckoutCommand command);
    Task AddItemsAsync(AddOrderItemsCommand command);
    Task UpdateItemQuantityAsync(UpdateOrderItemQuantityCommand command);
}
