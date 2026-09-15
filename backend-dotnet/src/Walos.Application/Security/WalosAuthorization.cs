namespace Walos.Application.Security;

public static class WalosClaimTypes
{
    public const string UserId = "userId";
    public const string CompanyId = "companyId";
    public const string BranchId = "branchId";
    public const string PlatformAdmin = "platformAdmin";
}

public static class WalosRoles
{
    public const string Dev = "dev";
    public const string PlatformAdmin = "platform_admin";
    public const string SuperAdmin = "super_admin";
    public const string Manager = "manager";
    public const string Cashier = "cashier";
    public const string Waiter = "waiter";
}

public static class WalosPolicies
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string CanonicalAuthenticated = "CanonicalAuthenticated";
    public const string Dashboard = "Dashboard";
    public const string TenantManager = "TenantManager";
    public const string Settings = "Settings";
    public const string Users = "Users";
    public const string Finance = "Finance";
    public const string PurchasesRead = "PurchasesRead";
    public const string SuppliersRead = "SuppliersRead";
    public const string InventoryWrite = "InventoryWrite";
    public const string SalesOperator = "SalesOperator";
    public const string CashOperator = "CashOperator";
    public const string DeliveryOperator = "DeliveryOperator";
    public const string DeliveryManage = "DeliveryManage";
    public const string CatalogWrite = "CatalogWrite";
    public const string CatalogDelete = "CatalogDelete";
    public const string PosDeliOperator = "PosDeliOperator";
}

public static class WalosSystemIdentity
{
    public const string CompanyTaxId = "WALOS-SYSTEM-001";
}
