export const brl = (v) => Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
export const brlCurto = (v) => Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', notation: 'compact', maximumFractionDigits: 1 })
export const int = (v) => Number(v).toLocaleString('pt-BR')
export const data = (v) => new Date(v).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })
export const dataHora = (v) => new Date(v).toLocaleString('pt-BR', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
export const diaCurto = (v) => new Date(v).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', timeZone: 'UTC' })
export const sinal = (n) => (n > 0 ? `+${int(n)}` : int(n))

/** Tempo relativo: "há 5 min", "há 3 h", "ontem", ou a data. */
export function relativo(v) {
  const s = (Date.now() - new Date(v).getTime()) / 1000
  if (s < 60) return 'agora'
  if (s < 3600) return `há ${Math.floor(s / 60)} min`
  if (s < 86400) return `há ${Math.floor(s / 3600)} h`
  if (s < 172800) return 'ontem'
  return data(v)
}

export function status(p) {
  if (p.quantidade === 0) return { id: 'esgotado', txt: 'Esgotado' }
  if (p.quantidade <= p.estoqueMinimo) return { id: 'baixo', txt: 'Estoque baixo' }
  return { id: 'ok', txt: 'Em estoque' }
}

/** Classe visual para cada tipo de movimentação. */
export const classeTipo = { 'Entrada': 'ok', 'Cadastro': 'info', 'Saída': 'esgotado', 'Ajuste': 'baixo' }
