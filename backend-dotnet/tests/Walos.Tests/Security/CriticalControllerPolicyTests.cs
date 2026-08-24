using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Controllers;
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
    [InlineData(typeof(SalesController), "InvoiceTable", WalosPolicies.SalesOperator)]
    [InlineData(typeof(SuppliersController), "Create", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(PurchaseOrdersController), "Create", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(PurchaseOrdersController), "Receive", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(PurchaseOrdersController), "Cancel", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "CreateProduct", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "UpdateProduct", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "UploadProductImage", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "DeleteProduct", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "ImportProducts", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "AddStock", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "ProcessAIInput", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "ConfirmAIAction", WalosPolicies.InventoryWrite)]
    [InlineData(typeof(InventoryController), "GetProfitsReport", WalosPolicies.Finance)]
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

    [Theory]
    [InlineData(typeof(UsersController), WalosPolicies.Users)]
    [InlineData(typeof(CashRegisterController), WalosPolicies.CashOperator)]
    [InlineData(typeof(CreditController), WalosPolicies.CashOperator)]
    [InlineData(typeof(RefundController), WalosPolicies.CashOperator)]
    [InlineData(typeof(DeliveryController), WalosPolicies.DeliveryOperator)]
    [InlineData(typeof(AiController), WalosPolicies.InventoryWrite)]
    [InlineData(typeof(AdminController), WalosPolicies.PlatformAdmin)]
    [InlineData(typeof(PlatformAdminController), WalosPolicies.PlatformAdmin)]
    [InlineData(typeof(FinanceController), WalosPolicies.Finance)]
    [InlineData(typeof(SalesController), WalosPolicies.SalesOperator)]
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
