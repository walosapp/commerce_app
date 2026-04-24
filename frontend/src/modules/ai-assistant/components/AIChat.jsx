import { useState, useRef, useEffect } from 'react';
import { Send, Mic, MicOff, Bot, User, Loader2, ExternalLink, CheckSquare, Square, Package, Truck } from 'lucide-react';
import aiService from '../../../services/aiService';
import toast from 'react-hot-toast';

const AGENT_LABELS = {
  inventory: { label: 'Inventario', icon: Package, color: 'text-green-600', bg: 'bg-green-100' },
  delivery: { label: 'Domicilios', icon: Truck, color: 'text-blue-600', bg: 'bg-blue-100' },
  general: { label: 'Asistente', icon: Bot, color: 'text-primary-600', bg: 'bg-primary-100' },
  orchestrator: { label: 'Asistente', icon: Bot, color: 'text-primary-600', bg: 'bg-primary-100' },
};

const ChecklistMessage = ({ payload, onSend }) => {
  const [items, setItems] = useState(payload.items || []);

  const toggle = (id) =>
    setItems((prev) => prev.map((i) => (i.id === id ? { ...i, checked: !i.checked } : i)));

  const handleConfirm = () => {
    const selected = items.filter((i) => i.checked);
    if (selected.length === 0) { toast.error('Selecciona al menos un producto'); return; }
    const names = selected.map((i) => i.name).join(', ');
    onSend(`Quiero pedir estos productos: ${names}. ¿Qué proveedor debo usar?`);
  };

  return (
    <div className="mt-2 space-y-2">
      <p className="text-xs text-gray-500 font-medium">{payload.prompt}</p>
      <div className="max-h-48 overflow-y-auto space-y-1">
        {items.map((item) => (
          <button
            key={item.id}
            onClick={() => toggle(item.id)}
            className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-xs transition-colors hover:bg-gray-200"
          >
            {item.checked
              ? <CheckSquare className="h-4 w-4 shrink-0 text-primary-600" />
              : <Square className="h-4 w-4 shrink-0 text-gray-400" />}
            <span className="flex-1 font-medium">{item.name}</span>
            <span className="text-gray-500">Stock: {item.currentStock} {item.unit} / Mín: {item.minStock}</span>
          </button>
        ))}
      </div>
      <button
        onClick={handleConfirm}
        className="btn btn-primary w-full text-xs py-1.5"
      >
        Continuar con {items.filter((i) => i.checked).length} productos
      </button>
    </div>
  );
};

const WhatsAppMessage = ({ payload }) => (
  <div className="mt-2 space-y-2">
    <div className="rounded-lg border border-green-200 bg-green-50 p-3">
      <p className="text-xs font-medium text-green-800 mb-1">Mensaje para WhatsApp:</p>
      <p className="text-xs text-green-900 whitespace-pre-line">{payload.message}</p>
    </div>
    <a
      href={payload.whatsAppUrl}
      target="_blank"
      rel="noopener noreferrer"
      className="flex items-center gap-2 rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 w-full justify-center"
    >
      <ExternalLink className="h-4 w-4" />
      Abrir WhatsApp
    </a>
  </div>
);

const DeliveryStatusMessage = ({ payload }) => (
  <div className="mt-2 rounded-lg border border-blue-200 bg-blue-50 p-3 text-xs">
    <p className="font-semibold text-blue-900">Pedido #{payload.orderNumber}</p>
    <p className="text-blue-800">Cliente: {payload.customerName}</p>
    <p className="text-blue-800">Dirección: {payload.address}</p>
    <span className="mt-1 inline-block rounded-full bg-blue-200 px-2 py-0.5 text-xs font-medium text-blue-900">
      {payload.status}
    </span>
  </div>
);

