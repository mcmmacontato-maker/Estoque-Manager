<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { api } from '../api'
import { int, dataHora, relativo, sinal, classeTipo } from '../format'
import Icon from '../components/Icon.vue'

const props = defineProps({ produtoInicial: { type: Number, default: null } })
const emit = defineEmits(['aviso'])

const produtos = ref([])
const movs = ref([])
const carregando = ref(true)
const filtros = reactive({ produtoId: props.produtoInicial ?? '', tipo: '', dias: 30 })
const form = reactive({ produtoId: props.produtoInicial ?? '', tipo: 'Entrada', quantidade: 1 })
const enviando = ref(false)

const tipos = ['', 'Entrada', 'Saída', 'Ajuste', 'Cadastro']
const periodos = [{ v: 7, t: 'Últimos 7 dias' }, { v: 30, t: 'Últimos 30 dias' }, { v: 90, t: 'Últimos 90 dias' }, { v: '', t: 'Todo o período' }]

const selecionado = computed(() => produtos.value.find((p) => p.id === Number(form.produtoId)))
const saldoPrevisto = computed(() => {
  if (!selecionado.value) return null
  const q = Number(form.quantidade) || 0
  return selecionado.value.quantidade + (form.tipo === 'Entrada' ? q : -q)
})
const formValido = computed(() =>
  selecionado.value && Number.isInteger(Number(form.quantidade)) && form.quantidade >= 1 && form.quantidade <= 10000 && saldoPrevisto.value >= 0)

const resumo = computed(() => ({
  entradas: movs.value.filter((m) => m.quantidade > 0).reduce((s, m) => s + m.quantidade, 0),
  saidas: movs.value.filter((m) => m.quantidade < 0).reduce((s, m) => s - m.quantidade, 0)
}))

async function carregarHistorico() {
  carregando.value = true
  try { movs.value = await api.movimentacoes(filtros) }
  catch (e) { emit('aviso', e.message) }
  finally { carregando.value = false }
}

async function registrar() {
  if (!formValido.value) return
  enviando.value = true
  try {
    const delta = form.tipo === 'Entrada' ? Number(form.quantidade) : -Number(form.quantidade)
    const atualizado = await api.movimentar(form.produtoId, delta)
    Object.assign(selecionado.value, atualizado)
    emit('aviso', `${form.tipo} de ${form.quantidade} un. registrada · saldo ${atualizado.quantidade}`)
    form.quantidade = 1
    carregarHistorico()
  } catch (e) { emit('aviso', e.message) }
  finally { enviando.value = false }
}

watch(filtros, carregarHistorico)

onMounted(async () => {
  try { produtos.value = await api.listar() } catch (e) { emit('aviso', e.message) }
  carregarHistorico()
})
</script>

