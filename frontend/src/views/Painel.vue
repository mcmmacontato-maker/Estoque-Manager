<script setup>
import { ref, computed, onMounted } from 'vue'
import { api } from '../api'
import { brl, brlCurto, int, diaCurto, relativo, sinal, classeTipo } from '../format'
import Icon from '../components/Icon.vue'

const emit = defineEmits(['aviso'])
const d = ref(null)

const maxSerie = computed(() => Math.max(1, ...(d.value?.serie ?? []).flatMap((s) => [s.entradas, s.saidas])))
const maxCategoria = computed(() => Math.max(1, ...(d.value?.porCategoria ?? []).map((c) => c.valor)))
const totalCategorias = computed(() => (d.value?.porCategoria ?? []).reduce((s, c) => s + c.valor, 0) || 1)

// Escala do eixo Y com valores "redondos"
const eixo = computed(() => {
  const m = maxSerie.value
  const passo = m <= 5 ? 1 : m <= 10 ? 2 : m <= 25 ? 5 : m <= 50 ? 10 : Math.ceil(m / 50) * 10
  const topo = Math.ceil(m / passo) * passo
  return { topo, marcas: Array.from({ length: topo / passo + 1 }, (_, i) => topo - i * passo) }
})

onMounted(async () => {
  try { d.value = await api.painel() }
  catch (e) { emit('aviso', e.message) }
})
</script>

