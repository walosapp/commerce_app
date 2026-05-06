/**
 * Barra de Estado de Caja
 * ¿Qué es? Indicador superior del estado de la caja registradora
 * ¿Para qué? Mostrar si la caja está abierta/cerrada, totales en vivo y acciones rápidas
 */

import { useState } from 'react';
import { Wallet, DoorOpen, DoorClosed, ArrowDownCircle, ArrowUpCircle, Clock } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const CashRegisterBar = ({ register, onOpen, onClose, onMovement, onHistory }) => {
  if (!register) {
    return (
      <div className="bg-amber-50 border-b border-amber-200 px-4 md:px-6 py-3 flex items-center justify-between gap-3 flex-wrap flex-shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center">
            <Wallet size={16} className="text-amber-600" />
          </div>
          <div>
            <p className="text-sm font-semibold text-amber-800">Caja cerrada</p>
            <p className="text-xs text-amber-600">Debes abrir caja para facturar</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={onHistory}
            className="flex items-center gap-1.5 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-medium text-amber-700 hover:bg-amber-50 transition-colors"
          >
            <Clock size={14} /> Historial
          </button>
          <button
            onClick={onOpen}
            className="flex items-center gap-1.5 rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-amber-700 transition-colors"
          >
            <DoorOpen size={14} /> Abrir Caja
          </button>
        </div>
      </div>
    );
  }

  const elapsed = Math.floor((Date.now() - new Date(register.openedAt).getTime()) / 60000);
  const hours = Math.floor(elapsed / 60);
  const mins = elapsed % 60;
  const timeStr = hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;

  return (
    <div className="bg-green-50 border-b border-green-200 px-4 md:px-6 py-3 flex items-center justify-between gap-3 flex-wrap flex-shrink-0">
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-green-100 flex items-center justify-center">
          <Wallet size={16} className="text-green-600" />
        </div>
        <div className="flex items-center gap-4 flex-wrap">
          <div>
            <p className="text-sm font-semibold text-green-800">Caja abierta</p>
            <p className="text-xs text-green-600">{timeStr} · {register.orderCount} orden{register.orderCount !== 1 ? 'es' : ''}</p>
          </div>
          <div className="hidden sm:flex items-center gap-4 text-xs">
            <span className="text-green-700">
              <span className="text-green-500">Ventas:</span>{' '}
              <span className="font-semibold">{formatCurrency(register.totalSales)}</span>
            </span>
            <span className="text-green-700">
              <span className="text-green-500">Efectivo:</span>{' '}
              <span className="font-semibold">{formatCurrency(register.totalCashSales)}</span>
            </span>
            <span className="text-green-700">
              <span className="text-green-500">Entradas:</span>{' '}
              <span className="font-semibold">{formatCurrency(register.cashIn)}</span>
            </span>
            <span className="text-green-700">
              <span className="text-green-500">Salidas:</span>{' '}
              <span className="font-semibold">{formatCurrency(register.cashOut)}</span>
            </span>
          </div>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <button
          onClick={() => onMovement('in')}
          className="flex items-center gap-1.5 rounded-lg border border-green-300 bg-white px-3 py-1.5 text-xs font-medium text-green-700 hover:bg-green-50 transition-colors"
          title="Entrada de efectivo"
        >
          <ArrowDownCircle size={14} /> Entrada
        </button>
        <button
          onClick={() => onMovement('out')}
          className="flex items-center gap-1.5 rounded-lg border border-green-300 bg-white px-3 py-1.5 text-xs font-medium text-green-700 hover:bg-green-50 transition-colors"
          title="Salida de efectivo"
        >
          <ArrowUpCircle size={14} /> Salida
        </button>
        <button
          onClick={onHistory}
          className="flex items-center gap-1.5 rounded-lg border border-green-300 bg-white px-3 py-1.5 text-xs font-medium text-green-700 hover:bg-green-50 transition-colors"
        >
          <Clock size={14} /> Historial
        </button>
        <button
          onClick={onClose}
          className="flex items-center gap-1.5 rounded-lg bg-red-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-600 transition-colors"
        >
          <DoorClosed size={14} /> Cerrar Caja
        </button>
      </div>
    </div>
  );
};

export default CashRegisterBar;
