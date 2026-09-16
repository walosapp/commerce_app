using System.Runtime.CompilerServices;

namespace Walos.Tests.Repositories;

public class CompanyFeatureSchemaTests
{
    private static readonly string[] CanonicalFeatureCodes =
    [
        "dashboard", "inventory", "restaurant", "pos", "cash",
        "purchases", "suppliers", "delivery", "finance", "ai"
    ];

    private static string ReadRepositoryFile(
        string relativePath,
        [CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException("No se pudo resolver el directorio de tests.");
        var backendRoot = Path.GetFullPath(Path.Combine(testSourceDirectory, "..", "..", ".."));
        var path = Path.Combine(backendRoot, relativePath);

        Assert.True(File.Exists(path), $"No se encontro el archivo esperado: {path}");
        return File.ReadAllText(path);
    }

    private static string GetRepositoryPath(
        string relativePath,
        [CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException("No se pudo resolver el directorio de tests.");
        var backendRoot = Path.GetFullPath(Path.Combine(testSourceDirectory, "..", "..", ".."));
        return Path.GetFullPath(Path.Combine(backendRoot, relativePath));
    }

    [Fact]
    public void Migration_EnforcesDashboardAndBackfillsEveryExistingCompany()
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "migrations", "019_company_features.sql"));