<template>
  <main class="page">
    <div class="page-header">
      <div>
        <h1>Movimentações</h1>
        <p>Registre entradas e saídas e consulte o histórico completo.</p>
      </div>
    </div>

    <section class="card registrar">
      <div class="card-header"><h2>Registrar movimentação</h2></div>
      <form class="form" @submit.prevent="registrar">
        <label class="field produto"><span>Produto</span>
          <select id="mov-produto" class="select" v-model="form.produtoId" required>
            <option value="" disabled>Selecione um produto</option>
            <option v-for="p in produtos" :key="p.id" :value="p.id">{{ p.nome }} — saldo {{ p.quantidade }}</option>
          </select>
        </label>

        <div class="field"><span>Tipo</span>
          <div class="segmented tipo" role="radiogroup">
            <button type="button" id="mov-entrada" role="radio" :aria-checked="form.tipo === 'Entrada'"
                    :class="{ active: form.tipo === 'Entrada' }" @click="form.tipo = 'Entrada'"><Icon name="up" :size="14" />Entrada</button>
            <button type="button" id="mov-saida" role="radio" :aria-checked="form.tipo === 'Saída'"
                    :class="{ active: form.tipo === 'Saída' }" @click="form.tipo = 'Saída'"><Icon name="down" :size="14" />Saída</button>
          </div>
        </div>

        <label class="field qtd"><span>Quantidade</span>
          <input id="mov-quantidade" class="input num" type="number" min="1" max="10000" step="1" v-model.number="form.quantidade" required />
        </label>

        <div class="field acao">
          <span :class="['previsto', saldoPrevisto < 0 ? 'erro' : '']">
            <template v-if="selecionado">Saldo após: <strong class="num">{{ saldoPrevisto }}</strong></template>&nbsp;
          </span>
          <button id="btn-registrar" type="submit" class="btn btn-primary" :disabled="!formValido || enviando">
            {{ enviando ? 'Registrando…' : 'Registrar' }}
          </button>
        </div>
      </form>
      <p v-if="saldoPrevisto < 0" class="aviso-form" role="alert">Estoque insuficiente: o saldo atual é {{ selecionado.quantidade }}.</p>
    </section>

    <section class="card">
      <div class="toolbar">
        <div class="filtros">
          <select id="filtro-produto" class="select" v-model="filtros.produtoId" aria-label="Filtrar por produto">
            <option value="">Todos os produtos</option>
            <option v-for="p in produtos" :key="p.id" :value="p.id">{{ p.nome }}</option>
          </select>
          <select id="filtro-periodo" class="select" v-model="filtros.dias" aria-label="Período">
            <option v-for="p in periodos" :key="p.v" :value="p.v">{{ p.t }}</option>
          </select>
        </div>
        <div class="segmented" role="tablist">
          <button v-for="t in tipos" :key="t || 'todos'" :id="`filtro-tipo-${t || 'todos'}`" role="tab"
                  :aria-selected="filtros.tipo === t" :class="{ active: filtros.tipo === t }" @click="filtros.tipo = t">
            {{ t || 'Todos' }}
          </button>
        </div>
      </div>

      <div class="table-wrap">
        <table class="table historico">
          <thead>
            <tr>
              <th>Data</th>
              <th>Produto</th>
              <th>Tipo</th>
              <th class="right">Quantidade</th>
              <th class="right">Saldo após</th>
              <th>Responsável</th>
            </tr>
          </thead>
          <tbody v-if="carregando">
            <tr v-for="i in 6" :key="i" class="skeleton"><td v-for="c in 6" :key="c"><span /></td></tr>
          </tbody>
          <tbody v-else-if="movs.length">
            <tr v-for="m in movs" :key="m.id">
              <td><span :title="dataHora(m.data)">{{ relativo(m.data) }}</span><small class="sub">{{ dataHora(m.data) }}</small></td>
              <td class="nome">{{ m.produtoNome }}</td>
              <td><span class="badge" :class="classeTipo[m.tipo]">{{ m.tipo }}</span></td>
              <td class="right num" :class="m.quantidade > 0 ? 'pos' : 'neg'">{{ sinal(m.quantidade) }}</td>
              <td class="right num">{{ int(m.saldoApos) }}</td>
              <td class="muted">{{ m.usuarioNome }}</td>
            </tr>
          </tbody>
        </table>

        <div v-if="!carregando && !movs.length" class="empty">
          <Icon name="inbox" :size="28" />
          <strong>Nenhuma movimentação encontrada</strong>
          <p>Ajuste os filtros ou registre uma movimentação acima.</p>
        </div>
      </div>

      <footer class="table-footer">
        <strong class="num">{{ movs.length }}</strong> registro(s)
        <template v-if="movs.length"> · <span class="pos num">+{{ int(resumo.entradas) }}</span> entradas · <span class="neg num">−{{ int(resumo.saidas) }}</span> saídas</template>
        <template v-if="movs.length >= 300"> · exibindo os 300 mais recentes</template>
      </footer>
    </section>
  </main>
</template>

<style scoped>
.registrar { margin-bottom: 16px; overflow: visible; }
.form { display: grid; grid-template-columns: minmax(220px, 2fr) auto 120px auto; gap: 16px; align-items: end; padding: 16px; }
.form .select { width: 100%; height: 38px; }
.tipo button { height: 32px; }
.acao { display: flex; flex-direction: column; align-items: flex-end; gap: 6px; }
.previsto { font-size: 12px; color: var(--text-2); }
.previsto.erro, .previsto.erro strong { color: var(--danger); }
.aviso-form { margin: -6px 16px 16px; color: var(--danger); font-size: 13px; }

.filtros { display: flex; gap: 8px; flex-wrap: wrap; }
.historico { min-width: 820px; }
.historico td small.sub { display: block; font-size: 12px; color: var(--text-3); }
.nome { font-weight: 500; }

@media (max-width: 900px) {
  .form { grid-template-columns: 1fr 1fr; }
  .produto { grid-column: 1 / -1; }
  .acao { grid-column: 1 / -1; flex-direction: row; justify-content: space-between; align-items: center; }
}
</style>
