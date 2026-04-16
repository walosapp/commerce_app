import { useQuery } from '@tanstack/react-query';
import { X, Truck, Loader2, Phone, Mail, MapPin, FileText, Edit2, Trash2 } from 'lucide-react';
import supplierService from '../../../services/supplierService';
import SupplierProductsManager from './SupplierProductsManager';
import SuggestedOrderPanel from './SuggestedOrderPanel';
import ContactActions from './ContactActions';

const SupplierDetailPanel = ({ supplierId, onClose, onEdit, onDelete }) => {
  const { data, isLoading } = useQuery({
    queryKey: ['supplier', supplierId],
    queryFn: () => supplierService.getById(supplierId),
    enabled: !!supplierId,
  });

  const supplier = data?.data;

  return (
    <div className="fixed inset-y-0 right-0 z-40 w-full sm:w-[420px] bg-white shadow-xl border-l flex flex-col">
      <div className="flex items-center gap-3 px-5 py-4 border-b">
        <Truck size={20} className="text-primary-600" />
        <h2 className="font-semibold text-gray-900 truncate">
          {supplier?.name ?? 'Proveedor'}
        </h2>
        <div className="ml-auto flex items-center gap-1">
          {supplier && (
            <>
              <button onClick={() => onEdit(supplier)} className="p-1.5 text-gray-400 hover:text-primary-600 hover:bg-gray-100 rounded-lg transition-colors" title="Editar">
                <Edit2 size={16} />
              </button>
              <button onClick={() => onDelete(supplier)} className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors" title="Eliminar">
                <Trash2 size={16} />
              </button>
            </>
          )}
          <button onClick={onClose} className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"><X size={18} /></button>
        </div>
      </div>

      {isLoading ? (
        <div className="flex-1 flex items-center justify-center">
          <Loader2 size={22} className="animate-spin text-primary-500" />
        </div>
      ) : !supplier ? (
        <div className="flex-1 flex items-center justify-center text-gray-400 text-sm">Proveedor no encontrado</div>
      ) : (
        <div className="flex-1 overflow-y-auto">
          <div className="px-5 py-4 space-y-5">
            <div className="bg-gray-50 rounded-xl p-4 space-y-2">
              {supplier.contactName && (
                <div className="flex items-center gap-2 text-sm text-gray-700">
                  <span className="font-medium w-20 shrink-0 text-gray-500">Contacto</span>
                  <span>{supplier.contactName}</span>
                </div>
              )}
              {supplier.phone && (
                <div className="flex items-center gap-2 text-sm text-gray-700">
                  <Phone size={13} className="text-gray-400 shrink-0" />
                  <span>{supplier.phone}</span>
                </div>
              )}
              {supplier.email && (
                <div className="flex items-center gap-2 text-sm text-gray-700">
                  <Mail size={13} className="text-gray-400 shrink-0" />
                  <span className="truncate">{supplier.email}</span>
                </div>
              )}
              {supplier.address && (
                <div className="flex items-center gap-2 text-sm text-gray-700">
                  <MapPin size={13} className="text-gray-400 shrink-0" />
                  <span>{supplier.address}</span>
                </div>
              )}
              {supplier.notes && (
                <div className="flex items-start gap-2 text-sm text-gray-600 pt-1">
                  <FileText size={13} className="text-gray-400 shrink-0 mt-0.5" />
                  <span className="italic">{supplier.notes}</span>
                </div>
              )}
            </div>

            <div>
              <p className="text-sm font-semibold text-gray-700 mb-2">Contactar</p>
              <ContactActions supplier={supplier} />
            </div>

            <SuggestedOrderPanel supplier={supplier} />

            <SupplierProductsManager
              supplierId={supplierId}
              products={supplier.products ?? []}
            />
          </div>
        </div>
      )}
    </div>
  );
};

export default SupplierDetailPanel;
