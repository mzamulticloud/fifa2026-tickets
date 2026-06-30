// =============================================================================
// Story 2.5 / F5 — Componente de Chatbot (AC-7).
//
// UI mínima com shadcn/ui (Sheet lateral + Input + ScrollArea + Button). Conversa
// com a LLM atual (VITE_LLM_PROVIDER — Gemini default) via useLlmChat, que executa
// as tools MCP no McpServer através do gateway YARP (AC-8/AC-9).
//
// Mostra o provider ativo (demo de portabilidade — AC-10) e um aviso discreto se
// o proxy de LLM não estiver configurado (a key NUNCA vai no bundle — proxy.ts).
// =============================================================================

import React, { useRef, useState, useEffect } from 'react';
import { MessageCircle, Send, Bot, User, Loader2 } from 'lucide-react';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { useIsAuthenticated } from '@azure/msal-react';
import { useLlmChat } from '@/hooks/useLlmChat';
import { isLlmProxyConfigured } from '@/lib/llm';
import { isEntraConfigured } from '@/lib/authV2';

export const Chatbot: React.FC = () => {
  const isAuthenticated = useIsAuthenticated();
  const { messages, loading, provider, error, send } = useLlmChat();
  const [input, setInput] = useState('');
  const scrollRef = useRef<HTMLDivElement>(null);
  const proxyReady = isLlmProxyConfigured();

  // Auto-scroll para a última mensagem.
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, loading]);

  // UX: o assistente só aparece quando o usuário está logado via Entra. O gateway
  // exige token Entra válido em /llm e /mcp (F3) — sem login o chat sempre daria 401.
  // Esconder o botão (em vez de mostrar um chat que falha) evita essa confusão.
  // Hooks acima são sempre chamados (regras dos Hooks); o early-return vem depois.
  if (!isEntraConfigured() || !isAuthenticated) {
    return null;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const text = input;
    setInput('');
    await send(text);
  };

  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button
          size="icon"
          className="fixed bottom-6 right-6 z-50 h-14 w-14 rounded-full shadow-lg"
          title="Abrir assistente de ingressos"
          aria-label="Abrir assistente de ingressos"
        >
          <MessageCircle className="h-6 w-6" />
        </Button>
      </SheetTrigger>

      <SheetContent side="right" className="flex w-full flex-col sm:max-w-md">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <Bot className="h-5 w-5" />
            Assistente de Ingressos
            <Badge variant="secondary" className="ml-auto text-xs" title="Provider LLM ativo (AC-10)">
              {provider}
            </Badge>
          </SheetTitle>
        </SheetHeader>

        {!proxyReady && (
          <p className="rounded-md bg-muted p-3 text-xs text-muted-foreground">
            Proxy de LLM não configurado (VITE_LLM_PROXY_URL). A API key da LLM nunca vai no
            navegador — configure o proxy server-side para habilitar o chat.
          </p>
        )}

        <div ref={scrollRef} className="flex-1 overflow-y-auto pr-3">
          <div className="flex flex-col gap-3 py-2">
            {messages.length === 0 && (
              <p className="text-sm text-muted-foreground">
                Pergunte sobre ingressos. Ex.: "Tem ingresso para Brasil x Argentina?",
                "Esse ingresso ID 123 é válido?", "Quem está nas oitavas?".
              </p>
            )}

            {messages.map((m, i) => (
              <div
                key={i}
                className={`flex gap-2 ${m.role === 'user' ? 'flex-row-reverse' : 'flex-row'}`}
              >
                <div className="mt-1 shrink-0">
                  {m.role === 'user' ? (
                    <User className="h-4 w-4 text-primary" />
                  ) : (
                    <Bot className="h-4 w-4 text-muted-foreground" />
                  )}
                </div>
                <div
                  className={`max-w-[80%] whitespace-pre-wrap rounded-lg px-3 py-2 text-sm ${
                    m.role === 'user'
                      ? 'bg-primary text-primary-foreground'
                      : 'bg-muted text-foreground'
                  }`}
                >
                  {m.content}
                </div>
              </div>
            ))}

            {loading && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Consultando...
              </div>
            )}

            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>
        </div>

        <form onSubmit={handleSubmit} className="flex items-center gap-2 pt-2">
          <Input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Digite sua pergunta..."
            disabled={loading}
            aria-label="Mensagem para o assistente"
          />
          <Button type="submit" size="icon" disabled={loading || !input.trim()}>
            <Send className="h-4 w-4" />
          </Button>
        </form>
      </SheetContent>
    </Sheet>
  );
};
