<script setup>
// Pagina dell'asta: pulsanti d'accesso (per ruolo) e finestre delle squadre.
import { ref, computed, onMounted } from 'vue'
import { api } from '../api.js'
import { sessione } from '../sessione.js'
import FinestreSquadre from '../componenti/FinestreSquadre.vue'

// chi vede quale pulsante: capitani -> "Inizia l'asta", streamer -> "Gestisci l'asta" (solo il ruolo esatto, admin escluso)
// il pulsante "Inizia l'asta" compare solo ad asta attiva (stato_globale_asta = 'asta attiva')
const astaAttiva = ref(false)
onMounted(async () => {
  try {
    astaAttiva.value = (await api('asta/stato')).attiva
  } catch {
    astaAttiva.value = false
  }
})
const eCapitano = computed(() => astaAttiva.value && sessione.autenticato && sessione.ruoli.includes('capitano'))
const eStreamer = computed(() => sessione.autenticato && sessione.ruoli.includes('streamer'))
</script>

<template>
  <div class="testa-pagina">
    <h1>Asta</h1>
  </div>

  <div v-if="eCapitano || eStreamer" class="azioni-asta">
    <RouterLink v-if="eCapitano" to="/asta/inizia" class="btn azioni-asta__btn">Inizia l'asta</RouterLink>
    <RouterLink v-if="eStreamer" to="/asta/gestisci" class="btn azioni-asta__btn">Gestisci l'asta</RouterLink>
  </div>


  <FinestreSquadre />
</template>

<style scoped>
/* pulsanti larghi in cima alla pagina, uno accanto all'altro a dividersi la larghezza */
.azioni-asta { display: flex; gap: var(--space-md); margin-bottom: var(--space-lg, 1.5rem); }
.azioni-asta__btn { flex: 1 1 0; justify-content: center; padding-block: var(--space-md); font-size: var(--text-lg); }
</style>
