<script setup>
import { ref, onMounted } from 'vue'
import { api } from '../api.js'
import SchedaPartita from '../componenti/SchedaPartita.vue'
import StatoDati from '../componenti/StatoDati.vue'

const elenco = ref([])
const pagina = ref(0)
const totale = ref(0)
const primoCaricamento = ref(true)
const altreInArrivo = ref(false)
const errore = ref('')

async function altre() {
  errore.value = ''
  altreInArrivo.value = pagina.value > 0
  try {
    const r = await api(`partite?pagina=${pagina.value + 1}&perPagina=30`)
    elenco.value.push(...r.partite)
    pagina.value = r.pagina
    totale.value = r.totale
  } catch (e) {
    errore.value = e.message
  } finally {
    primoCaricamento.value = false
    altreInArrivo.value = false
  }
}
onMounted(altre)
</script>

<template>
  <div class="testa-pagina">
    <h1>Partite</h1>
    <p>Lo storico dei TG Dito, dalla più recente. Per l'ELO contano quelle oltre i 10 minuti di gioco.</p>
  </div>

  <StatoDati
    :caricamento="primoCaricamento"
    :errore="elenco.length ? '' : errore"
    :vuoto="!elenco.length"
    :righe="8"
    testo-vuoto="Non c'è ancora nessuna partita registrata."
    @riprova="altre"
  >
    <!-- le partite in più («Mostra altre 30») entrano scivolando dal basso -->
    <TransitionGroup tag="ul" name="partita" class="partite">
      <SchedaPartita v-for="x in elenco" :key="x.partita.idGame" :partita="x.partita" :giocatori="x.giocatori" />
    </TransitionGroup>
    <div class="ancora">
      <p v-if="errore" class="perso" role="alert">Le altre partite non sono arrivate ({{ errore }}).</p>
      <button
        v-if="elenco.length < totale"
        type="button"
        class="btn btn--linea"
        :aria-busy="altreInArrivo"
        :disabled="altreInArrivo"
        @click="altre"
      >
        {{ altreInArrivo ? 'Arrivano…' : `Mostra altre 30 (${elenco.length} di ${totale})` }}
      </button>
    </div>
  </StatoDati>
</template>

<style scoped>
/* ---------- movimento ---------- */
:deep(.partita-enter-active) { transition: transform var(--dur-long) var(--ease-out); }
:deep(.partita-enter-from) { transform: translateY(12px); }
</style>
