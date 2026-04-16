import { MessageCircle, Mail } from 'lucide-react';

const buildMessage = (supplier, items) => {
  if (!items || items.length === 0) {
    return `Hola ${supplier.contactName || supplier.name}, me gustaría hacer un pedido. ¿Podría enviarme su lista de precios actualizada?`;
  }
  const lines = items.map(i =>
    `• ${i.productName}: ${i.suggestedQty} unidades${i.unitCost ? ` (~$${Number(i.unitCost).toLocaleString('es-CO')}/und)` : ''}`
  ).join('\n');
  return `Hola ${supplier.contactName || supplier.name},\n\nNos gustaría realizar el siguiente pedido:\n\n${lines}\n\nQuedamos atentos. Gracias!`;
};

const ContactActions = ({ supplier, suggestedItems = [] }) => {
  const message = buildMessage(supplier, suggestedItems);

  const handleWhatsApp = () => {
    const phone = supplier.phone?.replace(/\D/g, '');
    if (!phone) return;
    window.open(`https://wa.me/${phone}?text=${encodeURIComponent(message)}`, '_blank');
  };

  const handleEmail = () => {
    if (!supplier.email) return;
    const subject = encodeURIComponent(`Pedido - ${new Date().toLocaleDateString('es-CO')}`);
    const body = encodeURIComponent(message);
    window.open(`mailto:${supplier.email}?subject=${subject}&body=${body}`, '_blank');
  };

  return (
    <div className="flex gap-2">
      {supplier.phone && (
        <button
          onClick={handleWhatsApp}
          className="flex items-center gap-1.5 bg-green-500 hover:bg-green-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
        >
          <MessageCircle size={14} /> WhatsApp
        </button>
      )}
      {supplier.email && (
        <button
          onClick={handleEmail}
          className="flex items-center gap-1.5 bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
        >
          <Mail size={14} /> Email
        </button>
      )}
      {!supplier.phone && !supplier.email && (
        <span className="text-xs text-gray-400">Sin datos de contacto</span>
      )}
    </div>
  );
};

export default ContactActions;
