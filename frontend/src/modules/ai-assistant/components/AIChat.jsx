/**
 * Chat con IA para Inventario
 * ¿Qué es? Componente de chat interactivo con asistente de IA
 * ¿Para qué? Permitir registrar pedidos y consultar inventario por texto/voz
 */

import { useState, useRef, useEffect } from 'react';
import { Send, Mic, MicOff, Bot, User, Check, X, Loader2 } from 'lucide-react';
import inventoryService from '../../../services/inventoryService';
import toast from 'react-hot-toast';

const ACTION_LABELS = {
  add_stock: '📦 Agregar stock',
  create_and_stock: '🆕 Crear producto y agregar stock',
  need_info: '❓ Se necesita más información',
};

const AIChat = ({ onActionConfirmed }) => {
  const [messages, setMessages] = useState([
    {
      role: 'assistant',
      content: '¡Hola! Soy tu asistente de inventario. Puedes decirme cosas como:\n\n• "Me llegaron 24 cervezas Corona a $18 cada una"\n• "Me llegaron 54 cervezas Águila, el total fue 95000"\n• "¿Cuánto estoy ganando en total?"\n• "Muéstrame productos con stock bajo"\n\nSi el producto no existe, te ayudo a crearlo.',
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [pendingAction, setPendingAction] = useState(null);
  const [sessionId, setSessionId] = useState(null);
  const messagesEndRef = useRef(null);
  const recognitionRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  // Web Speech API setup
  useEffect(() => {
    if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
      const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
      recognitionRef.current = new SpeechRecognition();
      recognitionRef.current.continuous = false;
      recognitionRef.current.interimResults = false;
      recognitionRef.current.lang = 'es-MX';

      recognitionRef.current.onresult = (event) => {
        const transcript = event.results[0][0].transcript;
        setInput(transcript);
        setIsListening(false);
      };

      recognitionRef.current.onerror = () => {
        setIsListening(false);
        toast.error('Error con el micrófono. Intenta de nuevo.');
      };

      recognitionRef.current.onend = () => {
        setIsListening(false);
      };
    }
  }, []);

  const toggleListening = () => {
    if (!recognitionRef.current) {
      toast.error('Tu navegador no soporta reconocimiento de voz');
      return;
    }

    if (isListening) {
      recognitionRef.current.stop();
      setIsListening(false);
    } else {
      recognitionRef.current.start();
      setIsListening(true);
    }
  };

  const handleSend = async () => {
    if (!input.trim() || isLoading) return;

    const userMessage = input.trim();
    setInput('');
    setMessages((prev) => [...prev, { role: 'user', content: userMessage }]);
    setIsLoading(true);

    try {
      const result = await inventoryService.processAIInput(userMessage, 'text', sessionId);
      const data = result.data || result;

      if (data.sessionId) setSessionId(data.sessionId);

      const assistantMessage = data.response || data.message || 'Procesado correctamente';
      setMessages((prev) => [...prev, { role: 'assistant', content: assistantMessage }]);

      if (data.action === 'need_info') {
        // No confirmation needed, AI is asking for more details
        return;
      }

      if (data.interactionId && data.action && data.action !== 'query') {
        setPendingAction({
          interactionId: data.interactionId,
          action: data.action,
          confidence: data.confidence,
          data: data.data,
        });
      }
    } catch (error) {
      const errorMsg = error.response?.data?.message || 'Error al procesar tu solicitud';
      setMessages((prev) => [...prev, { role: 'assistant', content: `❌ ${errorMsg}` }]);
      toast.error('Error al comunicarse con el asistente');
    } finally {
      setIsLoading(false);
    }
  };

  const handleConfirm = async () => {
    if (!pendingAction) return;

    setIsLoading(true);
    try {
      const result = await inventoryService.confirmAIAction(pendingAction.interactionId);
      const msg = result?.data?.message || result?.message || 'Acción aplicada correctamente';
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: `✅ ${msg}` },
      ]);
      setPendingAction(null);
      toast.success(msg);
      onActionConfirmed?.();
    } catch (error) {
      const errorMsg = error.response?.data?.message || 'Error al confirmar la acción';
      setMessages((prev) => [...prev, { role: 'assistant', content: `❌ ${errorMsg}` }]);
      toast.error(errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleReject = () => {
    setPendingAction(null);
    setMessages((prev) => [
      ...prev,
      { role: 'assistant', content: 'Acción cancelada. ¿En qué más puedo ayudarte?' },
    ]);
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <div className="card flex flex-col h-[600px] p-0">
      {/* Header */}
      <div className="flex items-center gap-3 border-b border-gray-200 px-4 py-3">
        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-100">
          <Bot className="h-4 w-4 text-primary-600" />
        </div>
        <div>
          <h3 className="text-sm font-semibold text-gray-900">Asistente de Inventario</h3>
          <p className="text-xs text-gray-500">Powered by IA</p>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-hide">
        {messages.map((msg, index) => (
          <div
            key={index}
            className={`flex gap-3 ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            {msg.role === 'assistant' && (
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary-100">
                <Bot className="h-3.5 w-3.5 text-primary-600" />
              </div>
            )}
            <div
              className={`max-w-[80%] rounded-lg px-4 py-2.5 text-sm whitespace-pre-line ${
                msg.role === 'user'
                  ? 'bg-primary-500 text-white'
                  : 'bg-gray-100 text-gray-900'
              }`}
            >
              {msg.content}
            </div>
            {msg.role === 'user' && (
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gray-200">
                <User className="h-3.5 w-3.5 text-gray-600" />
              </div>
            )}
          </div>
        ))}

        {isLoading && (
          <div className="flex gap-3">
            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary-100">
              <Bot className="h-3.5 w-3.5 text-primary-600" />
            </div>
            <div className="flex items-center gap-2 rounded-lg bg-gray-100 px-4 py-2.5 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" />
              Procesando...
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Pending Action Confirmation */}
      {pendingAction && (
        <div className="border-t border-yellow-200 bg-yellow-50 px-4 py-3">
          <p className="text-sm font-medium text-yellow-900">
            {ACTION_LABELS[pendingAction.action] || pendingAction.action}
          </p>
          {pendingAction.data?.products?.length > 0 && (
            <div className="mt-1 space-y-1">
              {pendingAction.data.products.map((p, i) => (
                <div key={i} className="text-xs text-yellow-800">
                  <p>
                    {p.is_new && <span className="font-semibold text-green-700">[NUEVO] </span>}
                    {p.name} — {p.quantity} uds × ${p.unit_cost?.toLocaleString()}
                  </p>
                  {p.is_new && p.profit_margin > 0 && (
                    <p className="ml-4 text-yellow-700">
                      Margen: {p.profit_margin}% → Venta: ${p.sale_price?.toLocaleString() || Math.round(p.unit_cost * (1 + p.profit_margin / 100)).toLocaleString()}
                    </p>
                  )}
                  {p.is_new && !p.profit_margin && p.sale_price > 0 && (
                    <p className="ml-4 text-yellow-700">Venta: ${p.sale_price?.toLocaleString()}</p>
                  )}
                </div>
              ))}
              {pendingAction.data.total > 0 && (
                <p className="text-xs font-semibold text-yellow-900 mt-1">
                  Total: ${pendingAction.data.total?.toLocaleString()}
                </p>
              )}
            </div>
          )}
          {pendingAction.confidence > 0 && (
            <p className="text-xs text-yellow-700 mt-1">
              Confianza: {pendingAction.confidence}%
            </p>
          )}
          <div className="mt-2 flex gap-2">
            <button
              onClick={handleConfirm}
              disabled={isLoading}
              className="flex items-center rounded-md bg-green-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-700 disabled:opacity-50"
            >
              <Check className="mr-1 h-3 w-3" />
              Confirmar
            </button>
            <button
              onClick={handleReject}
              disabled={isLoading}
              className="flex items-center rounded-md bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700 disabled:opacity-50"
            >
              <X className="mr-1 h-3 w-3" />
              Cancelar
            </button>
          </div>
        </div>
      )}

      {/* Input */}
      <div className="border-t border-gray-200 p-4">
        <div className="flex items-center gap-2">
          <button
            onClick={toggleListening}
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg transition-colors ${
              isListening
                ? 'bg-red-100 text-red-600 animate-pulse'
                : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
            }`}
            title={isListening ? 'Detener' : 'Hablar'}
          >
            {isListening ? <MicOff className="h-5 w-5" /> : <Mic className="h-5 w-5" />}
          </button>
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={isListening ? 'Escuchando...' : 'Escribe o habla tu pedido...'}
            disabled={isLoading}
            className="input flex-1"
          />
          <button
            onClick={handleSend}
            disabled={!input.trim() || isLoading}
            className="btn btn-primary h-10 w-10 shrink-0 p-0"
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );
};

export default AIChat;
