import { MessageCircle, Mail } from 'lucide-react';

const buildMessage = (supplier, items, mode = 'suggested') => {
  const name = supplier.contactName || supplier.name;
  if (!items || items.length === 0) {
    return `Hola ${name}, me gustaría hacer un pedido. ¿Podría enviarme su lista de precios actualizada?`;
  }
  if (mode === 'order') {
    const lines = items.map(i => {
      const subtotal = (Number(i.quantity) || 0) * (Number(i.unitCost) || 0);
      return `• ${i.productName}: ${Number(i.quantity)} unid. x $${Number(i.unitCost).toLocaleString('es-CO')} = $${subtotal.toLocaleString('es-CO')}`;
    }).join('\n');
    const total = items.reduce((s, i) => s + (Number(i.quantity)||0)*(Number(i.unitCost)||0), 0);
    const date  = new Date().toLocaleDateString('es-CO', { dateStyle: 'long' });
    return `Hola ${name},\n\nLe enviamos la siguiente orden de compra (${date}):\n\n${lines}\n\n*Total: $${total.toLocaleString('es-CO')}*\n\nQuedamos atentos a su confirmación. Gracias.`;
  }
  const lines = items.map(i =>
    `• ${i.productName}: ${i.suggestedQty} unidades${i.unitCost ? ` (~$${Number(i.unitCost).toLocaleString('es-CO')}/und)` : ''}`
  ).join('\n');
  return `Hola ${name},\n\nNos gustaría realizar el siguiente pedido:\n\n${lines}\n\nQuedamos atentos. Gracias!`;
};

const ContactActions = ({ supplier, suggestedItems = [], orderItems, mode = 'suggested' }) => {
  const items   = mode === 'order' ? (orderItems ?? []) : suggestedItems;
  const message = buildMessage(supplier, items, mode);

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
