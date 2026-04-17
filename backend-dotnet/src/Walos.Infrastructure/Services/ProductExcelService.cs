using ClosedXML.Excel;
using Walos.Application.DTOs.Common;

namespace Walos.Infrastructure.Services;

public record ProductImportRow(
    string Name, string Sku, string? Barcode, string? Description,
    string CategoryName, string UnitName,
    decimal CostPrice, decimal SalePrice, decimal? MarginPercentage,
    string ProductType, bool TrackStock, bool IsForSale,
    decimal MinStock, decimal MaxStock, decimal ReorderPoint,
    int RowNumber, string? Error = null);

public class ProductExcelService
{
    private static readonly (string Header, int Width)[] Columns =
    [
        ("nombre *",          25),
        ("sku *",             15),
        ("codigo_barras",     15),
        ("descripcion",       30),
        ("categoria *",       20),
        ("unidad *",          15),
        ("costo *",           12),
        ("precio_venta",      12),
        ("margen_%",          10),
        ("tipo_producto",     16),
        ("controlar_stock",   14),
        ("disponible_venta",  14),
        ("stock_minimo",      12),
        ("stock_maximo",      12),
        ("punto_reorden",     12),
    ];

    private static readonly string[] ValidTypes = ["simple", "supply", "prepared", "service"];

    public byte[] GenerateTemplate(IEnumerable<string> categories, IEnumerable<string> units)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Productos");

        // ── Header row ──────────────────────────────────────────────────
        for (int i = 0; i < Columns.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = Columns[i].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(i + 1).Width = Columns[i].Width;
        }

        // ── Example row ─────────────────────────────────────────────────
        var row2 = ws.Row(2);
        row2.Cell(1).Value  = "Ejemplo Producto";
        row2.Cell(2).Value  = "PROD-001";
        row2.Cell(3).Value  = "";
        row2.Cell(4).Value  = "Descripcion opcional";
        row2.Cell(5).Value  = categories.FirstOrDefault() ?? "Bebidas";
        row2.Cell(6).Value  = units.FirstOrDefault() ?? "Unidad";
        row2.Cell(7).Value  = 1000;
        row2.Cell(8).Value  = 1500;
        row2.Cell(9).Value  = 50;
        row2.Cell(10).Value = "simple";
        row2.Cell(11).Value = "si";
        row2.Cell(12).Value = "si";
        row2.Cell(13).Value = 5;
        row2.Cell(14).Value = 100;
        row2.Cell(15).Value = 10;
        row2.Style.Fill.BackgroundColor = XLColor.FromArgb(243, 244, 246);

        // ── Instructions sheet ──────────────────────────────────────────
        var wsInfo = wb.Worksheets.Add("Instrucciones");
        wsInfo.Cell("A1").Value = "INSTRUCCIONES DE IMPORTACION";
        wsInfo.Cell("A1").Style.Font.Bold = true;
        wsInfo.Cell("A1").Style.Font.FontSize = 13;

        var info = new[]
        {
            ("A3",  "COLUMNAS OBLIGATORIAS: nombre, sku, categoria, unidad, costo"),
            ("A4",  "precio_venta: si se omite (0), se calcula con margen_%"),
            ("A5",  "margen_%: margen de ganancia en porcentaje (ej: 30 para 30%)"),
            ("A6",  "tipo_producto: simple | supply | prepared | service"),
            ("A7",  "controlar_stock: si / no"),
            ("A8",  "disponible_venta: si / no"),
            ("A9",  "stock_minimo / stock_maximo / punto_reorden: numeros (default 0)"),
            ("A11", "CATEGORIAS DISPONIBLES:"),
            ("A12", string.Join(", ", categories.Any() ? categories : ["(ninguna - crear en Configuracion > Catalogo)"])),
            ("A14", "UNIDADES DISPONIBLES:"),
            ("A15", string.Join(", ", units.Any() ? units : ["(ninguna - crear en Configuracion > Catalogo)"])),
        };

        foreach (var (addr, text) in info)
            wsInfo.Cell(addr).Value = text;

        wsInfo.Column("A").Width = 80;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public (List<ProductImportRow> Valid, List<ProductImportRow> Errors) ParseImport(
        Stream stream,
        IReadOnlyDictionary<string, long> categoriesMap,
        IReadOnlyDictionary<string, long> unitsMap)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Productos") ?? wb.Worksheets.First();

        var valid = new List<ProductImportRow>();
        var errors = new List<ProductImportRow>();

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            string Get(int col) => row.Cell(col).GetString().Trim();
            bool Flag(int col) => Get(col).ToLowerInvariant() is "si" or "yes" or "true" or "1";
            decimal Dec(int col, decimal def = 0) => decimal.TryParse(Get(col), out var v) ? v : def;

            var name = Get(1);
            if (string.IsNullOrEmpty(name)) continue;

            var err = new List<string>();

            var sku      = Get(2);
            if (string.IsNullOrEmpty(sku)) err.Add("sku requerido");

            var catName  = Get(5).ToLowerInvariant();
            if (!categoriesMap.TryGetValue(catName, out _)) err.Add($"categoria '{Get(5)}' no encontrada");

            var unitName = Get(6).ToLowerInvariant();
            if (!unitsMap.TryGetValue(unitName, out _)) err.Add($"unidad '{Get(6)}' no encontrada");

            var cost     = Dec(7);
            if (cost <= 0) err.Add("costo debe ser mayor a 0");

            var type     = Get(10).ToLowerInvariant();
            if (!ValidTypes.Contains(type)) type = "simple";

            decimal sale  = Dec(8);
            decimal margin = Dec(9);
            if (sale <= 0 && margin > 0 && cost > 0)
                sale = Math.Round(cost * (1 + margin / 100), 2);

            var importRow = new ProductImportRow(
                Name: name,
                Sku: sku,
                Barcode: Get(3) is "" ? null : Get(3),
                Description: Get(4) is "" ? null : Get(4),
                CategoryName: catName,
                UnitName: unitName,
                CostPrice: cost,
                SalePrice: sale,
                MarginPercentage: margin > 0 ? margin : null,
                ProductType: type,
                TrackStock: Get(11).ToLowerInvariant() is not "no",
                IsForSale: Get(12).ToLowerInvariant() is not "no",
                MinStock: Dec(13),
                MaxStock: Dec(14),
                ReorderPoint: Dec(15),
                RowNumber: r,
                Error: err.Count > 0 ? string.Join("; ", err) : null
            );

            if (err.Count == 0) valid.Add(importRow);
            else errors.Add(importRow);
        }

        return (valid, errors);
    }
}