<template>
  <main class="page">
    <div class="page-header">
      <div>
        <h1>Painel</h1>
        <p>Visão geral do inventário e da movimentação recente.</p>
      </div>
    </div>

    <template v-if="d">
      <section class="kpis" aria-label="Indicadores">
        <div class="kpi">
          <span>Valor do estoque</span>
          <strong class="num">{{ brl(d.totais.valor) }}</strong>
          <small>{{ int(d.totais.unidades) }} unidades em {{ d.totais.produtos }} produtos</small>
        </div>
        <div class="kpi">
          <span>Entradas · 14 dias</span>
          <strong class="num pos-num"><Icon name="up" :size="18" />{{ int(d.totais.entradas14d) }}</strong>
          <small>unidades recebidas</small>
        </div>
        <div class="kpi">
          <span>Saídas · 14 dias</span>
          <strong class="num neg-num"><Icon name="down" :size="18" />{{ int(d.totais.saidas14d) }}</strong>
          <small>unidades expedidas</small>
        </div>
        <div class="kpi">
          <span>Requer reposição</span>
          <strong class="num">{{ d.totais.esgotados + d.totais.baixo }}</strong>
          <small>{{ d.totais.esgotados }} esgotado(s) · {{ d.totais.baixo }} abaixo do mínimo</small>
        </div>
      </section>

      <div class="grid g-2-1">
        <section class="card">
          <div class="card-header">
            <h2>Movimentação diária</h2>
            <div class="legenda">
              <span><i class="dot entrada" />Entradas</span>
              <span><i class="dot saida" />Saídas</span>
            </div>
          </div>
          <div class="card-body">
            <div class="chart" role="img" :aria-label="`Gráfico de entradas e saídas nos últimos 14 dias`">
              <div class="eixo-y">
                <span v-for="m in eixo.marcas" :key="m" class="num">{{ m }}</span>
              </div>
              <div class="area">
                <div class="linhas"><i v-for="m in eixo.marcas" :key="m" /></div>
                <div v-for="s in d.serie" :key="s.dia" class="dia" :title="`${diaCurto(s.dia)}: +${s.entradas} / −${s.saidas}`">
                  <div class="barras">
                    <span class="barra entrada" :style="{ height: `${(s.entradas / eixo.topo) * 100}%` }" />
                    <span class="barra saida" :style="{ height: `${(s.saidas / eixo.topo) * 100}%` }" />
                  </div>
                  <span class="rotulo num">{{ diaCurto(s.dia) }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section class="card">
          <div class="card-header">
            <h2>Requer reposição</h2>
            <a href="#/produtos">Ver produtos</a>
          </div>
          <ul v-if="d.criticos.length" class="lista">
            <li v-for="p in d.criticos" :key="p.id">
              <div class="lista-topo">
                <span class="lista-nome">{{ p.nome }}</span>
                <span class="badge" :class="p.quantidade === 0 ? 'esgotado' : 'baixo'">
                  {{ p.quantidade === 0 ? 'Esgotado' : `${p.quantidade} de ${p.estoqueMinimo}` }}
                </span>
              </div>
              <div class="progresso" :aria-label="`${p.quantidade} de ${p.estoqueMinimo} unidades mínimas`">
                <span :class="p.quantidade === 0 ? 'zero' : ''" :style="{ width: `${Math.min(100, (p.quantidade / Math.max(1, p.estoqueMinimo)) * 100)}%` }" />
              </div>
            </li>
          </ul>
          <div v-else class="empty"><strong>Tudo em ordem</strong><p>Nenhum produto abaixo do mínimo.</p></div>
        </section>
      </div>

      <div class="grid g-1-1">
        <section class="card">
          <div class="card-header"><h2>Valor por categoria</h2></div>
          <ul class="lista">
            <li v-for="c in d.porCategoria" :key="c.categoria">
              <div class="lista-topo">
                <span class="lista-nome">{{ c.categoria }} <small>{{ c.itens }} {{ c.itens === 1 ? 'item' : 'itens' }}</small></span>
                <span class="num valor">{{ brlCurto(c.valor) }} <small>{{ Math.round((c.valor / totalCategorias) * 100) }}%</small></span>
              </div>
              <div class="progresso"><span class="azul" :style="{ width: `${(c.valor / maxCategoria) * 100}%` }" /></div>
            </li>
          </ul>
        </section>

        <section class="card">
          <div class="card-header">
            <h2>Atividade recente</h2>
            <a href="#/movimentacoes">Ver todas</a>
          </div>
          <ul v-if="d.ultimas.length" class="atividade">
            <li v-for="m in d.ultimas" :key="m.id">
              <span class="badge" :class="classeTipo[m.tipo]">{{ m.tipo }}</span>
              <div class="atividade-texto">
                <span class="lista-nome">{{ m.produtoNome }}</span>
                <small>{{ m.usuarioNome }} · {{ relativo(m.data) }}</small>
              </div>
              <span class="num" :class="m.quantidade > 0 ? 'pos' : 'neg'">{{ sinal(m.quantidade) }}</span>
            </li>
          </ul>
          <div v-else class="empty"><strong>Sem movimentações</strong><p>As entradas e saídas aparecerão aqui.</p></div>
        </section>
      </div>
    </template>

    <div v-else class="carregando">
      <div class="kpis"><div v-for="i in 4" :key="i" class="kpi skeleton"><span /><span style="height:22px;width:60%" /></div></div>
    </div>
  </main>
</template>

<style scoped>
.grid { display: grid; gap: 16px; margin-bottom: 16px; }
.g-2-1 { grid-template-columns: 2fr 1fr; }
.g-1-1 { grid-template-columns: 1fr 1fr; }

.kpi strong { display: flex; align-items: center; gap: 6px; }
.pos-num svg { color: var(--success); }
.neg-num svg { color: var(--danger); }

.legenda { display: flex; gap: 14px; font-size: 12px; color: var(--text-2); }
.legenda span { display: flex; align-items: center; gap: 6px; }
.dot { width: 8px; height: 8px; border-radius: 2px; display: inline-block; }
.dot.entrada, .barra.entrada { background: #12B76A; }
.dot.saida, .barra.saida { background: #F04438; }

.chart { display: grid; grid-template-columns: 28px 1fr; gap: 8px; height: 240px; }
.eixo-y { display: flex; flex-direction: column; justify-content: space-between; font-size: 11px; color: var(--text-3); text-align: right; padding-bottom: 22px; }
.eixo-y span { line-height: 0; }
.area { position: relative; display: grid; grid-template-columns: repeat(14, 1fr); gap: 4px; }
.linhas { position: absolute; inset: 0 0 22px; display: flex; flex-direction: column; justify-content: space-between; pointer-events: none; }
.linhas i { border-top: 1px dashed var(--border); }
.linhas i:last-child { border-top-style: solid; border-color: var(--border-strong); }
.dia { position: relative; display: grid; grid-template-rows: 1fr 22px; min-width: 0; }
.barras { display: flex; align-items: flex-end; justify-content: center; gap: 2px; }
.barra { width: min(10px, 40%); border-radius: 2px 2px 0 0; min-height: 0; transition: height .4s ease; }
.dia:hover .barras { background: var(--surface-2); border-radius: 4px 4px 0 0; }
.rotulo { font-size: 11px; color: var(--text-3); text-align: center; padding-top: 6px; white-space: nowrap; overflow: hidden; }
.dia:nth-child(even) .rotulo { visibility: hidden; }

.lista { list-style: none; padding: 4px 16px 12px; }
.lista li { padding: 10px 0; border-bottom: 1px solid var(--border); }
.lista li:last-child { border-bottom: none; }
.lista-topo { display: flex; justify-content: space-between; align-items: center; gap: 8px; margin-bottom: 8px; }
.lista-nome { font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lista-nome small, .valor small { color: var(--text-3); font-weight: 400; font-size: 12px; margin-left: 4px; }
.valor { font-weight: 500; white-space: nowrap; }
.progresso { height: 6px; background: var(--surface-2); border-radius: 999px; overflow: hidden; }
.progresso span { display: block; height: 100%; background: #F79009; border-radius: 999px; transition: width .4s ease; }
.progresso span.zero { width: 100% !important; background: #FDA29B; }
.progresso span.azul { background: var(--primary); }

.atividade { list-style: none; padding: 4px 16px 8px; }
.atividade li { display: grid; grid-template-columns: 84px 1fr auto; align-items: center; gap: 12px; padding: 10px 0; border-bottom: 1px solid var(--border); }
.atividade li:last-child { border-bottom: none; }
.atividade .badge { justify-self: start; }
.atividade-texto { display: grid; min-width: 0; }
.atividade-texto small { color: var(--text-3); font-size: 12px; }

.skeleton span { display: block; height: 12px; border-radius: 4px; background: var(--surface-2); animation: pulse 1.2s ease-in-out infinite; }

@media (max-width: 1100px) {
  .g-2-1, .g-1-1 { grid-template-columns: 1fr; }
}
</style>
