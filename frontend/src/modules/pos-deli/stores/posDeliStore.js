import { create } from 'zustand';
import { persist } from 'zustand/middleware';

const usePosDeliStore = create(
  persist(
    (set, get) => ({
      items: [],
      ticketNumber: null,

      get total() {
        return get().items.reduce((sum, item) => sum + item.subtotal, 0);
      },

      getTotal: () => get().items.reduce((sum, item) => sum + item.subtotal, 0),

      addWeighedItem: (product, weight) => {
        const subtotal = Math.round(weight * product.salePrice);
        set((state) => ({
          items: [
            ...state.items,
            {
              id: crypto.randomUUID(),
              productId: product.id,
              name: product.name,
              quantity: weight,
              unit: 'kg',
              unitPrice: product.salePrice,
              subtotal,
              isWeighed: true,
            },
          ],
        }));
      },

      addUnitItem: (product) => {
        set((state) => {
          const existing = state.items.find(
            (i) => i.productId === product.id && !i.isWeighed
          );
          if (existing) {
            return {
              items: state.items.map((i) =>
                i.id === existing.id
                  ? { ...i, quantity: i.quantity + 1, subtotal: (i.quantity + 1) * i.unitPrice }
                  : i
              ),
            };
          }
          return {
            items: [
              ...state.items,
              {
                id: crypto.randomUUID(),
                productId: product.id,
                name: product.name,
                quantity: 1,
                unit: 'und',
                unitPrice: product.salePrice,
                subtotal: product.salePrice,
                isWeighed: false,
              },
            ],
          };
        });
      },

      removeItem: (itemId) => {
        set((state) => ({
          items: state.items.filter((i) => i.id !== itemId),
        }));
      },

      updateQuantity: (itemId, newQty) => {
        if (newQty <= 0) {
          get().removeItem(itemId);
          return;
        }
        set((state) => ({
          items: state.items.map((i) =>
            i.id === itemId
              ? { ...i, quantity: newQty, subtotal: Math.round(newQty * i.unitPrice) }
              : i
          ),
        }));
      },

      setSelectedItemId: (itemId) => set({ selectedItemId: itemId }),
      selectedItemId: null,

      clearTicket: () => set({ items: [], selectedItemId: null }),
    }),
    {
      name: 'pos-deli-ticket',
      storage: {
        getItem: (name) => {
          const value = sessionStorage.getItem(name);
          return value ? JSON.parse(value) : null;
        },
        setItem: (name, value) => sessionStorage.setItem(name, JSON.stringify(value)),
        removeItem: (name) => sessionStorage.removeItem(name),
      },
    }
  )
);

export default usePosDeliStore;