        Assert.Contains("CHECK (code <> 'dashboard' OR (default_enabled AND is_mandatory AND is_active))", source);
        Assert.Contains("CHECK (feature_code <> 'dashboard' OR is_enabled)", source);
        Assert.Contains("CROSS JOIN platform.features f", source);
        Assert.Contains("ON CONFLICT (company_id, feature_code) DO NOTHING", source);
        Assert.Contains("('dashboard',   'Dashboard',     'Resumen operativo del comercio',                 TRUE,  TRUE,", source);
        Assert.Contains("('ai',          'Asistente IA',  'Asistente inteligente de Walos',                 FALSE, FALSE,", source);
    }

    [Theory]
    [InlineData("migrations", "019_company_features.sql")]
    [InlineData("scripts", "cleanup_data_keep_inventory.sql")]
    public void CanonicalFeatureCatalog_ContainsExactlyTheTenV1Features(string directory, string fileName)
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", directory, fileName));
        var insertStart = source.IndexOf("INSERT INTO platform.features", StringComparison.Ordinal);
        var conflictStart = source.IndexOf("ON CONFLICT (code)", insertStart, StringComparison.Ordinal);

        Assert.True(insertStart >= 0 && conflictStart > insertStart);
        var catalogInsert = source[insertStart..conflictStart];
        var codes = System.Text.RegularExpressions.Regex
            .Matches(catalogInsert, @"\('(?<code>[a-z][a-z0-9_]*)'")
            .Select(match => match.Groups["code"].Value)
            .ToArray();

        Assert.Equal(CanonicalFeatureCodes, codes);
    }

    [Fact]
    public void Migration_SeedsOnlyPlatformAdminRoleForWalosSystemCompanyWithoutCreatingUser()
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "migrations", "019_company_features.sql"));

        Assert.Contains("c.tax_id = 'WALOS-SYSTEM-001'", source);
        Assert.Contains("'platform_admin'", source);
        Assert.DoesNotContain("INSERT INTO core.users", source, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("800_seed_initial_data.sql")]
    [InlineData("900_seed_dev_user.sql")]
    public void LegacySeedMigrations_AreHarmlessCompatibilityMarkers(string seedFile)
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "migrations", seedFile));

        Assert.Contains("marcador de compatibilidad", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password_hash", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2a$", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin123", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("walos2024", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanonicalFreshSeedOrder_IsMigrationThenInitialDataThenSystemUser()
    {
        var migrationsDirectory = GetRepositoryPath(Path.Combine("..", "supabase", "migrations"));
        var ordered = Directory
            .EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var featureMigration = Array.IndexOf(ordered, "019_company_features.sql");
        var initialSeed = Array.IndexOf(ordered, "800_seed_initial_data.sql");
        var devSeed = Array.IndexOf(ordered, "900_seed_dev_user.sql");
        var compatibilityMarker = Array.IndexOf(ordered, "999_cleanup_data_keep_inventory.sql");

        Assert.True(featureMigration >= 0, "019_company_features.sql no esta en el directorio real de migraciones.");
        Assert.True(featureMigration < initialSeed, "019 debe ejecutarse antes de 800.");
        Assert.True(initialSeed < devSeed, "800 debe ejecutarse antes de 900.");
        Assert.True(devSeed < compatibilityMarker, "900 debe ejecutarse antes del marcador 999.");
    }

    [Fact]
    public void AutomaticMigrationSequence_NeverContainsDestructiveCleanupSql()
    {
        var migrationsDirectory = GetRepositoryPath(Path.Combine("..", "supabase", "migrations"));
        var migrationFiles = Directory.EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly);

        Assert.All(migrationFiles, path =>
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("TRUNCATE TABLE", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE FROM core.companies", source, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void AutomaticMigrationSequence_NeverBootstrapsFixedUsersOrPasswordHashes()
    {
        var migrationsDirectory = GetRepositoryPath(Path.Combine("..", "supabase", "migrations"));
        var migrationFiles = Directory.EnumerateFiles(migrationsDirectory, "*.sql", SearchOption.TopDirectoryOnly);

        Assert.All(migrationFiles, path =>
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("INSERT INTO core.users", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("admin123", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("walos2024", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(@"\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}", source);
        });
    }

    [Theory]
    [InlineData("bootstrap_demo_data.sql", "walos.bootstrap_admin_password_hash")]
    [InlineData("bootstrap_walos_system.sql", "walos.bootstrap_dev_password_hash")]
    public void ManualBootstrap_RequiresOperatorSuppliedBcryptHash(
        string scriptFile, string settingName)
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "scripts", scriptFile));

        Assert.Contains(settingName, source);
        Assert.Contains("RAISE EXCEPTION", source);
        Assert.DoesNotContain("admin123", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("walos2024", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}", source);
    }

    [Fact]
    public void Migration019_IsAtomicAndFailsClosedOnPartialOrIncompatibleState()
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "migrations", "019_company_features.sql"));

        Assert.Contains("BEGIN;", source);
        Assert.Contains("COMMIT;", source);
        Assert.Contains("v_features_exists <> v_company_features_exists", source);
        Assert.Contains("RAISE EXCEPTION", source);
        Assert.Contains("information_schema.columns", source);
        Assert.Contains("pg_constraint", source);
        Assert.Contains("Definicion canonica incompatible", source);

        var firstWrite = source.IndexOf("CREATE TABLE IF NOT EXISTS platform.features", StringComparison.Ordinal);
        Assert.True(firstWrite >= 0);
        Assert.All(
            new[]
            {
                "Estado parcial incompatible",
                "Columnas requeridas ausentes o incompatibles",
                "Claves primarias requeridas ausentes o incompatibles",
                "Claves foraneas requeridas ausentes o incompatibles",
                "Restricciones CHECK requeridas ausentes o incompatibles",
                "Definicion canonica incompatible"
            },
            guard => Assert.True(
                source.IndexOf(guard, StringComparison.Ordinal) < firstWrite,
                $"El guard '{guard}' debe ejecutarse antes de cualquier escritura."));
    }

    [Fact]
    public void CleanupMigration_IsOnlyANonDestructiveCompatibilityMarker()
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "migrations", "999_cleanup_data_keep_inventory.sql"));

        Assert.Contains("supabase/scripts/cleanup_data_keep_inventory.sql", source);
        Assert.Contains("NO ejecuta limpieza", source);
        Assert.DoesNotContain("TRUNCATE TABLE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManualCleanupScript_IsExplicitlyOptInAndRestoresCanonicalFeatureCatalog()
    {
        var source = ReadRepositoryFile(Path.Combine("..", "supabase", "scripts", "cleanup_data_keep_inventory.sql"));

        Assert.Contains("MANTENIMIENTO MANUAL", source);
        Assert.Contains("NO forma parte", source);
        Assert.Contains("BEGIN;", source);
        Assert.Contains("COMMIT;", source);
        Assert.Contains("walos.bootstrap_dev_password_hash", source);
        Assert.DoesNotContain("walos2024", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}", source);
        Assert.Contains("to_regclass('platform.features')", source);
        Assert.Contains("to_regclass('platform.company_features')", source);
        Assert.Contains("INSERT INTO platform.features", source);
        Assert.Contains("DELETE FROM platform.features", source);
        Assert.Contains("WHERE code NOT IN", source);
        Assert.Contains("INSERT INTO platform.company_features", source);
        Assert.Contains("CROSS JOIN platform.features f", source);
        Assert.Contains("ON CONFLICT (company_id, feature_code) DO NOTHING", source);

        var canonicalRows = new[]
        {
            "('dashboard',   'Dashboard',     'Resumen operativo del comercio',                 TRUE,  TRUE,  TRUE, 1)",
            "('inventory',   'Inventario',    'Productos, existencias, movimientos y alertas',  TRUE,  FALSE, TRUE, 2)",
            "('restaurant',  'Restaurante',   'Mesas, ordenes y cierre de venta restaurante',   TRUE,  FALSE, TRUE, 3)",
            "('pos',         'POS-Deli',      'Venta rapida de mostrador',                      TRUE,  FALSE, TRUE, 4)",
            "('cash',        'Caja',          'Apertura, movimientos y cierre de caja',         TRUE,  FALSE, TRUE, 5)",
            "('purchases',   'Compras',       'Ordenes de compra y recepcion',                  TRUE,  FALSE, TRUE, 6)",
            "('suppliers',   'Proveedores',   'Catalogo y gestion de proveedores',              TRUE,  FALSE, TRUE, 7)",
            "('delivery',    'Domicilios',    'Pedidos y seguimiento de domicilios',            TRUE,  FALSE, TRUE, 8)",
            "('finance',     'Finanzas',      'Ingresos, egresos y reportes financieros',       TRUE,  FALSE, TRUE, 9)",
            "('ai',          'Asistente IA',  'Asistente inteligente de Walos',                 FALSE, FALSE, TRUE, 10)"
        };

        Assert.All(canonicalRows, row => Assert.Contains(row, source));
    }

    [Fact]
    public void PreparedRefundMigration_AddsNullableItemAttributionWithoutBackfill()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "..", "supabase", "migrations", "020_refund_preparados_source_item.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS source_order_item_id BIGINT", source);
        Assert.Contains("REFERENCES sales.order_items(id)", source);
        Assert.Contains("ON DELETE SET NULL", source);
        Assert.Contains("idx_inv_movements_source_order_item", source);
        Assert.DoesNotContain("UPDATE inventory.movements", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TenantCreation_InsertsDefaultFeaturesInsideExistingTransaction()
    {
        var source = ReadRepositoryFile(Path.Combine("src", "Walos.Infrastructure", "Repositories", "AdminRepository.cs"));

        Assert.Contains("INSERT INTO platform.company_features", source);
        Assert.Contains("SELECT @CompanyId, f.code, f.default_enabled", source);
        Assert.Contains("new { CompanyId = company.Id }, tx", source);
    }

    [Fact]
    public void TenantListing_DerivesSystemIdentityFromTheCanonicalServerSideTaxId()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "src", "Walos.Infrastructure", "Repositories", "AdminRepository.cs"));

        Assert.Contains("(c.tax_id = 'WALOS-SYSTEM-001') AS IsSystem", source);
    }

    [Fact]
    public void FeatureRepository_AnchorsFallbacksToAnExistingNonDeletedCompany()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "src", "Walos.Infrastructure", "Repositories", "CompanyFeatureRepository.cs"));

        Assert.Contains("FROM core.companies c", source);
        Assert.Contains("c.id = @CompanyId", source);
        Assert.Contains("c.deleted_at IS NULL", source);
        Assert.Contains("COALESCE(cf.is_enabled, f.default_enabled)", source);
    }

    [Fact]
    public void FeatureRepository_SerializesBothDirectionsOfTheCashDependencyPerCompany()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "src", "Walos.Infrastructure", "Repositories", "CompanyFeatureRepository.cs"));

        Assert.Contains("BeginTransaction(IsolationLevel.ReadCommitted)", source);
        Assert.Contains("FOR UPDATE", source);
        Assert.Contains("featureCode == WalosFeatures.Restaurant || featureCode == WalosFeatures.Pos", source);
        Assert.Contains("CashFeatureCode = WalosFeatures.Cash", source);
        Assert.Contains("feature_dependency_conflict", source);
    }

    [Fact]
    public void BranchDeactivation_ConsidersPendingAndOrderedPurchaseOrdersInTenantAndBranchScope()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "src", "Walos.Infrastructure", "Repositories", "AdminRepository.cs"));

        Assert.Contains("FROM suppliers.purchase_orders", source);
        Assert.Contains("company_id = @CompanyId AND branch_id = @BranchId", source);
        Assert.Contains("status IN ('pending', 'ordered')", source);
    }

    [Fact]
    public void BranchDeactivation_ConsidersPendingAndPartialCreditsInTenantAndBranchScope()
    {
        var source = ReadRepositoryFile(Path.Combine(
            "src", "Walos.Infrastructure", "Repositories", "AdminRepository.cs"));

        Assert.Contains("FROM sales.credits", source);
        Assert.Contains("company_id = @CompanyId AND branch_id = @BranchId", source);
        Assert.Contains("status IN ('pending', 'partial')", source);
    }
}
