<script setup>
// Piccola form al centro dello schermo che chiede conferma dell'iscrizione al torneo.
// "No" chiude; "Sì" crea l'iscrizione (un solo record per utente in utenti_iscritti).
import { ref, watch, nextTick } from 'vue'
import { api, apiPost } from '../api.js'

const props = defineProps({ aperta: Boolean })
const emit = defineEmits(['chiudi'])

const bottoneSi = ref(null)
const invio = ref(false)
const errore = ref('')
const fatto = ref(false)
const verifica = ref(false) // controllo iniziale in corso
const chiuse = ref(false) // iscrizioni disattive
const giaIscritto = ref(false)

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
  chiuse.value = false
  giaIscritto.value = false
  // prima di chiedere conferma si controlla se le iscrizioni sono aperte e se l'utente non è già iscritto
  verifica.value = true
  try {
    const stato = await api('profilo/iscrizione')
    chiuse.value = !stato.aperte
    giaIscritto.value = stato.giaIscritto
  } catch (e) {
    errore.value = e.message
  } finally {
    verifica.value = false
  }
  await nextTick()
  bottoneSi.value?.focus()
})
</script>

<template>
  <div v-if="aperta" class="conferma-velo" @click.self="emit('chiudi')" @keydown.esc="emit('chiudi')">
    <div class="conferma" role="dialog" aria-modal="true" aria-labelledby="conferma-titolo">
      <h2 id="conferma-titolo">Iscrizione al torneo</h2>
      <p v-if="verifica" class="conferma__attesa">Controllo in corso…</p>
      <p v-else-if="chiuse">Le iscrizioni al Fanta Age sono disattive.</p>
      <p v-else-if="giaIscritto">Sei già iscritto al Fanta Age.</p>
      <p v-else-if="fatto">Iscrizione registrata! Sarà verificata a breve.</p>
      <p v-else>Vuoi confermare la tua iscrizione al FantaAge?</p>
      <p v-if="errore" class="conferma__errore" role="alert">{{ errore }}</p>
      <div class="conferma__azioni">
        <template v-if="fatto || chiuse || giaIscritto">
          <button type="button" class="btn" :disabled="verifica" @click="emit('chiudi')">Chiudi</button>
        </template>
        <template v-else>
          <button type="button" class="btn btn--linea" :disabled="invio" @click="emit('chiudi')">No</button>
          <button ref="bottoneSi" type="button" class="btn" :disabled="invio || verifica" @click="conferma">Sì</button>
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
