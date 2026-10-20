<script setup>
import { reactive, watch, onMounted, onUnmounted } from 'vue'

const props = defineProps({ produto: Object })
const emit = defineEmits(['salvar', 'fechar'])

const vazio = () => ({ nome: '', categoria: '', preco: 0, quantidade: 0, estoqueMinimo: 5 })
const form = reactive(vazio())

watch(() => props.produto, (p) => Object.assign(form, p ? { ...p } : vazio()), { immediate: true })

function enviar() {
  if (!form.nome.trim()) return
  emit('salvar', {
    ...form,
    categoria: form.categoria.trim() || 'Geral',
    preco: Number(form.preco),
    quantidade: Number(form.quantidade),
    estoqueMinimo: Number(form.estoqueMinimo)
  })
}

const esc = (e) => e.key === 'Escape' && emit('fechar')
onMounted(() => window.addEventListener('keydown', esc))
onUnmounted(() => window.removeEventListener('keydown', esc))
</script>

<template>
  <div class="overlay" @mousedown.self="emit('fechar')">
    <form class="modal" role="dialog" aria-modal="true" @submit.prevent="enviar">
      <div class="modal-header">
        <h2>{{ produto?.id ? 'Editar produto' : 'Novo produto' }}</h2>
        <p>{{ produto?.id ? 'Atualize as informações do item.' : 'Cadastre um novo item no estoque.' }}</p>
      </div>
      <div class="modal-body">
        <label class="field"><span>Nome</span>
          <input id="input-nome" class="input" v-model="form.nome" required autofocus placeholder="Ex.: Teclado mecânico" />
        </label>
        <label class="field"><span>Categoria</span>
          <input id="input-categoria" class="input" v-model="form.categoria" placeholder="Ex.: Periféricos" />
        </label>
        <div class="grid">
          <label class="field"><span>Preço (R$)</span>
            <input id="input-preco" class="input num" type="number" step="0.01" min="0" v-model="form.preco" />
          </label>
          <label class="field"><span>Quantidade</span>
            <input id="input-quantidade" class="input num" type="number" min="0" v-model="form.quantidade" />
          </label>
          <label class="field"><span>Estoque mínimo</span>
            <input id="input-minimo" class="input num" type="number" min="0" v-model="form.estoqueMinimo" />
          </label>
        </div>
      </div>
      <div class="modal-footer">
        <button id="btn-cancelar" type="button" class="btn btn-secondary" @click="emit('fechar')">Cancelar</button>
        <button id="btn-salvar" type="submit" class="btn btn-primary">{{ produto?.id ? 'Salvar alterações' : 'Cadastrar' }}</button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
@media (max-width: 520px) { .grid { grid-template-columns: 1fr; } }
</style>
