namespace Walos.Application.DTOs.Sales;

public class UpdateItemQuantityRequest
{
    public decimal Quantity { get; set; }
    public long OrderId { get; set; }
}
