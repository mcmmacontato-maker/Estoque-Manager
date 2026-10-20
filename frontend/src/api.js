const BASE = '/api/produtos'

/** Extrai uma mensagem legível de respostas ProblemDetails / ValidationProblem da API. */
async function erroDe(res) {
  try {
    const p = await res.json()
    if (p.errors) return Object.values(p.errors).flat().join(' ')
    if (p.detail || p.title) return p.detail || p.title
  } catch { /* corpo não-JSON */ }
  if (res.status === 429) return 'Muitas requisições. Aguarde alguns segundos.'
  return `Erro ${res.status}`
}

async function req(url, opts = {}) {
  const res = await fetch(url, { headers: { 'Content-Type': 'application/json' }, ...opts })
  if (!res.ok) throw new Error(await erroDe(res))
  return res.status === 204 ? null : res.json()
}

const id = (v) => encodeURIComponent(Number.parseInt(v, 10))

export const api = {
  listar: (busca = '') => req(`${BASE}?busca=${encodeURIComponent(busca.slice(0, 100))}`),
  criar: (p) => req(BASE, { method: 'POST', body: JSON.stringify(p) }),
  atualizar: (pid, p) => req(`${BASE}/${id(pid)}`, { method: 'PUT', body: JSON.stringify(p) }),
  movimentar: (pid, delta) => req(`${BASE}/${id(pid)}/movimentar?delta=${Number.parseInt(delta, 10)}`, { method: 'PATCH' }),
  remover: (pid) => req(`${BASE}/${id(pid)}`, { method: 'DELETE' })
}
