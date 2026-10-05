import { Search, ScanBarcode } from 'lucide-react';

const ProductSearchBar = ({ value, onChange, inputRef }) => {
  return (
    <div className="relative">
      <Search className="absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
      <input
        ref={inputRef}
        data-barcode-allowed="true"
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-label="Buscar producto"
        placeholder="Buscar producto, SKU o código..."
        className="w-full rounded-xl border border-gray-200 bg-gray-50 py-3 pl-12 pr-12 text-sm text-gray-900 outline-none transition-colors focus:border-primary-500 focus:bg-white"
      />
      <ScanBarcode className="absolute right-4 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-300" />
    </div>
  );
};

export default ProductSearchBar;
