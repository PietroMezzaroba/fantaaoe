<script setup>
// Riquadro delle puntate: un capitano per riga, con il nome e fino a 7 spazi per le sue puntate (le ultime 7).
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api, apiPost } from '../api.js'
import StatoDati from './StatoDati.vue'
import { ascoltaAsta } from '../astaLive.js'

// offerte = false: senza l'area "Fai un'offerta" (la pagina di gestione non può fare offerte)
defineProps({ offerte: { type: Boolean, default: true } })

const SPAZI = 7

const capitani = ref([])
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const squadre = await api('asta/squadre')
    capitani.value = squadre
      .filter((s) => s.capitano)
      .map((s) => ({ id: s.id, idCapitano: s.idCapitano, nome: s.capitano }))
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)

// stato della sezione, riempito da refresh(): giocatore in acquisto, puntata massima e puntate dei capitani
const giocatoreInAsta = ref('')
const eloGiocatore = ref(null)
const puntataMassima = ref(0)
const puntatePerCapitano = ref({}) // id capitano -> valori (ultime 7, crescenti)

// Applica lo stato delle puntate: in 'fase di acquisto' mostra il giocatore in acquisto, le puntate dei capitani
// (ultime 7, crescenti) e la puntata massima; altrimenti sezione vuota.
function applica(d) {
  giocatoreInAsta.value = d.giocatore ?? ''
  eloGiocatore.value = d.giocatore ? d.eloGiocatore : null
  puntataMassima.value = d.puntataMassima ?? 0
  puntatePerCapitano.value = Object.fromEntries((d.puntate ?? []).map((p) => [p.idCapitano, p.valori]))
}

// Refresh: rilegge lo stato dal server. Si usa all'apertura, dopo un'offerta (anche scartata) e dopo una riconnessione.
async function refresh() {
  try {
    applica(await api('asta/puntate'))
  } catch {
    /* resta quanto già mostrato: i prossimi eventi del server lo aggiorneranno */
  }
}

// Nessun polling: è il server (SignalR) a mandare lo stato aggiornato quando qualcuno chiama o offre.
let smettiDiAscoltare = () => {}
onMounted(() => {
  refresh()
  smettiDiAscoltare = ascoltaAsta({ suEvento: (e) => applica(e.puntate), suRiconnesso: refresh })
})
onBeforeUnmount(() => smettiDiAscoltare())

// offerta: i tasti mostrano e inviano un importo (puntata massima + 1, + 3, + 5), oppure un importo scritto a mano.
// Il server scarta ogni importo non superiore alla massima del momento; in ogni caso poi si fa il refresh dei dati.
const offerta = ref('')
const offertaValida = computed(() => Number.isInteger(offerta.value) && offerta.value > 0)
const inviando = ref(false)
const erroreOfferta = ref('')
const possoOffrire = computed(() => !!giocatoreInAsta.value && !inviando.value)

async function invia(corpo) {
  if (!possoOffrire.value) return
  inviando.value = true
  erroreOfferta.value = ''
  try {
    await apiPost('asta/offerta', corpo)
    offerta.value = ''
    await refresh()
  } catch (e) {
    erroreOfferta.value = e.message
    await refresh() // ad esempio: nel frattempo la massima è salita, i tasti vanno riallineati
  } finally {
    inviando.value = false
  }
}
// importo mostrato sul tasto: puntata massima attuale + incremento
const importoRialzo = (incremento) => puntataMassima.value + incremento
const rialza = (incremento) => invia({ importo: importoRialzo(incremento) })
const inviaImporto = () => offertaValida.value && invia({ importo: offerta.value })

// gli spazi della riga: le ultime SPAZI puntate, completate con spazi vuoti
const spazi = (c) => {
  const valori = (puntatePerCapitano.value[c.idCapitano] ?? []).slice(-SPAZI)
  return Array.from({ length: SPAZI }, (_, i) => valori[i] ?? null)
}
</script>

