import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { CreditCard, Lock, Check, ArrowLeft, ShoppingCart } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useCart } from '@/contexts/CartContext';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/hooks/use-toast';
import { TeamFlag } from '@/components/TeamFlag';
import { api } from '@/lib/api';
import {
  purchaseViaFunction,
  getPurchaseStatus,
  type PurchaseV2Category,
  type PurchaseV2Item,
} from '@/lib/apiFunctionV2';

// Oitavas de Final — toggle do fluxo de compra assíncrona (Function v2).
// A presença de VITE_FUNCTION_V2_URL é o próprio interruptor: definida → v2 async.
const FUNCTION_V2_URL = import.meta.env.VITE_FUNCTION_V2_URL ?? '';
const USE_FUNCTION_V2 = FUNCTION_V2_URL.length > 0;

// Polling do status v2.
const POLL_INTERVAL_MS = 2000;
const POLL_MAX_ATTEMPTS = 10;

/**
 * Mapeia o setor do carrinho para a categoria do contrato v2 ('VIP'|'Cat1'|'Cat2').
 * IMPORTANTE (defeito M-1): NÃO confiar no label (sector.name = 'VIP Premium' /
 * 'Categoria 1' / 'Categoria 2'), que diverge do seed. Usamos o `sector.id` estável
 * ('vip'|'cat1'|'cat2'), definido em data/stadiums.ts e lib/stadium-sectors.ts.
 */
function mapSectorToV2Category(sectorId: string): PurchaseV2Category | null {
  switch (sectorId) {
    case 'vip':
      return 'VIP';
    case 'cat1':
      return 'Cat1';
    case 'cat2':
      return 'Cat2';
    default:
      return null;
  }
}

