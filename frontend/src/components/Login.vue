<script setup>
import { ref, onMounted } from 'vue'
import { auth } from '../api'
import Icon from './Icon.vue'

const emit = defineEmits(['entrou'])
const email = ref('')
const senha = ref('')
const mostrarSenha = ref(false)
const erro = ref('')
const enviando = ref(null) // 'form' | 'Admin' | 'Operador'
const demo = ref({ ativo: false })

const contasDemo = [
  { perfil: 'Admin', titulo: 'Administrador', desc: 'Cadastra, edita, exclui e movimenta produtos' },
  { perfil: 'Operador', titulo: 'Operador', desc: 'Consulta e registra entradas e saídas' }
]

onMounted(async () => { demo.value = await auth.demoInfo() })

async function executar(chave, fn) {
  erro.value = ''
  enviando.value = chave
  try { emit('entrou', await fn()) }
  catch (e) { erro.value = e.message; senha.value = '' }
  finally { enviando.value = null }
}

const entrar = () => executar('form', () => auth.login(email.value.trim(), senha.value))
const entrarDemo = (perfil) => executar(perfil, () => auth.loginDemo(perfil))
</script>

<template>
  <div class="tela">
    <main class="painel">
      <div class="brand">
        <div class="logo"><Icon name="box" :size="18" /></div>
        <span>Estoque</span>
      </div>

      <h1>Entrar na sua conta</h1>
      <p class="sub">{{ demo.ativo ? 'Escolha um perfil para explorar o sistema.' : 'Use suas credenciais para acessar o inventário.' }}</p>

      <div v-if="erro" class="alerta" role="alert">{{ erro }}</div>

      <section v-if="demo.ativo" class="demo" aria-label="Acesso de demonstração">
        <button v-for="c in contasDemo" :key="c.perfil" :id="`btn-demo-${c.perfil.toLowerCase()}`"
                class="conta" :disabled="!!enviando" @click="entrarDemo(c.perfil)">
          <span class="conta-avatar">{{ c.titulo[0] }}</span>
          <span class="conta-texto">
            <strong>{{ enviando === c.perfil ? 'Entrando…' : `Entrar como ${c.titulo}` }}</strong>
            <small>{{ c.desc }}</small>
          </span>
          <Icon name="chevron" class="conta-seta" />
        </button>

        <div class="divisor"><span>ou entre com e-mail</span></div>
      </section>

      <form @submit.prevent="entrar" novalidate>
        <label class="field"><span>E-mail</span>
          <input id="login-email" class="input" type="email" v-model="email" autocomplete="username"
                 required maxlength="160" :autofocus="!demo.ativo" placeholder="nome@empresa.com" />
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

        <button id="btn-entrar" type="submit" :class="['btn', 'full', demo.ativo ? 'btn-secondary' : 'btn-primary']"
                :disabled="!!enviando || !email || !senha">
          {{ enviando === 'form' ? 'Entrando…' : 'Entrar' }}
        </button>
      </form>

      <p v-if="demo.ativo" class="rodape">
        <span class="pill">Demonstração</span>
        Dados fictícios{{ demo.resetMinutos ? `, restaurados a cada ${demo.resetMinutos} min` : '' }}. Fique à vontade para testar.
      </p>
      <p v-else class="rodape">Acesso restrito. Tentativas de login são registradas.</p>
    </main>
  </div>
</template>

<style scoped>
.tela { min-height: 100vh; display: grid; place-items: center; padding: 24px; background: var(--bg); }
.painel {
  width: min(420px, 100%); background: var(--surface); border: 1px solid var(--border);
  border-radius: var(--radius); padding: 32px;
}
.brand { display: flex; align-items: center; gap: 10px; font-weight: 600; font-size: 15px; margin-bottom: 28px; }
.logo { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 6px; background: var(--text); color: #fff; }
h1 { font-size: 20px; font-weight: 600; }
.sub { color: var(--text-2); margin: 4px 0 24px; }
.alerta { background: var(--danger-soft); color: var(--danger); border: 1px solid #FECDCA; border-radius: var(--radius-sm); padding: 10px 12px; font-size: 13px; margin-bottom: 16px; }

.demo { display: grid; gap: 10px; }
.conta {
  display: flex; align-items: center; gap: 12px; width: 100%; padding: 12px 14px; text-align: left;
  background: var(--surface); border: 1px solid var(--border-strong); border-radius: var(--radius);
  transition: border-color .15s, background-color .15s;
}
.conta:hover:not(:disabled) { border-color: var(--primary); background: var(--primary-soft); }
.conta:disabled { opacity: .6; cursor: wait; }
.conta-avatar {
  flex: none; width: 36px; height: 36px; border-radius: 50%; display: grid; place-items: center;
  background: var(--surface-2); border: 1px solid var(--border); font-weight: 600; color: var(--text-2);
}
.conta:hover:not(:disabled) .conta-avatar { background: var(--primary); border-color: var(--primary); color: #fff; }
.conta-texto { display: grid; gap: 2px; flex: 1; min-width: 0; }
.conta-texto strong { font-weight: 500; }
.conta-texto small { color: var(--text-2); font-size: 13px; }
.conta-seta { color: var(--text-3); flex: none; }

.divisor { display: flex; align-items: center; gap: 12px; color: var(--text-3); font-size: 12px; margin: 14px 0 6px; }
.divisor::before, .divisor::after { content: ''; flex: 1; height: 1px; background: var(--border); }

form { display: grid; gap: 16px; }
.senha { position: relative; }
.senha .input { padding-right: 76px; }
.toggle {
  position: absolute; right: 6px; top: 50%; transform: translateY(-50%);
  border: none; background: transparent; color: var(--text-2); font-size: 13px; font-weight: 500;
  padding: 4px 8px; border-radius: 4px;
}
.toggle:hover { background: var(--surface-2); color: var(--text); }
.full { width: 100%; height: 40px; margin-top: 4px; }
.rodape { margin-top: 24px; padding-top: 16px; border-top: 1px solid var(--border); color: var(--text-3); font-size: 12px; text-align: center; line-height: 1.7; }
.pill { display: inline-block; padding: 0 8px; margin-right: 4px; border-radius: 999px; background: var(--warning-soft); color: var(--warning); font-weight: 500; }
</style>
