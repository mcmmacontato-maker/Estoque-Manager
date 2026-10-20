const BASE = '/api/produtos'

/** Chamado quando a sessão expira (401) para o app voltar à tela de login. */
let aoExpirar = () => {}
export const definirAoExpirar = (fn) => { aoExpirar = fn }

/** Extrai uma mensagem legível de respostas ProblemDetails / ValidationProblem da API. */
async function erroDe(res) {
  try {
    const p = await res.json()
    if (p.errors) return Object.values(p.errors).flat().join(' ')
    if (p.detail || p.title) return p.detail || p.title
  } catch { /* corpo não-JSON */ }
  if (res.status === 429) return 'Muitas requisições. Aguarde alguns segundos.'
  if (res.status === 403) return 'Você não tem permissão para esta ação.'
  return `Erro ${res.status}`
}

async function req(url, opts = {}, { tratar401 = true } = {}) {
  // same-origin: o cookie HttpOnly de sessão é enviado automaticamente pelo navegador
  const res = await fetch(url, { credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, ...opts })
  if (res.status === 401 && tratar401) {
    aoExpirar()
    throw new Error('Sua sessão expirou. Entre novamente.')
  }
  if (!res.ok) throw new Error(await erroDe(res))
  return res.status === 204 ? null : res.json()
}

const id = (v) => encodeURIComponent(Number.parseInt(v, 10))

export const auth = {
  login: (email, senha) => req('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, senha }) }, { tratar401: false }),
  logout: () => req('/api/auth/logout', { method: 'POST' }, { tratar401: false }),
  /** Retorna o usuário logado ou null (sem disparar o fluxo de sessão expirada). */
  me: () => req('/api/auth/me', {}, { tratar401: false }).catch(() => null)
}

export const api = {
  listar: (busca = '') => req(`${BASE}?busca=${encodeURIComponent(busca.slice(0, 100))}`),
  criar: (p) => req(BASE, { method: 'POST', body: JSON.stringify(p) }),
  atualizar: (pid, p) => req(`${BASE}/${id(pid)}`, { method: 'PUT', body: JSON.stringify(p) }),
  movimentar: (pid, delta) => req(`${BASE}/${id(pid)}/movimentar?delta=${Number.parseInt(delta, 10)}`, { method: 'PATCH' }),
  remover: (pid) => req(`${BASE}/${id(pid)}`, { method: 'DELETE' })
}
