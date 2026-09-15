import CatalogSettings from './components/CatalogSettings';

const CatalogPage = () => (
  <div className="space-y-6">
    <div>
      <h1 className="text-2xl font-bold text-gray-900">Catalogo</h1>
      <p className="mt-1 text-sm text-gray-500">Administra categorias y unidades del inventario.</p>
    </div>
    <CatalogSettings />
  </div>
);

export default CatalogPage;
