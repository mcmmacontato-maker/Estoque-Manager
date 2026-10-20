<script setup>
import { ref } from 'vue'
import { auth } from '../api'
import Icon from './Icon.vue'

const emit = defineEmits(['entrou'])
const email = ref('')
const senha = ref('')
const mostrarSenha = ref(false)
const erro = ref('')
const enviando = ref(false)

async function entrar() {
  erro.value = ''
  enviando.value = true
  try {
    emit('entrou', await auth.login(email.value.trim(), senha.value))
  } catch (e) {
    erro.value = e.message
    senha.value = ''
  } finally {
    enviando.value = false
  }
}
</script>

<template>
  <div class="tela">
    <main class="painel">
      <div class="brand">
        <div class="logo"><Icon name="box" :size="18" /></div>
        <span>Estoque</span>
      </div>

      <h1>Entrar na sua conta</h1>
      <p class="sub">Use suas credenciais corporativas para acessar o inventário.</p>

      <form @submit.prevent="entrar" novalidate>
        <div v-if="erro" class="alerta" role="alert">{{ erro }}</div>

        <label class="field"><span>E-mail</span>
          <input id="login-email" class="input" type="email" v-model="email" autocomplete="username"
                 required maxlength="160" autofocus placeholder="nome@empresa.com" />
        </label>

        <label class="field"><span>Senha</span>
          <div class="senha">
            <input id="login-senha" class="input" :type="mostrarSenha ? 'text' : 'password'" v-model="senha"
                   autocomplete="current-password" required maxlength="128" />
            <button id="btn-mostrar-senha" type="button" class="toggle" @click="mostrarSenha = !mostrarSenha"
                    :aria-label="mostrarSenha ? 'Ocultar senha' : 'Mostrar senha'">
              {{ mostrarSenha ? 'Ocultar' : 'Mostrar' }}
            </button>
          </div>
        </label>

        <button id="btn-entrar" type="submit" class="btn btn-primary full" :disabled="enviando || !email || !senha">
          {{ enviando ? 'Entrando…' : 'Entrar' }}
        </button>
      </form>

      <p class="rodape">Acesso restrito. Tentativas de login são registradas.</p>
    </main>
  </div>
</template>

<style scoped>
.tela { min-height: 100vh; display: grid; place-items: center; padding: 24px; background: var(--bg); }
.painel {
  width: min(400px, 100%); background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius); padding: 32px;
}
.brand { display: flex; align-items: center; gap: 10px; font-weight: 600; font-size: 15px; margin-bottom: 28px; }
.logo { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 6px; background: var(--text); color: #fff; }
h1 { font-size: 20px; font-weight: 600; }
.sub { color: var(--text-2); margin: 4px 0 24px; }
form { display: grid; gap: 16px; }
.alerta { background: var(--danger-soft); color: var(--danger); border: 1px solid #FECDCA; border-radius: var(--radius-sm); padding: 10px 12px; font-size: 13px; }
.senha { position: relative; }
.senha .input { padding-right: 76px; }
.toggle {
  position: absolute; right: 6px; top: 50%; transform: translateY(-50%);
  border: none; background: transparent; color: var(--text-2); font-size: 13px; font-weight: 500;
  padding: 4px 8px; border-radius: 4px;
}
.toggle:hover { background: var(--surface-2); color: var(--text); }
.full { width: 100%; height: 40px; margin-top: 4px; }
.rodape { margin-top: 24px; padding-top: 16px; border-top: 1px solid var(--border); color: var(--text-3); font-size: 12px; text-align: center; }
</style>
