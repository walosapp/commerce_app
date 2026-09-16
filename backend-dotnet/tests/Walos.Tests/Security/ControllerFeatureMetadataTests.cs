using Microsoft.AspNetCore.Authorization;
using Walos.API.Authorization;
using Walos.API.Controllers;
using Walos.Application.Security;
using Walos.Domain.Features;

namespace Walos.Tests.Security;

public class ControllerFeatureMetadataTests
{
    [Theory]
    [InlineData(typeof(CashRegisterController), WalosFeatures.Cash)]
    [InlineData(typeof(PosDeliController), WalosFeatures.Pos)]
    [InlineData(typeof(PurchaseOrdersController), WalosFeatures.Purchases)]
    [InlineData(typeof(DeliveryController), WalosFeatures.Delivery)]
    public void Module_Controller_Requires_Expected_Feature(Type controller, string feature)
    {
        var metadata = controller.GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single();

        Assert.Equal(feature, metadata.Feature);
    }

    [Fact]
    public void Dashboard_Finance_Summary_Is_Not_Feature_Gated()
    {
        Assert.Empty(typeof(FinanceController).GetMethod(nameof(FinanceController.GetSummary))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true));
        Assert.Equal(WalosFeatures.Finance, typeof(FinanceController)
            .GetMethod(nameof(FinanceController.GetEntries))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single().Feature);
        Assert.Equal(WalosPolicies.Dashboard, typeof(FinanceController)
            .GetMethod(nameof(FinanceController.GetSummary))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single(attribute => attribute.Policy is not null).Policy);
        Assert.Equal(WalosPolicies.Finance, typeof(FinanceController)
            .GetMethod(nameof(FinanceController.GetEntries))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single(attribute => attribute.Policy is not null).Policy);
    }

    [Fact]
    public void Credits_Require_Restaurant_Or_Pos()
    {
        var requirement = typeof(CreditController)
            .GetCustomAttributes(typeof(RequireAnyFeatureAttribute), true)
            .Cast<RequireAnyFeatureAttribute>().Single();

        Assert.Equal([WalosFeatures.Restaurant, WalosFeatures.Pos], requirement.Features);
    }

    [Fact]
    public void Tenant_Ai_Settings_Require_Ai_Feature_Without_Gating_Platform_Admin_Controller()
    {
        foreach (var methodName in new[] { nameof(PlatformController.GetAiUsage), nameof(PlatformController.UpdateAiKey) })
        {
            var feature = typeof(PlatformController).GetMethod(methodName)!
                .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
                .Cast<RequireFeatureAttribute>().Single();
            Assert.Equal(WalosFeatures.Ai, feature.Feature);
        }

        Assert.Empty(typeof(PlatformAdminController)
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true));
    }

    [Fact]
    public void Supplier_Reads_Support_Purchases_But_Supplier_Mutations_Require_Suppliers()
    {
        var shared = typeof(SuppliersController)
            .GetCustomAttributes(typeof(RequireAnyFeatureAttribute), true)
            .Cast<RequireAnyFeatureAttribute>().Single();
        Assert.Equal([WalosFeatures.Suppliers, WalosFeatures.Purchases], shared.Features);

        Assert.Empty(typeof(SuppliersController).GetMethod(nameof(SuppliersController.GetAll))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true));
        Assert.Equal(WalosFeatures.Suppliers, typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.Create))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single().Feature);
    }

    [Fact]
    public void Shared_Product_Reads_Support_All_Dependent_Modules_But_Writes_Require_Inventory()
    {
        var shared = typeof(InventoryController)
            .GetCustomAttributes(typeof(RequireAnyFeatureAttribute), true)
            .Cast<RequireAnyFeatureAttribute>().Single();
        Assert.Equal(
            [WalosFeatures.Inventory, WalosFeatures.Restaurant, WalosFeatures.Pos,
                WalosFeatures.Purchases, WalosFeatures.Suppliers, WalosFeatures.Delivery],
            shared.Features);

        Assert.Empty(typeof(InventoryController).GetMethod(nameof(InventoryController.GetProducts))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true));
        Assert.Equal(WalosFeatures.Inventory, typeof(InventoryController)
            .GetMethod(nameof(InventoryController.CreateProduct))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single().Feature);
    }

    [Fact]
    public void Sale_Catalog_Requires_An_Operational_Sales_Feature()
    {
        var requirement = typeof(InventoryController)
            .GetMethod(nameof(InventoryController.GetSaleCatalog))!
            .GetCustomAttributes(typeof(RequireAnyFeatureAttribute), true)
            .Cast<RequireAnyFeatureAttribute>()
            .Single();

        Assert.Equal(
            [WalosFeatures.Restaurant, WalosFeatures.Pos, WalosFeatures.Delivery],
            requirement.Features);
    }

    [Fact]
    public void Sales_Shared_Reads_Allow_Restaurant_Or_Pos_But_Table_And_Kitchen_Are_Restaurant_Only()
    {
        var shared = typeof(SalesController)
            .GetCustomAttributes(typeof(RequireAnyFeatureAttribute), true)
            .Cast<RequireAnyFeatureAttribute>().Single();
        Assert.Equal([WalosFeatures.Restaurant, WalosFeatures.Pos], shared.Features);

        Assert.Empty(typeof(SalesController).GetMethod(nameof(SalesController.GetReceipt))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true));
        Assert.Equal(WalosFeatures.Restaurant, typeof(SalesController)
            .GetMethod(nameof(SalesController.GetTables))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single().Feature);
        Assert.Equal(WalosFeatures.Restaurant, typeof(SalesController)
            .GetMethod(nameof(SalesController.GetKitchenTicket))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single().Feature);
        Assert.Equal(WalosFeatures.Restaurant, typeof(SalesController)
            .GetMethod(nameof(SalesController.GetActiveRestaurantKitchenTicket))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>().Single().Feature);
    }

    [Fact]
    public void General_Ai_Requires_Ai_While_Inventory_Ai_Requires_Both_Features()
    {
        var features = typeof(AiController)
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>()
            .Select(a => a.Feature)
            .ToArray();

        Assert.Equal([WalosFeatures.Ai], features);

        var inventoryAi = typeof(InventoryController)
            .GetMethod(nameof(InventoryController.ProcessAIInput))!
            .GetCustomAttributes(typeof(RequireFeatureAttribute), true)
            .Cast<RequireFeatureAttribute>()
            .Select(a => a.Feature)
            .ToArray();
        Assert.Equal([WalosFeatures.Inventory, WalosFeatures.Ai], inventoryAi);
    }

    [Fact]
    public void Controllers_No_Longer_Trust_Raw_Dev_Role()
    {
        var controllerTypes = new[]
        {
            typeof(CatalogController), typeof(InventoryController), typeof(PosDeliController)
        };

        var attributes = controllerTypes
            .SelectMany(type => type.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Concat(type.GetMethods().SelectMany(m =>
                    m.GetCustomAttributes(typeof(AuthorizeAttribute), true))))
            .Cast<AuthorizeAttribute>();

        Assert.DoesNotContain(attributes, a =>
            a.Roles?.Split(',').Contains(WalosRoles.Dev, StringComparer.OrdinalIgnoreCase) == true);
    }
}
