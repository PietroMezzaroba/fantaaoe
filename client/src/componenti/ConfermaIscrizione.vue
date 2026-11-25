<script setup>
// Piccola form al centro dello schermo che chiede conferma dell'iscrizione al torneo.
// "No" chiude; "Sì" crea l'iscrizione (un solo record per utente in utenti_iscritti).
import { ref, watch, nextTick } from 'vue'
import { apiPost } from '../api.js'

const props = defineProps({ aperta: Boolean })
const emit = defineEmits(['chiudi'])

const bottoneSi = ref(null)
const invio = ref(false)
const errore = ref('')
const fatto = ref(false)

async function conferma() {
  invio.value = true
  errore.value = ''
  try {
    await apiPost('profilo/iscriviti', {})
    fatto.value = true
  } catch (e) {
    errore.value = e.message
  } finally {
    invio.value = false
  }
}

watch(() => props.aperta, async (aperta) => {
  if (!aperta) return
  errore.value = ''
  fatto.value = false
  await nextTick()
  bottoneSi.value?.focus()
})
</script>

<template>
  <div v-if="aperta" class="conferma-velo" @click.self="emit('chiudi')" @keydown.esc="emit('chiudi')">
    <div class="conferma" role="dialog" aria-modal="true" aria-labelledby="conferma-titolo">
      <h2 id="conferma-titolo">Iscrizione al torneo</h2>
      <p v-if="fatto">Iscrizione registrata! Sarà verificata a breve.</p>
      <p v-else>Vuoi confermare la tua iscrizione al FantaAge?</p>
      <p v-if="errore" class="conferma__errore" role="alert">{{ errore }}</p>
      <div class="conferma__azioni">
        <template v-if="fatto">
          <button type="button" class="btn" @click="emit('chiudi')">Chiudi</button>
        </template>
        <template v-else>
          <button type="button" class="btn btn--linea" :disabled="invio" @click="emit('chiudi')">No</button>
          <button ref="bottoneSi" type="button" class="btn" :disabled="invio" @click="conferma">Sì</button>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
.conferma-velo {
  position: fixed; inset: 0; z-index: var(--z-modal);
  display: grid; place-items: center; padding: var(--space-md);
  background: oklch(10% 0.02 255 / 0.7);
}
.conferma {
  width: min(100%, 22rem); display: grid; gap: var(--space-md);
  padding: var(--space-lg); background: var(--color-paper-2);
  border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
h2 { margin: 0; font-size: var(--text-xl); font-weight: var(--display-weight); }
p { margin: 0; color: var(--color-ink-2); }
.conferma__errore { color: var(--color-danger, #e5484d); }
.conferma__azioni { display: flex; justify-content: flex-end; gap: var(--space-xs); }
</style>
