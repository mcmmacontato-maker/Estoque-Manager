<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { auth, definirAoExpirar } from './api'
import Icon from './components/Icon.vue'
import Login from './components/Login.vue'
import Painel from './views/Painel.vue'
import Produtos from './views/Produtos.vue'
import Movimentacoes from './views/Movimentacoes.vue'

const paginas = {
  painel: { titulo: 'Painel', icone: 'grid' },
  produtos: { titulo: 'Produtos', icone: 'box' },
  movimentacoes: { titulo: 'Movimentações', icone: 'arrows' }
}

const usuario = ref(null)
const verificando = ref(true)
const demo = ref({ ativo: false })
const toast = ref(null)
const isAdmin = computed(() => usuario.value?.perfil === 'Admin')
const iniciais = computed(() =>
  (usuario.value?.nome ?? '').split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]).join('').toUpperCase())

// ---------- Roteamento por hash (#/painel, #/produtos, #/movimentacoes?produto=3) ----------
function lerRota() {
  const [caminho, query = ''] = location.hash.replace(/^#\/?/, '').split('?')
  const pagina = paginas[caminho] ? caminho : 'painel'
  return { pagina, params: Object.fromEntries(new URLSearchParams(query)), chave: location.hash || '#/painel' }
}
const rota = ref(lerRota())
const atualizarRota = () => { rota.value = lerRota(); window.scrollTo(0, 0) }
onMounted(() => window.addEventListener('hashchange', atualizarRota))
onUnmounted(() => window.removeEventListener('hashchange', atualizarRota))

const produtoInicial = computed(() => {
  const n = Number.parseInt(rota.value.params.produto, 10)
  return Number.isInteger(n) && n > 0 ? n : null
})

// ---------- Sessão ----------
function aviso(msg) {
  toast.value = msg
  clearTimeout(aviso.t)
  aviso.t = setTimeout(() => (toast.value = null), 3200)
}

function encerrarSessao() { usuario.value = null }

function entrou(u) {
  usuario.value = u
  location.hash = '#/painel'
}

async function sair() {
  try { await auth.logout() } finally { encerrarSessao() }
}

definirAoExpirar(() => {
  if (usuario.value) aviso('Sua sessão expirou. Entre novamente.')
  encerrarSessao()
})

onMounted(async () => {
  const [u, d] = await Promise.all([auth.me(), auth.demoInfo()])
  usuario.value = u
  demo.value = d
  verificando.value = false
})
</script>

<template>
  <div v-if="verificando" class="boot" aria-busy="true" />
  <Login v-else-if="!usuario" @entrou="entrou" />
  <div v-else class="layout">
    <aside class="sidebar">
      <div class="brand">
        <div class="logo"><Icon name="box" :size="16" /></div>
        <span>Estoque</span>
      </div>
      <nav aria-label="Navegação principal">
        <a v-for="(p, id) in paginas" :key="id" :id="`nav-${id}`" :href="`#/${id}`"
           :class="['nav-item', { active: rota.pagina === id }]" :aria-current="rota.pagina === id ? 'page' : null">
          <Icon :name="p.icone" />{{ p.titulo }}
        </a>
      </nav>
      <div class="sidebar-rodape">
        <span class="perfil-tag">{{ isAdmin ? 'Acesso total' : 'Acesso de operador' }}</span>
      </div>
    </aside>

    <div class="content">
      <header class="topbar">
        <ol class="breadcrumb">
          <li>Inventário</li>
          <li><Icon name="chevron" :size="14" /></li>
          <li aria-current="page">{{ paginas[rota.pagina].titulo }}</li>
          <li v-if="demo.ativo" class="demo-pill" :title="demo.resetMinutos ? `Dados restaurados a cada ${demo.resetMinutos} min` : 'Dados fictícios'">Demonstração</li>
        </ol>
        <div class="user">
          <div class="avatar">{{ iniciais }}</div>
          <div class="user-info">
            <span class="user-nome">{{ usuario.nome }}</span>
            <span class="user-perfil">{{ usuario.perfil }}</span>
          </div>
          <button id="btn-sair" class="btn btn-secondary btn-sm" @click="sair">
            <Icon name="logout" :size="14" />{{ demo.ativo ? 'Trocar perfil' : 'Sair' }}
          </button>
        </div>
      </header>

      <!-- Navegação mobile (sidebar oculta em telas pequenas) -->
      <nav class="tabs-mobile" aria-label="Navegação">
        <a v-for="(p, id) in paginas" :key="id" :href="`#/${id}`" :class="{ active: rota.pagina === id }">{{ p.titulo }}</a>
      </nav>

      <Painel v-if="rota.pagina === 'painel'" :key="rota.chave" @aviso="aviso" />
      <Produtos v-else-if="rota.pagina === 'produtos'" :key="rota.chave" :is-admin="isAdmin" @aviso="aviso" />
      <Movimentacoes v-else :key="rota.chave" :produto-inicial="produtoInicial" @aviso="aviso" />
    </div>

    <Transition name="toast">
      <div v-if="toast" class="toast" role="status">{{ toast }}</div>
    </Transition>
  </div>
</template>

<style scoped>
.layout { display: grid; grid-template-columns: 232px 1fr; min-height: 100vh; }
.boot { min-height: 100vh; background: var(--bg); }

/* Sidebar */
.sidebar {
  position: sticky; top: 0; height: 100vh; display: flex; flex-direction: column;
  background: var(--surface); border-right: 1px solid var(--border); padding: 16px 12px;
}
.brand { display: flex; align-items: center; gap: 10px; padding: 4px 8px 20px; font-weight: 600; font-size: 15px; }
.logo { display: grid; place-items: center; width: 28px; height: 28px; border-radius: 6px; background: var(--text); color: #fff; }
nav { display: grid; gap: 2px; }
.nav-item {
  display: flex; align-items: center; gap: 10px; height: 34px; padding: 0 10px;
  border-radius: var(--radius-sm); color: var(--text-2); text-decoration: none; font-weight: 500;
  transition: background-color .15s, color .15s;
}
.nav-item:hover { background: var(--surface-2); color: var(--text); }
.nav-item.active { background: var(--primary-soft); color: var(--primary); }
.sidebar-rodape { margin-top: auto; padding: 12px 10px 4px; border-top: 1px solid var(--border); }
.perfil-tag { font-size: 12px; color: var(--text-3); }

/* Topbar */
.content { min-width: 0; }
.topbar {
  height: 56px; display: flex; align-items: center; justify-content: space-between;
  padding: 0 32px; background: var(--surface); border-bottom: 1px solid var(--border);
}
.breadcrumb { display: flex; align-items: center; gap: 6px; list-style: none; color: var(--text-3); }
.breadcrumb li { display: flex; }
.breadcrumb [aria-current] { color: var(--text); font-weight: 500; }
.demo-pill { margin-left: 8px; padding: 1px 8px; border-radius: 999px; background: var(--warning-soft); color: var(--warning); font-size: 12px; font-weight: 500; }
.user { display: flex; align-items: center; gap: 10px; }
.user-info { display: grid; line-height: 1.2; }
.user-nome { font-weight: 500; color: var(--text); }
.user-perfil { font-size: 12px; color: var(--text-3); }
.btn-sm { height: 32px; padding: 0 12px; font-size: 13px; margin-left: 6px; }
.avatar { width: 30px; height: 30px; border-radius: 50%; display: grid; place-items: center; background: var(--surface-2); border: 1px solid var(--border); font-size: 12px; font-weight: 600; color: var(--text-2); }

.tabs-mobile { display: none; }

.toast { position: fixed; right: 24px; bottom: 24px; z-index: 60; background: var(--text); color: #fff; padding: 10px 16px; border-radius: var(--radius-sm); box-shadow: var(--shadow-lg); font-weight: 500; }

@media (max-width: 1024px) {
  .layout { grid-template-columns: 1fr; }
  .sidebar { display: none; }
  .topbar { padding-inline: 20px; }
  .tabs-mobile {
    display: flex; gap: 4px; padding: 8px 20px 0; background: var(--surface); border-bottom: 1px solid var(--border); overflow-x: auto;
  }
  .tabs-mobile a { padding: 8px 12px; color: var(--text-2); text-decoration: none; font-weight: 500; border-bottom: 2px solid transparent; white-space: nowrap; }
  .tabs-mobile a.active { color: var(--primary); border-bottom-color: var(--primary); }
}
@media (max-width: 560px) {
  .user-info, .breadcrumb li:not([aria-current]) { display: none; }
}
</style>
