<script setup>
import { ref, computed, onMounted } from 'vue'
import { api } from './api'
import Icon from './components/Icon.vue'
import ProdutoForm from './components/ProdutoForm.vue'
import ConfirmDialog from './components/ConfirmDialog.vue'

const produtos = ref([])
const busca = ref('')
const filtro = ref('todos')
const carregando = ref(true)
const editando = ref(null)
const modalAberto = ref(false)
const excluindo = ref(null)
const toast = ref(null)

const brl = (v) => Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const int = (v) => Number(v).toLocaleString('pt-BR')
const data = (v) => new Date(v).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })

function status(p) {
  if (p.quantidade === 0) return { id: 'esgotado', txt: 'Esgotado' }
  if (p.quantidade <= p.estoqueMinimo) return { id: 'baixo', txt: 'Estoque baixo' }
  return { id: 'ok', txt: 'Em estoque' }
}

const stats = computed(() => {
  const l = produtos.value
  return {
    total: l.length,
    unidades: l.reduce((s, p) => s + p.quantidade, 0),
    valor: l.reduce((s, p) => s + p.preco * p.quantidade, 0),
    baixo: l.filter((p) => status(p).id === 'baixo').length,
    esgotado: l.filter((p) => status(p).id === 'esgotado').length
  }
})

const filtros = computed(() => [
  { id: 'todos', txt: 'Todos', n: stats.value.total },
  { id: 'baixo', txt: 'Estoque baixo', n: stats.value.baixo },
  { id: 'esgotado', txt: 'Esgotados', n: stats.value.esgotado }
])

const visiveis = computed(() =>
  filtro.value === 'todos' ? produtos.value : produtos.value.filter((p) => status(p).id === filtro.value)
)

function aviso(msg) {
  toast.value = msg
  clearTimeout(aviso.t)
  aviso.t = setTimeout(() => (toast.value = null), 3000)
}

let timer
function buscar() {
  clearTimeout(timer)
  timer = setTimeout(carregar, 250)
}

async function carregar() {
  try { produtos.value = await api.listar(busca.value) }
  catch { aviso('Não foi possível conectar à API.') }
  finally { carregando.value = false }
}

function abrir(p = null) { editando.value = p; modalAberto.value = true }

async function salvar(dados) {
  try {
    if (dados.id) await api.atualizar(dados.id, dados)
    else await api.criar(dados)
    modalAberto.value = false
    aviso(dados.id ? 'Produto atualizado.' : 'Produto cadastrado.')
    carregar()
  } catch (e) { aviso(e.message) }
}

async function mover(p, delta) {
  try { Object.assign(p, await api.movimentar(p.id, delta)) }
  catch (e) { aviso(e.message) }
}

async function confirmarExclusao() {
  const p = excluindo.value
  excluindo.value = null
  try { await api.remover(p.id); aviso('Produto excluído.'); carregar() }
  catch (e) { aviso(e.message) }
}

onMounted(carregar)
</script>

