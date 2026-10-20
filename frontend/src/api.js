const BASE = '/api/produtos'

async function req(url, opts = {}) {
  const res = await fetch(url, { headers: { 'Content-Type': 'application/json' }, ...opts })
  if (!res.ok) throw new Error((await res.text()) || `Erro ${res.status}`)
  return res.status === 204 ? null : res.json()
}

export const api = {
  listar: (busca = '') => req(`${BASE}?busca=${encodeURIComponent(busca)}`),
  criar: (p) => req(BASE, { method: 'POST', body: JSON.stringify(p) }),
  atualizar: (id, p) => req(`${BASE}/${id}`, { method: 'PUT', body: JSON.stringify(p) }),
  movimentar: (id, delta) => req(`${BASE}/${id}/movimentar?delta=${delta}`, { method: 'PATCH' }),
  remover: (id) => req(`${BASE}/${id}`, { method: 'DELETE' })
}
