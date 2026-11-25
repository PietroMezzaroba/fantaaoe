<script setup>
// Riquadro con i giocatori ancora disponibili: ricerca per nome (anche parziale) e, sotto, l'elenco con l'ELO a fianco.
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api, apiPost } from '../api.js'
import StatoDati from './StatoDati.vue'
import FinestraModale from './FinestraModale.vue'
import { ascoltaAsta } from '../astaLive.js'

// chiamabile = true: ogni giocatore ha il tasto per fare la prima offerta (pagina "Inizia l'asta")
defineProps({ chiamabile: { type: Boolean, default: false } })

const giocatori = ref([])
const caricamento = ref(true)
const errore = ref('')
const ricerca = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    giocatori.value = await api('asta/giocatori-disponibili')
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
// i tasti "Offri" si attivano solo se è il turno dell'utente (fase chiamata_giocatore e capitano di turno)
const tuoTurno = ref(false)
async function caricaTurno() {
  try {
    tuoTurno.value = (await api('asta/turno')).tuoTurno
  } catch {
    tuoTurno.value = false
  }
}
// elenco e turno cambiano solo quando qualcuno chiama un giocatore: il server lo comunica (SignalR), niente polling
const ricarica = () => Promise.all([carica(), caricaTurno()])
let smettiDiAscoltare = () => {}
onMounted(() => {
  ricarica()
  smettiDiAscoltare = ascoltaAsta({ suEvento: (e) => e.tipo === 'chiamata' && ricarica(), suRiconnesso: ricarica })
})
onBeforeUnmount(() => smettiDiAscoltare())

// dialog della prima offerta su un giocatore: salva la chiamata (asta_giocatori_chiamati); il server controlla turno e credito
const vFocus = { mounted: (el) => el.focus() }
const scelto = ref(null)
const primaPuntata = ref('')
const puntataValida = computed(() => Number.isInteger(primaPuntata.value) && primaPuntata.value >= 0)
const invio = ref(false)
const erroreChiamata = ref('')
function apri(g) {
  scelto.value = g
  primaPuntata.value = ''
  erroreChiamata.value = ''
}
const chiudi = () => { scelto.value = null }
async function conferma() {
  if (!puntataValida.value || invio.value) return
  invio.value = true
  erroreChiamata.value = ''
  try {
    await apiPost('asta/chiamata', { idGiocatore: scelto.value.idDiscord, puntata: primaPuntata.value })
    chiudi()
    await Promise.all([carica(), caricaTurno()]) // il giocatore chiamato esce dall'elenco e il turno passa al capitano successivo
  } catch (e) {
    erroreChiamata.value = e.message
  } finally {
    invio.value = false
  }
}

// filtra per nome senza badare a maiuscole/minuscole e accenti
const normalizza = (t) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()
const visibili = computed(() => {
  const q = normalizza(ricerca.value.trim())
  return q ? giocatori.value.filter((g) => normalizza(g.nome).includes(q)) : giocatori.value
})
</script>

<template>
  <section class="disponibili" aria-labelledby="disponibili-titolo">
    <h2 id="disponibili-titolo" class="disponibili__titolo">Giocatori disponibili</h2>
    <input
      v-model="ricerca"
      type="search"
      class="campo disponibili__ricerca"
      placeholder="Cerca per nome"
      aria-label="Cerca un giocatore per nome"
      autocomplete="off"
    />
    <StatoDati
      :caricamento="caricamento"
      :errore="errore"
      :vuoto="!giocatori.length"
      :righe="6"
      testo-vuoto="Non ci sono giocatori disponibili."
      @riprova="carica"
    >
      <ul v-if="visibili.length" class="disponibili__elenco">
        <li v-for="g in visibili" :key="g.idDiscord">
          <span class="disponibili__nome" :title="g.nome">{{ g.nome }}</span>
          <span class="disponibili__elo">{{ g.elo }}</span>
          <button v-if="chiamabile" type="button" class="btn btn--sm disponibili__chiama" :disabled="!tuoTurno" :title="tuoTurno ? '' : 'Non è il tuo turno'" :aria-label="`Fai un'offerta per ${g.nome}`" @click="apri(g)">Offri</button>
        </li>
      </ul>
      <p v-else class="muto piccolo">Nessun giocatore corrisponde a «{{ ricerca }}».</p>
    </StatoDati>
  </section>

  <FinestraModale v-if="scelto" titolo="Fai un'offerta" @chiudi="chiudi">
    <form class="offerta" @submit.prevent="conferma">
      <p>Sei sicuro di voler fare un'offerta per <strong>{{ scelto.nome }}</strong>?</p>
      <label class="offerta__campo">
        <span>Prima puntata:</span>
        <input v-model.number="primaPuntata" v-focus type="number" min="0" step="1" class="campo" required />
      </label>
      <p v-if="erroreChiamata" class="offerta__errore" role="alert">{{ erroreChiamata }}</p>
      <div class="offerta__azioni">
        <button type="button" class="btn btn--linea" :disabled="invio" @click="chiudi">Annulla</button>
        <button type="submit" class="btn"
          :disabled="!puntataValida || invio">Fai la puntata</button>
      </div>
    </form>
  </FinestraModale>
</template>

<style scoped>
.disponibili {
  display: flex; flex-direction: column; gap: var(--space-xs); height: 100%; min-height: 0; padding: var(--space-xs);
  background: var(--color-paper-2); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.disponibili__titolo { margin: 0; font-size: var(--text-lg); font-weight: 600; text-align: center; }
.disponibili__ricerca { width: 100%; }
.disponibili__elenco { list-style: none; margin: 0; padding: 0; flex: 1 1 auto; min-height: 0; overflow: auto; font-size: var(--text-sm); }
.disponibili__elenco li {
  display: flex; justify-content: space-between; gap: var(--space-sm); padding: 1px var(--space-xs);
  border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.disponibili__elenco li { align-items: center; }
/* tasto minimo: poco più alto del testo, così le righe restano compatte */
.disponibili__chiama { flex: 0 0 auto; min-height: 0; height: auto; padding: 0 var(--space-xs); font-size: 0.7rem; line-height: 1.5; }
.offerta { display: grid; gap: var(--space-md); }
.offerta p { margin: 0; }
.offerta__errore { color: var(--color-danger, #e5484d); }
.offerta__campo { display: grid; gap: var(--space-2xs, 4px); }
.offerta__azioni { display: flex; justify-content: flex-end; gap: var(--space-xs); }
.disponibili__nome { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.disponibili__elo { font-variant-numeric: tabular-nums; font-weight: 600; }
</style>
