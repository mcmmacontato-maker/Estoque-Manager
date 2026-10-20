<script setup>
import { ref, computed, onMounted } from 'vue'
import { api } from '../api'
import { brl, int, data, status } from '../format'
import Icon from '../components/Icon.vue'
import ProdutoForm from '../components/ProdutoForm.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'

const props = defineProps({ isAdmin: Boolean })
const emit = defineEmits(['aviso'])

const produtos = ref([])
const busca = ref('')
const filtro = ref('todos')
const carregando = ref(true)
const editando = ref(null)
const modalAberto = ref(false)
const excluindo = ref(null)

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
  filtro.value === 'todos' ? produtos.value : produtos.value.filter((p) => status(p).id === filtro.value))

let timer
function buscar() {
  clearTimeout(timer)
  timer = setTimeout(carregar, 250)
}

async function carregar() {
  try { produtos.value = await api.listar(busca.value) }
  catch (e) { emit('aviso', e.message) }
  finally { carregando.value = false }
}

function abrir(p = null) { editando.value = p; modalAberto.value = true }

async function salvar(dados) {
  try {
    if (dados.id) await api.atualizar(dados.id, dados)
    else await api.criar(dados)
    modalAberto.value = false
    emit('aviso', dados.id ? 'Produto atualizado.' : 'Produto cadastrado.')
    carregar()
  } catch (e) { emit('aviso', e.message) }
}

async function mover(p, delta) {
  try { Object.assign(p, await api.movimentar(p.id, delta)) }
  catch (e) { emit('aviso', e.message) }
}

async function confirmarExclusao() {
  const p = excluindo.value
  excluindo.value = null
  try { await api.remover(p.id); emit('aviso', 'Produto excluído.'); carregar() }
  catch (e) { emit('aviso', e.message) }
}

onMounted(carregar)
</script>

<template>
  <main class="page">
    <div class="page-header">
      <div>
        <h1>Produtos</h1>
        <p>Gerencie o catálogo e os níveis de estoque.</p>
      </div>
      <button v-if="props.isAdmin" id="btn-novo" class="btn btn-primary" @click="abrir()">
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
        <table class="table produtos">
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
            <tr v-for="i in 5" :key="i" class="skeleton">
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
                  <a :id="`btn-historico-${p.id}`" class="icon-btn" :href="`#/movimentacoes?produto=${p.id}`" aria-label="Ver histórico" title="Ver histórico"><Icon name="arrows" /></a>
                  <template v-if="props.isAdmin">
                    <button :id="`btn-editar-${p.id}`" class="icon-btn" @click="abrir(p)" aria-label="Editar" title="Editar"><Icon name="edit" /></button>
                    <button :id="`btn-excluir-${p.id}`" class="icon-btn danger" @click="excluindo = p" aria-label="Excluir" title="Excluir"><Icon name="trash" /></button>
                  </template>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <div v-if="!carregando && !visiveis.length" class="empty">
          <Icon name="inbox" :size="28" />
          <strong>Nenhum produto encontrado</strong>
          <p>Ajuste a busca ou os filtros{{ props.isAdmin ? ', ou cadastre um novo produto' : '' }}.</p>
        </div>
      </div>

      <footer class="table-footer">
        Exibindo <strong class="num">{{ visiveis.length }}</strong> de <span class="num">{{ produtos.length }}</span> produtos
      </footer>
    </section>

    <Transition name="modal">
      <ProdutoForm v-if="modalAberto" :produto="editando" @salvar="salvar" @fechar="modalAberto = false" />
    </Transition>
    <Transition name="modal">
      <ConfirmDialog v-if="excluindo" titulo="Excluir produto"
                     :mensagem="`O produto “${excluindo.nome}” será removido permanentemente. O histórico de movimentações é mantido.`"
                     @confirmar="confirmarExclusao" @fechar="excluindo = null" />
    </Transition>
  </main>
</template>

<style scoped>
.produtos { min-width: 860px; }
.produtos td { height: 56px; }
.prod { display: grid; }
.prod-nome { font-weight: 500; }
.prod-cat { font-size: 13px; color: var(--text-3); }

.stepper { display: inline-flex; align-items: center; border: 1px solid var(--border-strong); border-radius: var(--radius-sm); overflow: hidden; }
.stepper button { width: 28px; height: 28px; display: grid; place-items: center; border: none; background: var(--surface); color: var(--text-2); }
.stepper button:hover:not(:disabled) { background: var(--surface-2); color: var(--text); }
.stepper button:disabled { color: var(--border-strong); cursor: not-allowed; }
.stepper span { min-width: 40px; text-align: center; font-weight: 500; border-inline: 1px solid var(--border-strong); line-height: 28px; }

.row-actions { display: inline-flex; gap: 2px; }
</style>
