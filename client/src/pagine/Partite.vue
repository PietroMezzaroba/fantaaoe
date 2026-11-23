<script setup>
import { ref, onMounted } from 'vue'
import { api } from '../api.js'
import SchedaPartita from '../componenti/SchedaPartita.vue'

const elenco = ref([])
const pagina = ref(0)
const totale = ref(0)
const caricamento = ref(false)
const errore = ref('')

async function altre() {
  caricamento.value = true
  errore.value = ''
  try {
    const r = await api(`partite?pagina=${pagina.value + 1}&perPagina=30`)
    elenco.value.push(...r.partite)
    pagina.value = r.pagina
    totale.value = r.totale
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(altre)
</script>

<template>
  <h1>Partite</h1>
  <p class="tenue piccolo">Storico dei TG Dito, dalla più recente. In verde chi ha vinto. Valide solo quelle oltre i 10 minuti di gioco.</p>

  <div class="lista-partite">
    <SchedaPartita v-for="x in elenco" :key="x.partita.idGame" :partita="x.partita" :giocatori="x.giocatori" />
  </div>

  <div v-if="errore" class="stato errore">{{ errore }}</div>
  <div v-else-if="caricamento" class="stato">Caricamento…</div>
  <div v-else-if="elenco.length < totale" class="stato">
    <button class="pulsante" @click="altre">Carica altre ({{ elenco.length }} di {{ totale }})</button>
  </div>
</template>
