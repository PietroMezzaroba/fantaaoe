<script setup>
// Login simulato, solo sviluppo: si monta solo se il server dice loginSviluppo (mai in produzione).
import { ref, watch, nextTick } from 'vue'
import { sessione, caricaSessione } from '../sessione.js'
import { apiPost } from '../api.js'

const username = ref('')
const password = ref('')
const errore = ref('')
const invio = ref(false)
const campoUsername = ref(null)

watch(() => sessione.accessoAperto, async (aperto) => {
  if (!aperto) return
  errore.value = ''
  await nextTick()
  campoUsername.value?.focus()
})

function chiudi() {
  sessione.accessoAperto = false
  password.value = ''
}

async function entra() {
  errore.value = ''
  invio.value = true
  try {
    await apiPost('auth/dev-login', { username: username.value, password: password.value })
    await caricaSessione()
    chiudi()
  } catch (e) {
    errore.value = e.message
  } finally {
    invio.value = false
  }
}
</script>

<template>
  <div v-if="sessione.accessoAperto" class="dev-velo" @click.self="chiudi" @keydown.esc="chiudi">
    <form class="dev-form" role="dialog" aria-modal="true" aria-labelledby="dev-titolo" @submit.prevent="entra">
      <h2 id="dev-titolo">Accesso di sviluppo</h2>
      <p class="dev-nota">Login simulato: gli utenti sono nella tabella <code>fanta_torneo.utenti_dev</code>.</p>
      <label class="campo">Username
        <input ref="campoUsername" v-model="username" class="cerca" type="text" autocomplete="username" required />
      </label>
      <label class="campo">Password
        <input v-model="password" class="cerca" type="password" autocomplete="current-password" required />
      </label>
      <p v-if="errore" class="dev-errore" role="alert">{{ errore }}</p>
      <div class="dev-azioni">
        <button type="button" class="btn btn--linea" @click="chiudi">Annulla</button>
        <button type="submit" class="btn" :disabled="invio">{{ invio ? 'Accesso…' : 'Accedi' }}</button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.dev-velo {
  position: fixed; inset: 0; z-index: var(--z-modal);
  display: grid; place-items: center; padding: var(--space-md);
  background: oklch(10% 0.02 255 / 0.7);
}
.dev-form {
  width: min(100%, 22rem); display: grid; gap: var(--space-md);
  padding: var(--space-lg); background: var(--color-paper-2);
  border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
h2 { margin: 0; font-size: var(--text-xl); font-weight: var(--display-weight); }
.dev-nota { margin: 0; font-size: var(--text-sm); color: var(--color-muted); }
code { color: var(--color-accent); }
.dev-errore { margin: 0; font-size: var(--text-sm); color: var(--color-error); }
.dev-azioni { display: flex; justify-content: flex-end; gap: var(--space-xs); }
</style>