const AIChat = ({ onActionConfirmed }) => {
  const [messages, setMessages] = useState([
    {
      role: 'assistant',
      agentType: 'orchestrator',
      responseType: 'text',
      content: '¡Hola! Soy tu asistente inteligente. Puedo ayudarte con:\n\n📦 Inventario: stock bajo, ingresar productos, crear pedidos a proveedores\n🛵 Domicilios: consultar y actualizar estado de pedidos\n💬 General: cualquier pregunta sobre tu negocio\n\n¿En qué te ayudo?',
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [sessionId, setSessionId] = useState(null);
  const messagesEndRef = useRef(null);
  const recognitionRef = useRef(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) return;
    recognitionRef.current = new SR();
    recognitionRef.current.continuous = false;
    recognitionRef.current.interimResults = false;
    recognitionRef.current.lang = 'es-CO';
    recognitionRef.current.onresult = (e) => { setInput(e.results[0][0].transcript); setIsListening(false); };
    recognitionRef.current.onerror = () => { setIsListening(false); toast.error('Error con el micrófono'); };
    recognitionRef.current.onend = () => setIsListening(false);
  }, []);

  const toggleListening = () => {
    if (!recognitionRef.current) { toast.error('Tu navegador no soporta reconocimiento de voz'); return; }
    if (isListening) { recognitionRef.current.stop(); setIsListening(false); }
    else { recognitionRef.current.start(); setIsListening(true); }
  };

  const sendMessage = async (text) => {
    if (!text.trim() || isLoading) return;
    setInput('');
    setMessages((prev) => [...prev, { role: 'user', content: text }]);
    setIsLoading(true);

    try {
      const result = await aiService.chat(text, sessionId);
      const data = result.data;

      if (data.sessionId) setSessionId(data.sessionId);

      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          agentType: data.agentType || 'orchestrator',
          responseType: data.responseType || 'text',
          content: data.message,
          payload: data.payload,
        },
      ]);

      onActionConfirmed?.();
    } catch (err) {
      const msg = err.response?.data?.message || 'Error al comunicarse con el asistente';
      setMessages((prev) => [...prev, { role: 'assistant', agentType: 'orchestrator', responseType: 'text', content: `❌ ${msg}` }]);
      toast.error(msg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSend = () => sendMessage(input.trim());
  const handleKeyDown = (e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); } };

  return (
    <div className="card flex flex-col h-[640px] p-0">
      {/* Header */}
      <div className="flex items-center gap-3 border-b border-gray-200 px-4 py-3">
        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-100">
          <Bot className="h-4 w-4 text-primary-600" />
        </div>
        <div>
          <h3 className="text-sm font-semibold text-gray-900">Asistente IA</h3>
          <p className="text-xs text-gray-500">Inventario · Domicilios · General</p>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-hide">
        {messages.map((msg, idx) => {
          const agent = AGENT_LABELS[msg.agentType] || AGENT_LABELS.orchestrator;
          const AgentIcon = agent.icon;
          return (
            <div key={idx} className={`flex gap-3 ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}>
              {msg.role === 'assistant' && (
                <div className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full ${agent.bg}`}>
                  <AgentIcon className={`h-3.5 w-3.5 ${agent.color}`} />
                </div>
              )}
              <div className={`max-w-[82%] ${msg.role === 'user' ? '' : 'space-y-1'}`}>
                {msg.role === 'assistant' && msg.agentType && msg.agentType !== 'orchestrator' && (
                  <p className={`text-[10px] font-semibold uppercase tracking-wide ${agent.color}`}>
                    {agent.label}
                  </p>
                )}
                <div className={`rounded-lg px-4 py-2.5 text-sm whitespace-pre-line ${
                  msg.role === 'user' ? 'bg-primary-500 text-white' : 'bg-gray-100 text-gray-900'
                }`}>
                  {msg.content}
                  {msg.responseType === 'checklist' && msg.payload && (
                    <ChecklistMessage payload={msg.payload} onSend={sendMessage} />
                  )}
                  {msg.responseType === 'whatsapp' && msg.payload && (
                    <WhatsAppMessage payload={msg.payload} />
                  )}
                  {msg.responseType === 'delivery_status' && msg.payload && (
                    <DeliveryStatusMessage payload={msg.payload} />
                  )}
                </div>
              </div>
              {msg.role === 'user' && (
                <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gray-200">
                  <User className="h-3.5 w-3.5 text-gray-600" />
                </div>
              )}
            </div>
          );
        })}

        {isLoading && (
          <div className="flex gap-3">
            <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary-100">
              <Bot className="h-3.5 w-3.5 text-primary-600" />
            </div>
            <div className="flex items-center gap-2 rounded-lg bg-gray-100 px-4 py-2.5 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" />
              Pensando...
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="border-t border-gray-200 p-4">
        <div className="flex items-center gap-2">
          <button
            onClick={toggleListening}
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg transition-colors ${
              isListening ? 'animate-pulse bg-red-100 text-red-600' : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
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
            placeholder={isListening ? 'Escuchando...' : 'Escribe tu mensaje...'}
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
