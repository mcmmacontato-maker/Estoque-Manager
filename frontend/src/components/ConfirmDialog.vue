<script setup>
import { onMounted, onUnmounted } from 'vue'

defineProps({ titulo: String, mensagem: String, acao: { type: String, default: 'Excluir' } })
const emit = defineEmits(['confirmar', 'fechar'])

const esc = (e) => e.key === 'Escape' && emit('fechar')
onMounted(() => window.addEventListener('keydown', esc))
onUnmounted(() => window.removeEventListener('keydown', esc))
</script>

<template>
  <div class="overlay" @mousedown.self="emit('fechar')">
    <div class="modal small" role="alertdialog" aria-modal="true">
      <div class="modal-header">
        <h2>{{ titulo }}</h2>
        <p>{{ mensagem }}</p>
      </div>
      <div class="modal-footer">
        <button id="btn-confirm-cancelar" class="btn btn-secondary" @click="emit('fechar')">Cancelar</button>
        <button id="btn-confirm-ok" class="btn btn-danger" @click="emit('confirmar')">{{ acao }}</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.small { width: min(420px, 100%); }
.modal-header { padding-bottom: 20px; }
</style>