<template>
  <div class="layout">
    <aside class="sidebar">
      <div class="brand">
        <div class="logo"><Icon name="box" :size="16" /></div>
        <span>Estoque</span>
      </div>
      <nav aria-label="Navegação principal">
        <a href="#" class="nav-item"><Icon name="grid" />Painel</a>
        <a href="#" class="nav-item active" aria-current="page"><Icon name="box" />Produtos</a>
        <a href="#" class="nav-item"><Icon name="arrows" />Movimentações</a>
      </nav>
      <nav class="nav-bottom">
        <a href="#" class="nav-item"><Icon name="settings" />Configurações</a>
      </nav>
    </aside>

    <div class="content">
      <header class="topbar">
        <ol class="breadcrumb">
          <li>Inventário</li>
          <li><Icon name="chevron" :size="14" /></li>
          <li aria-current="page">Produtos</li>
        </ol>
        <div class="user">
          <div class="avatar">MA</div>
          <span>Maria Andrade</span>
        </div>
      </header>

      <main class="page">
        <div class="page-header">
          <div>
            <h1>Produtos</h1>
            <p>Gerencie o catálogo e os níveis de estoque.</p>
          </div>
          <button id="btn-novo" class="btn btn-primary" @click="abrir()">
            <Icon name="plus" />Novo produto
          </button>
        </div>

        <section class="kpis" aria-label="Indicadores">
          <div class="kpi"><span>Produtos cadastrados</span><strong class="num">{{ int(stats.total) }}</strong></div>
          <div class="kpi"><span>Unidades em estoque</span><strong class="num">{{ int(stats.unidades) }}</strong></div>
          <div class="kpi"><span>Valor do estoque</span><strong class="num">{{ brl(stats.valor) }}</strong></div>
          <div class="kpi">
            <span>Requer reposição</span>
            <strong class="num">{{ stats.baixo + stats.esgotado }}</strong>
            <small>{{ stats.esgotado }} esgotado(s) · {{ stats.baixo }} abaixo do mínimo</small>
          </div>
        </section>

        <section class="card">
          <div class="toolbar">
            <div class="search">
              <Icon name="search" />
              <input id="input-busca" class="input" v-model="busca" @input="buscar"
                     placeholder="Buscar por nome ou categoria" aria-label="Buscar produtos" />
            </div>
            <div class="segmented" role="tablist">
              <button v-for="f in filtros" :key="f.id" :id="`filtro-${f.id}`" role="tab"
                      :aria-selected="filtro === f.id" :class="{ active: filtro === f.id }" @click="filtro = f.id">
                {{ f.txt }} <span class="count num">{{ f.n }}</span>
              </button>
            </div>
          </div>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Produto</th>
                  <th>Status</th>
                  <th class="right">Preço</th>
                  <th class="center">Quantidade</th>
                  <th class="right">Mínimo</th>
                  <th>Atualizado</th>
                  <th class="right"><span class="sr-only">Ações</span></th>
                </tr>
              </thead>
              <tbody v-if="carregando">
                <tr v-for="i in 4" :key="i" class="skeleton">
                  <td v-for="c in 7" :key="c"><span /></td>
                </tr>
              </tbody>
              <tbody v-else-if="visiveis.length">
                <tr v-for="p in visiveis" :key="p.id">
                  <td>
                    <div class="prod">
                      <span class="prod-nome">{{ p.nome }}</span>
                      <span class="prod-cat">{{ p.categoria }}</span>
                    </div>
                  </td>
                  <td><span class="badge" :class="status(p).id">{{ status(p).txt }}</span></td>
                  <td class="right num">{{ brl(p.preco) }}</td>
                  <td class="center">
                    <div class="stepper">
                      <button :id="`btn-menos-${p.id}`" :disabled="!p.quantidade" @click="mover(p, -1)" aria-label="Remover uma unidade"><Icon name="minus" :size="14" /></button>
                      <span class="num">{{ p.quantidade }}</span>
                      <button :id="`btn-mais-${p.id}`" @click="mover(p, 1)" aria-label="Adicionar uma unidade"><Icon name="plus" :size="14" /></button>
                    </div>
                  </td>
                  <td class="right num muted">{{ p.estoqueMinimo }}</td>
                  <td class="muted">{{ data(p.atualizadoEm) }}</td>
                  <td class="right">
                    <div class="row-actions">
                      <button :id="`btn-editar-${p.id}`" class="icon-btn" @click="abrir(p)" aria-label="Editar"><Icon name="edit" /></button>
                      <button :id="`btn-excluir-${p.id}`" class="icon-btn danger" @click="excluindo = p" aria-label="Excluir"><Icon name="trash" /></button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>

            <div v-if="!carregando && !visiveis.length" class="empty">
              <Icon name="inbox" :size="28" />
              <strong>Nenhum produto encontrado</strong>
              <p>Ajuste a busca ou os filtros, ou cadastre um novo produto.</p>
            </div>
          </div>

          <footer class="table-footer">
            Exibindo <strong class="num">{{ visiveis.length }}</strong> de <span class="num">{{ produtos.length }}</span> produtos
          </footer>
        </section>
      </main>
    </div>

    <Transition name="modal">
      <ProdutoForm v-if="modalAberto" :produto="editando" @salvar="salvar" @fechar="modalAberto = false" />
    </Transition>
    <Transition name="modal">
      <ConfirmDialog v-if="excluindo" titulo="Excluir produto"
                     :mensagem="`O produto “${excluindo.nome}” será removido permanentemente. Esta ação não pode ser desfeita.`"
                     @confirmar="confirmarExclusao" @fechar="excluindo = null" />
    </Transition>
    <Transition name="toast">
      <div v-if="toast" class="toast" role="status">{{ toast }}</div>
    </Transition>
  </div>
</template>

<style scoped>
.layout { display: grid; grid-template-columns: 232px 1fr; min-height: 100vh; }

/* Sidebar */
.sidebar {
  position: sticky; top: 0; height: 100vh; display: flex; flex-direction: column;
  background: var(--surface); border-right: 1px solid var(--border); padding: 16px 12px;
}
.brand { display: flex; align-items: center; gap: 10px; padding: 4px 8px 20px; font-weight: 600; font-size: 15px; }
.logo { display: grid; place-items: center; width: 28px; height: 28px; border-radius: 6px; background: var(--text); color: #fff; }
nav { display: grid; gap: 2px; }
.nav-bottom { margin-top: auto; }
.nav-item {
  display: flex; align-items: center; gap: 10px; height: 34px; padding: 0 10px;
  border-radius: var(--radius-sm); color: var(--text-2); text-decoration: none; font-weight: 500;
  transition: background-color .15s, color .15s;
}
.nav-item:hover { background: var(--surface-2); color: var(--text); }
.nav-item.active { background: var(--primary-soft); color: var(--primary); }

/* Topbar */
.content { min-width: 0; }
.topbar {
  height: 56px; display: flex; align-items: center; justify-content: space-between;
  padding: 0 32px; background: var(--surface); border-bottom: 1px solid var(--border);
}
.breadcrumb { display: flex; align-items: center; gap: 6px; list-style: none; color: var(--text-3); }
.breadcrumb li { display: flex; }
.breadcrumb [aria-current] { color: var(--text); font-weight: 500; }
.user { display: flex; align-items: center; gap: 10px; color: var(--text-2); font-weight: 500; }
.avatar { width: 30px; height: 30px; border-radius: 50%; display: grid; place-items: center; background: var(--surface-2); border: 1px solid var(--border); font-size: 12px; font-weight: 600; color: var(--text-2); }

/* Página */
.page { padding: 28px 32px 48px; max-width: 1280px; }
.page-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; margin-bottom: 24px; }
h1 { font-size: 24px; font-weight: 600; letter-spacing: -.01em; }
.page-header p { color: var(--text-2); margin-top: 4px; }

.kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 24px; }
.kpi { background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius); padding: 16px 20px; display: grid; gap: 6px; align-content: start; }
.kpi span { color: var(--text-2); font-size: 13px; font-weight: 500; }
.kpi strong { font-size: 24px; font-weight: 600; letter-spacing: -.01em; }
.kpi small { color: var(--text-3); font-size: 12px; }

/* Card + toolbar */
.card { background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius); overflow: hidden; }
.toolbar { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.search { position: relative; width: min(340px, 100%); color: var(--text-3); }
.search svg { position: absolute; left: 11px; top: 50%; transform: translateY(-50%); }
.search .input { padding-left: 34px; height: 36px; }
.segmented { display: inline-flex; background: var(--surface-2); border-radius: var(--radius-sm); padding: 3px; gap: 2px; }
.segmented button {
  height: 30px; padding: 0 12px; border: none; background: transparent; border-radius: 4px;
  color: var(--text-2); font-weight: 500; font-size: 13px; display: flex; align-items: center; gap: 6px;
}
.segmented button:hover { color: var(--text); }
.segmented button.active { background: var(--surface); color: var(--text); box-shadow: 0 1px 2px rgba(16,24,40,.08); }
.count { font-size: 12px; color: var(--text-3); }

/* Tabela */
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; min-width: 820px; }
th {
  text-align: left; font-size: 12px; font-weight: 500; color: var(--text-2);
  text-transform: uppercase; letter-spacing: .04em; padding: 10px 16px;
  background: #FCFCFD; border-bottom: 1px solid var(--border);
}
td { padding: 0 16px; height: 56px; border-bottom: 1px solid var(--border); }
tbody tr:last-child td { border-bottom: none; }
tbody tr:hover td { background: #FCFCFD; }
.right { text-align: right; }
.center { text-align: center; }
.muted { color: var(--text-2); }
.prod { display: grid; }
.prod-nome { font-weight: 500; }
.prod-cat { font-size: 13px; color: var(--text-3); }

.badge { display: inline-flex; align-items: center; gap: 6px; height: 22px; padding: 0 8px; border-radius: 999px; font-size: 12px; font-weight: 500; }
.badge::before { content: ''; width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
.badge.ok { background: var(--success-soft); color: var(--success); }
.badge.baixo { background: var(--warning-soft); color: var(--warning); }
.badge.esgotado { background: var(--danger-soft); color: var(--danger); }

.stepper { display: inline-flex; align-items: center; border: 1px solid var(--border-strong); border-radius: var(--radius-sm); overflow: hidden; }
.stepper button { width: 28px; height: 28px; display: grid; place-items: center; border: none; background: var(--surface); color: var(--text-2); }
.stepper button:hover:not(:disabled) { background: var(--surface-2); color: var(--text); }
.stepper button:disabled { color: var(--border-strong); cursor: not-allowed; }
.stepper span { min-width: 40px; text-align: center; font-weight: 500; border-inline: 1px solid var(--border-strong); line-height: 28px; }

.row-actions { display: inline-flex; gap: 2px; }

.skeleton span { display: block; height: 12px; border-radius: 4px; background: var(--surface-2); animation: pulse 1.2s ease-in-out infinite; }
@keyframes pulse { 50% { opacity: .5; } }

.empty { display: grid; justify-items: center; gap: 6px; padding: 48px 16px; color: var(--text-3); text-align: center; }
.empty strong { color: var(--text); font-weight: 500; margin-top: 6px; }
.empty p { color: var(--text-2); }

.table-footer { padding: 12px 16px; border-top: 1px solid var(--border); color: var(--text-2); font-size: 13px; }
.table-footer strong { color: var(--text); font-weight: 500; }

.toast { position: fixed; right: 24px; bottom: 24px; z-index: 60; background: var(--text); color: #fff; padding: 10px 16px; border-radius: var(--radius-sm); box-shadow: var(--shadow-lg); font-weight: 500; }

.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }

@media (max-width: 1024px) {
  .layout { grid-template-columns: 1fr; }
  .sidebar { display: none; }
  .kpis { grid-template-columns: repeat(2, 1fr); }
  .topbar, .page { padding-inline: 20px; }
}
@media (max-width: 560px) {
  .kpis { grid-template-columns: 1fr; }
  .page-header { flex-direction: column; align-items: stretch; }
  .user span { display: none; }
}
</style>
