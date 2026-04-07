namespace Walos.Domain.Entities;

public class FinancialSummary
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal SystemSalesTotal { get; set; }
    public decimal TotalBusinessIncome { get; set; }
    public decimal NetBalance { get; set; }
    public string? TopCategoryName { get; set; }
    public decimal TopCategoryAmount { get; set; }
}