const Checkout: React.FC = () => {
  const { items, totalPrice, clearCart } = useCart();
  const { user, isAuthenticated, addOrder } = useAuth();
  const { toast } = useToast();
  const navigate = useNavigate();

  const [cardNumber, setCardNumber] = useState('');
  const [cardName, setCardName] = useState('');
  const [expiry, setExpiry] = useState('');
  const [cvv, setCvv] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);

  // Oitavas de Final — feedback do fluxo assíncrono v2 (UX a/b).
  const [v2CorrelationId, setV2CorrelationId] = useState<string | null>(null);
  const [v2StatusMessage, setV2StatusMessage] = useState<string | null>(null);

  const serviceFee = totalPrice * 0.1;
  const grandTotal = totalPrice + serviceFee;

  if (!isAuthenticated) {
    navigate('/login?redirect=/checkout');
    return null;
  }

  if (items.length === 0) {
    return (
      <div className="min-h-screen py-12">
        <div className="container mx-auto px-4">
          <div className="max-w-md mx-auto text-center py-20">
            <div className="w-24 h-24 rounded-full bg-secondary flex items-center justify-center mx-auto mb-6">
              <ShoppingCart className="w-12 h-12 text-muted-foreground" />
            </div>
            <h1 className="font-display text-3xl mb-4">Carrinho Vazio</h1>
            <p className="text-muted-foreground mb-8">
              Adicione ingressos ao carrinho para continuar.
            </p>
            <Link to="/matches">
              <Button className="gold-gradient hover:opacity-90">
                Ver Jogos
              </Button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // ---------------------------------------------------------------------------
  // Oitavas de Final — fluxo de compra ASSÍNCRONO via Function v2 (CARRINHO INTEIRO).
  // Mapeia TODAS as linhas do carrinho → 1 POST atômico { userId, items[] }; a Function
  // explode em N mensagens (1 por linha). Exibe o orderId como protocolo (UX a) e faz
  // polling de TODOS os correlationIds em paralelo até todos completed/algum failed/timeout (UX b).
  // ---------------------------------------------------------------------------
  const handleSubmitV2 = async () => {
    setIsProcessing(true);
    setV2CorrelationId(null);
    setV2StatusMessage(null);

    const userId = Number(user?.id);
    if (!Number.isFinite(userId)) {
      toast({
        title: 'Não foi possível montar o pedido',
        description:
          'Dados do usuário incompletos. Recarregue e tente novamente.',
        variant: 'destructive',
      });
      setIsProcessing(false);
      return;
    }

    // Mapeia TODO o carrinho → items, validando categoria e matchId POR linha.
    const v2Items: PurchaseV2Item[] = [];
    for (const cartItem of items) {
      const category = mapSectorToV2Category(cartItem.sector.id);
      const matchId = Number(cartItem.match.id);
      const quantity = Number(cartItem.quantity);

      if (
        !category ||
        !Number.isFinite(matchId) ||
        !Number.isFinite(quantity)
      ) {
        toast({
          title: 'Não foi possível montar o pedido',
          description:
            'Dados do ingresso incompletos (categoria, jogo ou quantidade). Recarregue e tente novamente.',
          variant: 'destructive',
        });
        setIsProcessing(false);
        return;
      }

      v2Items.push({ matchId, category, quantity });
    }

    // 1) POST atômico do carrinho → 202 { orderId, correlationIds[] }
    const accepted = await purchaseViaFunction({ userId, items: v2Items });

    if (accepted.error || !accepted.data) {
      toast({
        title: 'Erro ao enviar o pedido',
        description: accepted.error ?? 'Resposta inesperada da Function v2.',
        variant: 'destructive',
      });
      setIsProcessing(false);
      return;
    }

    const { orderId, correlationIds } = accepted.data;
    if (!Array.isArray(correlationIds) || correlationIds.length === 0) {
      toast({
        title: 'Resposta inesperada',
        description: 'A Function v2 não retornou os protocolos do pedido.',
        variant: 'destructive',
      });
      setIsProcessing(false);
      return;
    }

    // Protocolo principal = orderId (pedido inteiro).
    setV2CorrelationId(orderId);
    setV2StatusMessage('Pedido recebido — em processamento.');
    toast({
      title: 'Pedido recebido',
      description: `Em processamento. Protocolo: ${orderId}`,
    });

    // 2) Polling: a cada tick consulta TODOS os correlationIds em paralelo.
    //    Confirma quando TODOS = completed; falha se algum = failed; timeout esgota tentativas.
    for (let attempt = 0; attempt < POLL_MAX_ATTEMPTS; attempt++) {
      await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));

      const results = await Promise.all(
        correlationIds.map((id) => getPurchaseStatus(id))
      );

      // Erro transitório em qualquer consulta: continua tentando até esgotar.
      if (results.some((r) => r.error || !r.data)) {
        setV2StatusMessage('Consultando status do pedido...');
        continue;
      }

      const statuses = results.map((r) => (r.data?.status ?? '').toLowerCase());

      if (statuses.some((s) => s === 'failed')) {
        setV2StatusMessage(null);
        toast({
          title: 'Compra não concluída',
          description: `O processamento de um ou mais ingressos falhou (protocolo ${orderId}).`,
          variant: 'destructive',
        });
        setIsProcessing(false);
        return;
      }

      if (statuses.every((s) => s === 'completed')) {
        const purchasedTickets = items.map((cartItem, index) => ({
          // Confirmação montada a partir do CARRINHO LOCAL + orderId.
          // O 202 não traz tickets[]/total_amount; NÃO fabricamos ticket fake.
          ticketId: `${orderId}-${index + 1}`,
          matchId: cartItem.match.id,
          homeTeam: cartItem.homeTeam.name,
          awayTeam: cartItem.awayTeam.name,
          homeFlag: cartItem.homeTeam.flag,
          awayFlag: cartItem.awayTeam.flag,
          stadium: cartItem.stadium.name,
          city: cartItem.stadium.city,
          date: cartItem.match.date,
          time: cartItem.match.time,
          sector: cartItem.sector.name,
          quantity: cartItem.quantity,
          buyerName: user?.name || '',
          buyerEmail: user?.email || '',
          purchaseDate: new Date(),
        }));

        addOrder({
          items: items.map((cartItem) => ({
            matchId: cartItem.match.id,
            sectorId: cartItem.sector.id,
            quantity: cartItem.quantity,
            unitPrice: cartItem.unitPrice,
            totalPrice: cartItem.totalPrice,
          })),
          totalPrice: grandTotal,
          status: 'confirmed',
          paymentMethod: 'credit_card',
        });

        clearCart();
        toast({
          title: 'Compra confirmada!',
          description: `Protocolo ${orderId}.`,
        });
        navigate('/payment-confirmation', {
          state: {
            tickets: purchasedTickets,
            totalAmount: grandTotal,
            correlationId: orderId,
          },
        });
        setIsProcessing(false);
        return;
      }

      // Alguma linha ainda em andamento (queued/processing/...): segue o polling.
      setV2StatusMessage('Processando seu pedido...');
    }

    // Timeout: não confirmou dentro das tentativas.
    toast({
      title: 'Processamento em andamento',
      description: `Ainda não foi possível confirmar (protocolo ${orderId}). Verifique "Meus Ingressos" em instantes.`,
      variant: 'destructive',
    });
    setV2StatusMessage(
      'O pedido ainda está em processamento. Confira "Meus Ingressos" em instantes.'
    );
    setIsProcessing(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Oitavas de Final — toggle: com VITE_FUNCTION_V2_URL definida, usa v2 async.
    if (USE_FUNCTION_V2) {
      await handleSubmitV2();
      return;
    }

    setIsProcessing(true);

    try {
      // Preparar items para a API do backend usando o ticketCategoryId do banco
      const purchaseItems = items.map(item => ({
        ticket_category_id: item.ticketCategoryId,
        quantity: item.quantity,
      }));

      // Chamar API do backend para registrar a compra
      const result = await api.purchaseTickets(purchaseItems);

      if (result.error) {
        toast({
          title: 'Erro na compra',
          description: result.error,
          variant: 'destructive',
        });
        setIsProcessing(false);
        return;
      }

      // Create tickets for confirmation page
      const purchasedTickets = items.map((item, index) => ({
        ticketId: `FIFA2026-${Date.now()}-${index + 1}`,
        matchId: item.match.id,
        homeTeam: item.homeTeam.name,
        awayTeam: item.awayTeam.name,
        homeFlag: item.homeTeam.flag,
        awayFlag: item.awayTeam.flag,
        stadium: item.stadium.name,
        city: item.stadium.city,
        date: item.match.date,
        time: item.match.time,
        sector: item.sector.name,
        quantity: item.quantity,
        buyerName: user?.name || '',
        buyerEmail: user?.email || '',
        purchaseDate: new Date(),
      }));

      // Create order locally for immediate display
      addOrder({
        items: items.map(item => ({
          matchId: item.match.id,
          sectorId: item.sector.id,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          totalPrice: item.totalPrice,
        })),
        totalPrice: grandTotal,
        status: 'confirmed',
        paymentMethod: 'credit_card',
      });

      clearCart();

      toast({
        title: 'Compra realizada!',
        description: 'Seus ingressos foram confirmados.',
      });

      // Navigate to confirmation page with tickets data
      navigate('/payment-confirmation', {
        state: {
          tickets: purchasedTickets,
          totalAmount: grandTotal,
        },
      });
    } catch (error) {
      toast({
        title: 'Erro',
        description: 'Erro ao processar a compra. Tente novamente.',
        variant: 'destructive',
      });
    } finally {
      setIsProcessing(false);
    }
  };

  const formatCardNumber = (value: string) => {
    return value.replace(/\s/g, '').replace(/(\d{4})/g, '$1 ').trim().slice(0, 19);
  };

  const formatExpiry = (value: string) => {
    return value.replace(/\D/g, '').replace(/(\d{2})(\d)/, '$1/$2').slice(0, 5);
  };

  return (
    <div className="min-h-screen py-12">
      <div className="container mx-auto px-4">
        {/* Back Button */}
        <Link to="/cart" className="inline-flex items-center text-muted-foreground hover:text-foreground mb-8">
          <ArrowLeft className="w-4 h-4 mr-2" />
          Voltar ao carrinho
        </Link>

        <h1 className="font-display text-4xl mb-8">
          <span className="gold-text">Finalizar</span> Compra
        </h1>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Payment Form */}
          <div className="lg:col-span-2">
            <form onSubmit={handleSubmit} className="space-y-8">
              {/* User Info */}
              <div className="rounded-2xl bg-card border border-border p-6">
                <h2 className="font-display text-xl mb-4">Dados do Comprador</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label>Nome</Label>
                    <Input value={user?.name} disabled className="bg-secondary/50" />
                  </div>
                  <div className="space-y-2">
                    <Label>Email</Label>
                    <Input value={user?.email} disabled className="bg-secondary/50" />
                  </div>
                </div>
              </div>

              {/* Payment */}
              <div className="rounded-2xl bg-card border border-border p-6">
                <div className="flex items-center gap-2 mb-6">
                  <CreditCard className="w-5 h-5 text-primary" />
                  <h2 className="font-display text-xl">Pagamento</h2>
                </div>

                <div className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="cardNumber">Número do Cartão</Label>
                    <Input
                      id="cardNumber"
                      placeholder="0000 0000 0000 0000"
                      value={cardNumber}
                      onChange={(e) => setCardNumber(formatCardNumber(e.target.value))}
                      required
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="cardName">Nome no Cartão</Label>
                    <Input
                      id="cardName"
                      placeholder="NOME COMPLETO"
                      value={cardName}
                      onChange={(e) => setCardName(e.target.value.toUpperCase())}
                      required
                    />
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-2">
                      <Label htmlFor="expiry">Validade</Label>
                      <Input
                        id="expiry"
                        placeholder="MM/AA"
                        value={expiry}
                        onChange={(e) => setExpiry(formatExpiry(e.target.value))}
                        required
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="cvv">CVV</Label>
                      <Input
                        id="cvv"
                        placeholder="000"
                        value={cvv}
                        onChange={(e) => setCvv(e.target.value.replace(/\D/g, '').slice(0, 4))}
                        required
                      />
                    </div>
                  </div>
                </div>

                <div className="flex items-center gap-2 mt-6 p-4 rounded-xl bg-success/10 text-success">
                  <Lock className="w-4 h-4" />
                  <span className="text-sm">Pagamento seguro com criptografia SSL</span>
                </div>
              </div>

              {/* Oitavas de Final — feedback do fluxo assíncrono v2 (protocolo + status). */}
              {USE_FUNCTION_V2 && v2StatusMessage && (
                <div className="p-4 rounded-xl bg-primary/10 text-foreground border border-primary/30">
                  <p className="text-sm font-medium">{v2StatusMessage}</p>
                  {v2CorrelationId && (
                    <p className="text-xs text-muted-foreground mt-1">
                      Protocolo: <span className="font-mono">{v2CorrelationId}</span>
                    </p>
                  )}
                </div>
              )}

              <Button
                type="submit"
                className="w-full gold-gradient hover:opacity-90 text-primary-foreground"
                size="lg"
                disabled={isProcessing}
              >
                {isProcessing ? (
                  <>
                    <span className="animate-spin mr-2">⚽</span>
                    Processando...
                  </>
                ) : (
                  <>
                    <Check className="w-5 h-5 mr-2" />
                    Confirmar Pagamento de ${grandTotal.toLocaleString()}
                  </>
                )}
              </Button>
            </form>
          </div>

          {/* Order Summary */}
          <div className="lg:col-span-1">
            <div className="sticky top-24 rounded-2xl bg-card border border-border p-6">
              <h2 className="font-display text-xl mb-6">Resumo do Pedido</h2>

              <div className="space-y-4 mb-6">
                {items.map((item) => (
                  <div key={item.id} className="flex gap-3 pb-4 border-b border-border">
                    <div className="flex-1">
                      <div className="flex items-center gap-1 text-sm mb-1">
                        <TeamFlag flag={item.homeTeam.flag} name={item.homeTeam.name} size="sm" />
                        <span>vs</span>
                        <TeamFlag flag={item.awayTeam.flag} name={item.awayTeam.name} size="sm" />
                      </div>
                      <span className="text-xs text-muted-foreground block">{item.sector.name}</span>
                      <span className="text-xs text-muted-foreground">{item.quantity}x ${item.unitPrice}</span>
                    </div>
                    <span className="font-medium">${item.totalPrice.toLocaleString()}</span>
                  </div>
                ))}
              </div>

              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Subtotal</span>
                  <span>${totalPrice.toLocaleString()}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Taxa de serviço (10%)</span>
                  <span>${serviceFee.toLocaleString()}</span>
                </div>
                <div className="border-t border-border pt-2 mt-2">
                  <div className="flex justify-between">
                    <span className="font-medium">Total</span>
                    <span className="font-display text-2xl gold-text">${grandTotal.toLocaleString()}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Checkout;