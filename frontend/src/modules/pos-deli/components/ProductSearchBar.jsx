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
        placeholder="Buscar por nombre, SKU o barcode..."
        className="w-full rounded-2xl border border-gray-200 bg-white py-4 pl-12 pr-14 text-base text-gray-900 shadow-sm outline-none transition-colors focus:border-primary-500"
      />
      <ScanBarcode className="absolute right-4 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-300" />
    </div>
  );
};

export default ProductSearchBar;
