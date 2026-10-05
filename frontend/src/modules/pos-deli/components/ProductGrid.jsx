import { useState } from 'react';
import { Package, Plus, Scale } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';
import { resolveAssetUrl } from '../../../utils/assetUrl';

const ProductThumbnail = ({ reference }) => {
  const url = resolveAssetUrl(reference);
  const [failedUrl, setFailedUrl] = useState(null);
  return url && failedUrl !== url
    ? <img src={url} alt="" loading="lazy" onError={() => setFailedUrl(url)} className="h-full w-full object-cover" />
    : <Package aria-hidden="true" className="h-6 w-6 text-gray-300" />;
};

const ProductGrid = ({ weighedProducts, unitProducts, onSelectWeighed, onSelectUnit, disabled = false }) => {
  const products = [...unitProducts, ...weighedProducts];
  if (products.length === 0) {
    return <div className="flex min-h-[240px] flex-col items-center justify-center gap-3 text-center text-gray-400"><Package className="h-10 w-10" /><p className="font-semibold text-gray-600">No hay productos para mostrar</p><p className="text-sm">Probá otra búsqueda o categoría.</p></div>;
  }
  return (
    <div className="grid grid-cols-2 gap-1.5 xl:grid-cols-3 2xl:grid-cols-4 min-[1800px]:grid-cols-5">
      {products.map((product) => (
        <button
          key={product.id}
          type="button"
          title={product.name}
          disabled={disabled}
          onClick={() => (product.isWeighed ? onSelectWeighed : onSelectUnit)(product)}
          className="group flex min-h-[100px] min-w-0 flex-col gap-1 overflow-hidden rounded-xl border border-gray-200 bg-white p-2 text-left shadow-sm transition hover:border-primary-400 hover:shadow-md focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary-500 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <div className="flex min-w-0 items-start gap-2">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-gray-50">
              <ProductThumbnail reference={product.imageUrl} />
            </div>
            <div className="min-w-0 flex-1">
              <span className="block truncate text-[10px] font-medium uppercase leading-3 tracking-wide text-gray-400">{product.categoryName || 'Sin categoría'}</span>
              <span className="mt-1 line-clamp-2 min-h-[32px] text-sm font-bold leading-4 text-gray-900">{product.name}</span>
            </div>
          </div>
          <div className="mt-auto flex flex-wrap items-center justify-between gap-1">
            <span className="text-base font-black tabular-nums text-primary-600">{formatCurrency(product.salePrice)}{product.isWeighed && <span className="text-xs font-medium">/kg</span>}</span>
            <span className="ml-auto shrink-0 rounded-lg bg-primary-50 p-1.5 text-primary-600 group-hover:bg-primary-600 group-hover:text-white">{product.isWeighed ? <Scale aria-hidden="true" className="h-4 w-4" /> : <Plus aria-hidden="true" className="h-4 w-4" />}</span>
          </div>
        </button>
      ))}
    </div>
  );
};

export default ProductGrid;
