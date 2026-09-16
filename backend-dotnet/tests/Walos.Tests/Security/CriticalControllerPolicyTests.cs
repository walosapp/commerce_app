using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Controllers;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Security;

namespace Walos.Tests.Security;

public class CriticalControllerPolicyTests
{
    [Fact]
    public void Inventory_Write_Actions_DoNotFallBack_To_GenericAuthorization()
    {
        var writeActions = typeof(InventoryController).GetMethods()
            .Where(method => method.GetCustomAttributes(inherit: true).Any(attribute =>
                attribute is HttpPostAttribute
                    or HttpPutAttribute
                    or HttpPatchAttribute
                    or HttpDeleteAttribute));

        foreach (var action in writeActions)
        {
            var authorize = action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .SingleOrDefault();

            Assert.NotNull(authorize);
            Assert.Equal(WalosPolicies.InventoryWrite, authorize!.Policy);
        }
    }

    [Theory]
    [InlineData(typeof(CompanyController), "UpdateSettings", WalosPolicies.Settings)]
    [InlineData(typeof(CompanyController), "UpdateOperationsSettings", WalosPolicies.Settings)]
    [InlineData(typeof(PlatformController), "UpdateAiKey", WalosPolicies.Settings)]
    [InlineData(typeof(AuthController), "Logout", WalosPolicies.CanonicalAuthenticated)]
    [InlineData(typeof(SalesController), "InvoiceTable", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "CancelTable", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "GetOrderItems", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "GetSummary", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "GetCompletedOrders", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "GetReceipt", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "GetKitchenTicket", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "SearchOrders", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "GetOrderDetail", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SalesController), "ExportOrders", WalosPolicies.SalesInvoiceOperator)]
    [InlineData(typeof(SuppliersController), "Create", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(SuppliersController), "GetAll", WalosPolicies.SuppliersRead)]
    [InlineData(typeof(SuppliersController), "GetById", WalosPolicies.SuppliersRead)]
    [InlineData(typeof(SuppliersController), "GetSuggestedOrder", WalosPolicies.SuppliersRead)]
    [InlineData(typeof(PurchaseOrdersController), "GetAll", WalosPolicies.PurchasesRead)]
    [InlineData(typeof(PurchaseOrdersController), "GetById", WalosPolicies.PurchasesRead)]
    [InlineData(typeof(PurchaseOrdersController), "Create", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(PurchaseOrdersController), "Receive", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(PurchaseOrdersController), "Cancel", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "CreateProduct", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "UpdateProduct", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "UploadProductImage", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "DeleteProduct", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "ImportProducts", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "AddStock", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "GetProducts", WalosPolicies.InventoryRead)]
    [InlineData(typeof(InventoryController), "GetProductById", WalosPolicies.InventoryRead)]
    [InlineData(typeof(InventoryController), "GetStock", WalosPolicies.InventoryRead)]
    [InlineData(typeof(InventoryController), "GetLowStock", WalosPolicies.InventoryRead)]
    [InlineData(typeof(InventoryController), "GetAlerts", WalosPolicies.InventoryRead)]
    [InlineData(typeof(InventoryController), "ProcessAIInput", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "ConfirmAIAction", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "GetProfitsReport", WalosPolicies.Finance)]
    [InlineData(typeof(FinanceController), "GetEntries", WalosPolicies.Finance)]
    [InlineData(typeof(FinanceController), "CreateEntry", WalosPolicies.Finance)]
    [InlineData(typeof(FinanceController), "GetSummary", WalosPolicies.Dashboard)]
    [InlineData(typeof(RecipesController), "UpsertIngredient", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(RecipesController), "RemoveIngredient", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(RecipesController), "ClearRecipe", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(DeliveryController), "Reject", WalosPolicies.DeliveryManage)]
    [InlineData(typeof(DeliveryController), "Cancel", WalosPolicies.DeliveryManage)]
    [InlineData(typeof(DeliveryController), "Return", WalosPolicies.DeliveryManage)]
    public void Critical_Action_Uses_Explicit_Policy(Type controller, string methodName, string expectedPolicy)
    {
        var method = controller.GetMethod(methodName);
        var authorize = method?
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
    }

    [Fact]
    public void Sale_Catalog_Uses_CatalogRead_Without_Exposing_InventoryRead()
    {
        var controllerPolicy = typeof(InventoryController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        var actionPolicies = typeof(InventoryController)
            .GetMethod(nameof(InventoryController.GetSaleCatalog))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();

        Assert.Equal(WalosPolicies.CatalogRead, controllerPolicy.Policy);
        Assert.Empty(actionPolicies);
    }

    [Fact]
    public void Sale_Catalog_Dto_Does_Not_Expose_Administrative_Inventory_Fields()
    {
        var properties = typeof(SaleCatalogProductResponse).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(SaleCatalogProductResponse.AvailableQuantity), properties);
        Assert.Contains(nameof(SaleCatalogProductResponse.SalePrice), properties);
        Assert.DoesNotContain("CostPrice", properties);
        Assert.DoesNotContain("Quantity", properties);
        Assert.DoesNotContain("ReservedQuantity", properties);
        Assert.DoesNotContain("MinStock", properties);
        Assert.DoesNotContain("Location", properties);
        Assert.DoesNotContain("StockStatus", properties);
    }

    [Theory]
    [InlineData(typeof(UsersController), WalosPolicies.Users)]
    [InlineData(typeof(CashRegisterController), WalosPolicies.CashOperator)]
    [InlineData(typeof(CreditController), WalosPolicies.CashOperator)]
    [InlineData(typeof(RefundController), WalosPolicies.CashOperator)]
    [InlineData(typeof(DeliveryController), WalosPolicies.DeliveryOperator)]
    [InlineData(typeof(AiController), WalosPolicies.InventoryWrite)]
    [InlineData(typeof(AdminController), WalosPolicies.PlatformAdmin)]
    [InlineData(typeof(PlatformAdminController), WalosPolicies.PlatformAdmin)]
    [InlineData(typeof(SalesController), WalosPolicies.SalesTableOperator)]
    [InlineData(typeof(RecipesController), WalosPolicies.Recipes)]
    [InlineData(typeof(InventoryController), WalosPolicies.CatalogRead)]
    [InlineData(typeof(CatalogController), WalosPolicies.CatalogRead)]
    public void Critical_Controller_Uses_Explicit_Policy(Type controller, string expectedPolicy)
    {
        var authorize = controller
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
    }
}
