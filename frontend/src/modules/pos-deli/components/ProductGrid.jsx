import { formatCurrency } from '../../../utils/formatCurrency';

const ProductSection = ({ title, products, onClick, emptyText }) => (
  <section>
    <div className="mb-3 flex items-center justify-between">
      <h3 className="text-sm font-black uppercase tracking-wide text-gray-700">{title}</h3>
      <span className="text-xs text-gray-400">{products.length} productos</span>
    </div>

    {products.length === 0 ? (
      <div className="rounded-2xl border border-dashed border-gray-200 bg-white px-4 py-8 text-center text-sm text-gray-400">
        {emptyText}
      </div>
    ) : (
      <div className="grid grid-cols-2 gap-3 xl:grid-cols-3">
        {products.map((product) => (
          <button
            key={product.id}
            type="button"
            onClick={() => onClick(product)}
            className="rounded-2xl border border-gray-200 bg-white p-4 text-left shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary-300 hover:shadow-md"
          >
            <p className="line-clamp-2 min-h-[40px] text-sm font-bold text-gray-900">{product.name}</p>
            <p className="mt-2 text-lg font-black text-primary-600">
              {formatCurrency(product.salePrice)}
              {product.isWeighed ? <span className="ml-1 text-xs font-semibold text-primary-500">/kg</span> : null}
            </p>
            <div className="mt-2 flex items-center justify-between text-xs text-gray-500">
              <span className="truncate">{product.categoryName || 'Sin categoría'}</span>
              <span>{product.unitAbbreviation || '-'}</span>
            </div>
          </button>
        ))}
      </div>
    )}
  </section>
);

const ProductGrid = ({ weighedProducts, unitProducts, onSelectWeighed, onSelectUnit }) => {
  return (
    <div className="space-y-6">
      <ProductSection
        title="Pesables"
        products={weighedProducts}
        onClick={onSelectWeighed}
        emptyText="No hay productos pesables para mostrar"
      />

      <ProductSection
        title="Por unidad"
        products={unitProducts}
        onClick={onSelectUnit}
        emptyText="No hay productos por unidad para mostrar"
      />
    </div>
  );
};

export default ProductGrid;