<template>
  <section class="puntate" aria-labelledby="puntate-titolo">
    <h2 id="puntate-titolo" class="puntate__titolo">Puntate</h2>
    <StatoDati
      :caricamento="caricamento"
      :errore="errore"
      :vuoto="!capitani.length"
      :righe="6"
      testo-vuoto="Non c'è ancora nessun capitano."
      @riprova="carica"
    >
      <ul class="puntate__elenco">
        <li v-for="c in capitani" :key="c.id">
          <span class="puntate__nome" :title="c.nome">{{ c.nome }}</span>
          <span v-for="(v, i) in spazi(c)" :key="i" class="puntate__spazio" :class="{ vuoto: v === null }">{{ v }}</span>
        </li>
      </ul>
      <dl class="puntate__stato">
        <div><dt>Giocatore:</dt><dd>
          {{ giocatoreInAsta || '—' }}
          <span v-if="giocatoreInAsta" class="puntate__elo" title="ELO">ELO {{ eloGiocatore }}</span>
        </dd></div>
        <div><dt>Puntata massima:</dt><dd>{{ puntataMassima }}</dd></div>
      </dl>
      <div v-if="offerte" class="puntate__offerta" role="group" aria-labelledby="offerta-titolo">
        <p id="offerta-titolo" class="puntate__etichetta">Fai un'offerta</p>
        <div class="puntate__rialzi">
          <button type="button" class="btn" :disabled="!possoOffrire" 
            :title="`+1 sulla puntata massima`" @click="rialza(1)">{{ giocatoreInAsta ? importoRialzo(1) : '—' }}</button>
          <button type="button" class="btn" :disabled="!possoOffrire" 
            :title="`+3 sulla puntata massima`" @click="rialza(3)">{{ giocatoreInAsta ? importoRialzo(3) : '—' }}</button>
          <button type="button" class="btn" :disabled="!possoOffrire" 
            :title="`+5 sulla puntata massima`" @click="rialza(5)">{{ giocatoreInAsta ? importoRialzo(5) : '—' }}</button>
        </div>
        <form class="puntate__libera" @submit.prevent="inviaImporto">
          <input
            v-model.number="offerta"
            type="number"
            min="1"
            step="1"
            class="campo"
            placeholder="Importo"
            aria-label="Importo dell'offerta"
          />
          <button type="submit" class="btn" :disabled="!offertaValida || !possoOffrire">Ok</button>
        </form>
        <p v-if="erroreOfferta" class="puntate__errore" role="alert">{{ erroreOfferta }}</p>
      </div>
    </StatoDati>
  </section>
</template>

<style scoped>
.puntate {
  display: flex; flex-direction: column; gap: var(--space-sm); height: 100%; min-height: 0; padding: var(--space-sm);
  background: var(--color-paper-2); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.puntate__titolo { margin: 0; font-size: var(--text-lg); font-weight: 600; text-align: center; }
.puntate__elenco { list-style: none; margin: 0; padding: 0; flex: 0 1 auto; min-height: 0; overflow: auto; font-size: var(--text-sm); }
.puntate__elenco li {
  display: grid; grid-template-columns: minmax(6rem, 1.4fr) repeat(7, minmax(0, 1fr)); align-items: center; gap: 3px;
  padding: var(--space-2xs, 4px) var(--space-xs); border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.puntate__stato { margin: 0; padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); display: grid; gap: var(--space-xs); font-size: var(--text-md); }
.puntate__stato div { display: flex; align-items: baseline; gap: var(--space-sm); }
.puntate__stato dt { color: var(--color-ink-2); }
.puntate__elo { margin-left: var(--space-xs); font-size: var(--text-sm); font-weight: 400; color: var(--color-ink-2); }
.puntate__stato dd { margin: 0; font-weight: 700; font-variant-numeric: tabular-nums; }
.puntate__offerta { display: grid; gap: var(--space-sm); padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); }
.puntate__errore { margin: 0; color: var(--color-danger, #e5484d); font-size: var(--text-sm); }
.puntate__etichetta { margin: 0; font-size: var(--text-md); color: var(--color-ink-2); }
.puntate__rialzi { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--space-sm); }
.puntate__libera { display: flex; gap: var(--space-sm); }
.puntate__libera .campo { flex: 1 1 auto; min-width: 0; }
.puntate__nome { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.puntate__spazio {
  text-align: center; padding: 1px 0; font-size: var(--text-xs, 0.75rem); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
  background: var(--color-paper); font-variant-numeric: tabular-nums; min-height: 1.6em;
}
.puntate__spazio.vuoto { border-style: dashed; background: transparent; }
</style>
