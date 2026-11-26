<script setup>
// Riquadro delle puntate: un capitano per riga, con il nome e fino a 7 spazi per le sue puntate (le ultime 7).
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { api, apiPost } from '../api.js'
import StatoDati from './StatoDati.vue'
import FinestraModale from './FinestraModale.vue'
import NumeroVivo from './NumeroVivo.vue'
import { sessione } from '../sessione.js'
import { ascoltaAsta, presenzaPagina } from '../astaLive.js'

// offerte = false: senza l'area "Fai un'offerta" (la pagina di gestione non può fare offerte)
// streamer = true: area con "Termina offerta" e "Avvia nuova offerta" (pagina di gestione)
defineProps({ offerte: { type: Boolean, default: true }, streamer: Boolean })

const SPAZI = 7

const capitani = ref([])
const caricamento = ref(true)
const errore = ref('')

async function carica(silenzioso = false) {
  if (!silenzioso) caricamento.value = true
  errore.value = ''
  try {
    const squadre = await api('asta/squadre')
    capitani.value = squadre
      .filter((s) => s.capitano)
      .map((s) => ({ id: s.id, idCapitano: s.idCapitano, nome: s.capitano, credito: s.credito, ordine: s.ordine }))
      // in ordine di flusso dell'asta (asta_ordine_capitani.ordine); chi non ha un ordine (0) va in fondo
      .sort((a, b) => (a.ordine || Infinity) - (b.ordine || Infinity) || a.id - b.id)
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(() => carica())

// stato della sezione, riempito da refresh(): giocatore in acquisto, puntata massima e puntate dei capitani
const fase = ref('') // fase_flusso_asta
const idChiamante = ref(null) // capitano che ha fatto la prima offerta (stella)
const idMigliore = ref(null) // capitano con la puntata più alta (ultima puntata in rosso)
// la stella: prima della chiamata indica il capitano di turno, poi chi ha fatto la prima offerta
const motivoStella = computed(() => (fase.value === 'chiamata_giocatore' ? 'Capitano di turno' : 'Ha fatto la prima offerta'))
const residuoMs = ref(null) // timer in pausa: tempo rimasto (ms); null = non in pausa
const scadenzaMs = ref(null) // quando scade il timer (ora del server, ms Unix); null = timer non attivo
const scarto = ref(0) // differenza tra l'orologio del server e quello di questo browser
const adesso = ref(Date.now())
const log = ref([]) // ultimi fatti dell'asta (dal più recente)
const connessi = ref(new Set()) // capitani connessi alla pagina (cerchio verde/rosso)
const giocatoreInAsta = ref('')
const eloGiocatore = ref(null)
const puntataMassima = ref(0)
const puntatePerCapitano = ref({}) // id capitano -> valori (ultime 7, crescenti)

// Applica lo stato delle puntate: in 'fase di acquisto' mostra il giocatore in acquisto, le puntate dei capitani
// (ultime 7, crescenti) e la puntata massima; altrimenti sezione vuota.
function applica(d) {
  fase.value = d.fase ?? ''
  idChiamante.value = d.idChiamante ?? null
  log.value = d.log ?? []
  scadenzaMs.value = d.scadenzaMs ?? null
  residuoMs.value = d.residuoMs ?? null
  if (d.oraServerMs) scarto.value = d.oraServerMs - Date.now()
  idMigliore.value = d.idMigliore ?? null
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
let esciDallaPagina = () => {}
onMounted(() => {
  refresh()
  smettiDiAscoltare = ascoltaAsta({ suEvento: (e) => {
    applica(e.puntate)
    if (e.tipo === 'acquisto') carica(true) // chi ha comprato ha meno crediti
  }, suRiconnesso: refresh, suPresenze: (ids) => { connessi.value = new Set(ids) } })
  esciDallaPagina = presenzaPagina() // da qui in poi il capitano risulta connesso (cerchio verde)
})
onBeforeUnmount(() => {
  esciDallaPagina() // il capitano non è più sulla pagina: torna rosso per gli altri
  smettiDiAscoltare()
})

// Timer a video: conto alla rovescia in secondi:centesimi. Gira solo mentre il timer è attivo; la scadenza vera la gestisce il server.
let ticker = null
watch(scadenzaMs, (v) => {
  clearInterval(ticker)
  ticker = null
  if (v) {
    adesso.value = Date.now()
    ticker = setInterval(() => { adesso.value = Date.now() }, 30)
  }
}, { immediate: true })
onBeforeUnmount(() => clearInterval(ticker))

const inPausa = computed(() => residuoMs.value !== null)
const timerAttivo = computed(() => scadenzaMs.value !== null || inPausa.value) // in pausa il tempo si vede, fermo
const restanteMs = computed(() => (inPausa.value ? residuoMs.value : Math.max(0, scadenzaMs.value - (adesso.value + scarto.value))))
// <secondi a 2 cifre>:<centesimi a 2 cifre>; senza timer attivo, un trattino
const timerTesto = computed(() => {
  if (!timerAttivo.value) return '-'
  const ms = restanteMs.value
  const secondi = Math.floor(ms / 1000)
  const centesimi = Math.floor((ms % 1000) / 10)
  return `${String(secondi).padStart(2, '0')}:${String(centesimi).padStart(2, '0')}`
})
const timerInScadenza = computed(() => timerAttivo.value && !inPausa.value && restanteMs.value < 10000)

// anello del timer: si svuota col tempo. La durata piena non arriva dal server: è quella che il timer aveva
// quando è ripartito (a ogni chiamata e a ogni offerta si riporta a timer_asta_secondi)
// aveva quando è ripartito, cioè la più lunga vista (dopo una pausa riparte dal residuo, che è più corto)
const durataMs = ref(0)
watch(scadenzaMs, (v) => {
  const r = v ? v - (Date.now() + scarto.value) : 0
  if (r > durataMs.value) durataMs.value = r
})
const CIRCONFERENZA = 2 * Math.PI * 21
const anelloScarto = computed(() => {
  if (!timerAttivo.value) return CIRCONFERENZA
  const resto = durataMs.value ? Math.min(1, Math.max(0, restanteMs.value / durataMs.value)) : 1
  return CIRCONFERENZA * (1 - resto)
})

// Riquadro "stato dell'asta": in cima, se tocca a un capitano chiamare, di chi è il turno; poi i fatti recenti
const ora = (quando) => new Date(quando).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
const messaggi = computed(() => {
  const m = []
  if (inPausa.value) m.push({ chiave: 'pausa', evidenza: true, testo: 'Timer in pausa' })
  // offerta chiusa: si attende che lo streamer prema "Avvia nuova offerta"
  if (fase.value === 'acquisto fatto') m.push({ chiave: 'attesa', evidenza: true, testo: 'In attesa che l\'Admin dia il via alla prossima chiamata' })
  if (fase.value === 'chiamata_giocatore' && idChiamante.value) {
    const di = capitani.value.find((x) => x.idCapitano === idChiamante.value)
    if (di) m.push({ chiave: 'turno', evidenza: true, testo: `${di.nome} deve fare la sua chiamata` })
  }
  for (const l of log.value) {
    const testo = {
      chiamata: `${l.capitano} ha chiamato ${l.giocatore} con una prima offerta da ${l.valore}`,
      offerta: `${l.capitano} ha offerto ${l.valore} per ${l.giocatore}`,
      acquisto: `${l.capitano} si è aggiudicato ${l.giocatore} per ${l.valore}`,
    }[l.tipo]
    if (testo) m.push({ chiave: `${l.tipo}-${l.quando}-${l.capitano}-${l.valore}`, testo, quando: ora(l.quando), tipo: l.tipo })
  }
  return m
})

// offerta: i tasti mostrano e inviano un importo (puntata massima + 1, + 3, + 5), oppure un importo scritto a mano.
// Il server scarta ogni importo non superiore alla massima del momento; in ogni caso poi si fa il refresh dei dati.
const offerta = ref('')
// il proprio credito (se si è un capitano): è il massimo che si può offrire
const mioCredito = computed(() => capitani.value.find((c) => c.idCapitano === sessione.discordId)?.credito ?? null)
const offertaMinima = computed(() => puntataMassima.value + 1)
const offertaMassima = computed(() => mioCredito.value)
const offertaValida = computed(
  () => Number.isInteger(offerta.value) && offerta.value >= offertaMinima.value && (offertaMassima.value === null || offerta.value <= offertaMassima.value),
)
const suggerimentoOfferta = computed(() => (offertaMassima.value === null ? `da ${offertaMinima.value}` : `da ${offertaMinima.value} a ${offertaMassima.value}`))
// un tasto di rialzo oltre il proprio credito non si può usare
const rialzoPossibile = (incremento) => offertaMassima.value === null || importoRialzo(incremento) <= offertaMassima.value
const inviando = ref(false)
const erroreOfferta = ref('')
const inFaseAcquisto = computed(() => fase.value === 'fase di acquisto')
// le offerte sono aperte solo in 'fase di acquisto' e col timer che gira (non in pausa)
const offerteAperte = computed(() => inFaseAcquisto.value && !inPausa.value)
const possoOffrire = computed(() => offerteAperte.value && !inviando.value)

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
// streamer: chiude l'offerta (giocatore alla squadra del miglior offerente) o ne avvia una nuova
const operando = ref(false)
const erroreStreamer = ref('')
async function operazione(percorso) {
  operando.value = true
  erroreStreamer.value = ''
  try {
    await apiPost(percorso, {})
    await refresh()
  } catch (e) {
    erroreStreamer.value = e.message
    await refresh()
  } finally {
    operando.value = false
  }
}
// "Termina offerta" chiede conferma: è un'azione che assegna il giocatore e fa pagare il capitano
const confermaTermina = ref(false)
const vFocus = { mounted: (el) => el.focus() }
const miglioreNome = computed(() => capitani.value.find((c) => c.idCapitano === idMigliore.value)?.nome ?? null)
async function terminaOfferta() {
  confermaTermina.value = false
  await operazione('asta/termina-offerta')
}
const nuovaOfferta = () => operazione('asta/nuova-offerta')
const pausaTimer = () => operazione(inPausa.value ? 'asta/riprendi-timer' : 'asta/pausa-timer')

const rialza = (incremento) => invia({ importo: importoRialzo(incremento) })
const inviaImporto = () => offertaValida.value && invia({ importo: offerta.value })

// gli spazi della riga: le ultime SPAZI puntate, completate con spazi vuoti
// ogni spazio: { valore, rosso }; è rosso l'ultima puntata del capitano con la puntata più alta
const spazi = (c) => {
  const valori = (puntatePerCapitano.value[c.idCapitano] ?? []).slice(-SPAZI)
  const rosso = c.idCapitano === idMigliore.value ? valori.length - 1 : -1
  return Array.from({ length: SPAZI }, (_, i) => ({ valore: valori[i] ?? null, rosso: i === rosso }))
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
          <span class="puntate__nome" :title="c.nome">
            <span
              class="puntate__presenza"
              :class="connessi.has(c.idCapitano) ? 'on' : 'off'"
              role="img"
              :aria-label="connessi.has(c.idCapitano) ? 'Connesso' : 'Non connesso'"
              :title="connessi.has(c.idCapitano) ? 'Connesso' : 'Non connesso'"
            ></span>
            <span class="puntate__testo">{{ c.nome }}</span>
            <span v-if="c.idCapitano === idChiamante" class="puntate__stella" role="img" :aria-label="motivoStella" :title="motivoStella">★</span>
          </span>
          <TransitionGroup name="puntata">
            <span v-for="(s, i) in spazi(c)" :key="`${i}-${s.valore}`" class="puntate__spazio" :class="{ vuoto: s.valore === null, migliore: s.rosso }">{{ s.valore }}</span>
          </TransitionGroup>
        </li>
      </ul>
      <div class="puntate__riepilogo">
      <dl class="puntate__stato">
        <div><dt>Giocatore:</dt><dd>
          {{ giocatoreInAsta || '—' }}
          <span v-if="giocatoreInAsta" class="puntate__elo" title="ELO">ELO {{ eloGiocatore }}</span>
        </dd></div>
        <div><dt>Puntata massima:</dt><dd><NumeroVivo :valore="puntataMassima" :durata="400" /></dd></div>
      </dl>
      <div class="puntate__timer" :class="{ attivo: timerAttivo, urgente: timerInScadenza, pausa: inPausa }" role="timer" aria-label="Tempo rimasto">
        <svg class="puntate__timer-anello" viewBox="0 0 48 48" width="2.2em" height="2.2em" aria-hidden="true">
          <circle class="puntate__timer-traccia" cx="24" cy="24" r="21" />
          <circle
            class="puntate__timer-resto"
            cx="24" cy="24" r="21"
            :stroke-dasharray="CIRCONFERENZA"
            :stroke-dashoffset="anelloScarto"
            transform="rotate(-90 24 24)"
          />
        </svg>
        <span class="puntate__timer-testo">{{ timerTesto }}</span>
      </div>
      </div>
      <div v-if="streamer" class="puntate__offerta" role="group" aria-labelledby="streamer-titolo">
        <p id="streamer-titolo" class="puntate__etichetta">Gestione offerta</p>
        <div class="puntate__streamer">
          <button type="button" class="btn" :disabled="!inFaseAcquisto || operando" @click="confermaTermina = true">Termina offerta</button>
          <button type="button" class="btn" :disabled="!inPausa && scadenzaMs === null || operando" @click="pausaTimer">
            {{ inPausa ? 'Riprendi il timer' : 'Metti in pausa' }}
          </button>
          <button type="button" class="btn" :disabled="fase !== 'acquisto fatto' || operando" @click="nuovaOfferta">Avvia nuova offerta</button>
        </div>
        <p v-if="erroreStreamer" class="puntate__errore" role="alert">{{ erroreStreamer }}</p>
      </div>
      <div v-if="offerte" class="puntate__offerta" role="group" aria-labelledby="offerta-titolo">
        <p id="offerta-titolo" class="puntate__etichetta">Fai un'offerta</p>
        <div class="puntate__rialzi">
          <button type="button" class="btn" :disabled="!possoOffrire || !rialzoPossibile(1)" 
            :title="`+1 sulla puntata massima`" @click="rialza(1)">{{ inFaseAcquisto ? importoRialzo(1) : '—' }}</button>
          <button type="button" class="btn" :disabled="!possoOffrire || !rialzoPossibile(3)" 
            :title="`+3 sulla puntata massima`" @click="rialza(3)">{{ inFaseAcquisto ? importoRialzo(3) : '—' }}</button>
          <button type="button" class="btn" :disabled="!possoOffrire || !rialzoPossibile(5)" 
            :title="`+5 sulla puntata massima`" @click="rialza(5)">{{ inFaseAcquisto ? importoRialzo(5) : '—' }}</button>
        </div>
        <form class="puntate__libera" @submit.prevent="inviaImporto">
          <input
            v-model.number="offerta"
            type="number"
            :min="offertaMinima"
            :max="offertaMassima ?? undefined"
            step="1"
            class="campo"
            :placeholder="suggerimentoOfferta"
            :title="`Offerta ${suggerimentoOfferta}`"
            aria-label="Importo dell'offerta"
            :disabled="!offerteAperte"
          />
          <button type="submit" class="btn" :disabled="!offertaValida || !possoOffrire">Ok</button>
        </form>
        <p v-if="erroreOfferta" class="puntate__errore" role="alert">{{ erroreOfferta }}</p>
      </div>
      <div class="puntate__fatti" aria-labelledby="fatti-titolo">
        <p id="fatti-titolo" class="puntate__etichetta">Stato dell'asta</p>
        <TransitionGroup tag="ul" name="fatto" class="puntate__fatti-elenco" aria-live="polite">
          <li v-for="m in messaggi" :key="m.chiave" :class="[m.tipo, { evidenza: m.evidenza }]">
            <span>{{ m.testo }}</span>
            <time v-if="m.quando">{{ m.quando }}</time>
          </li>
          <li v-if="!messaggi.length" key="vuoto" class="vuoto">Nessuna attività per ora.</li>
        </TransitionGroup>
      </div>
    </StatoDati>
  </section>

  <FinestraModale v-if="confermaTermina" titolo="Termina offerta" @chiudi="confermaTermina = false">
    <div class="conferma">
      <p>Vuoi terminare l'offerta?</p>
      <p v-if="giocatoreInAsta && miglioreNome">
        <strong>{{ giocatoreInAsta }}</strong> verrà assegnato a <strong>{{ miglioreNome }}</strong> per <strong>{{ puntataMassima }}</strong>.
      </p>
      <div class="conferma__azioni">
        <button v-focus type="button" class="btn btn--linea" @click="confermaTermina = false">Annulla</button>
        <button type="button" class="btn" @click="terminaOfferta">Termina offerta</button>
      </div>
    </div>
  </FinestraModale>
</template>

<style scoped>
.puntate {
  display: flex; flex-direction: column; gap: var(--space-sm); height: 100%; min-height: 0; padding: var(--space-sm);
  background: var(--color-paper-2); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.puntate__titolo { margin: 0; font-size: var(--text-lg); font-weight: 600; text-align: center; }
.puntate__elenco { list-style: none; margin: 0; padding: 0; flex: 0 1 auto; min-height: 0; overflow: auto; font-size: var(--text-sm); }
.puntate__elenco li {
  display: grid; grid-template-columns: minmax(9rem, 1.8fr) repeat(7, minmax(0, 1fr)); align-items: center; gap: 3px;
  padding: var(--space-2xs, 4px) var(--space-xs); border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.puntate__riepilogo { display: flex; align-items: center; justify-content: space-between; gap: var(--space-md); padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); }
.puntate__stato { margin: 0; display: grid; gap: var(--space-xs); font-size: var(--text-md); min-width: 0; }
/* timer a destra, con il disegno del cronometro; spento (trattino) quando non c'è un'offerta in corso */
.puntate__timer { flex: 0 0 auto; display: flex; align-items: center; gap: var(--space-xs); color: var(--color-muted); }
.puntate__timer.attivo { color: var(--color-ink); }
.puntate__timer.pausa { color: oklch(62% 0.15 75); }
.puntate__timer.urgente { color: oklch(55% 0.22 25); }
.puntate__timer-testo { font-size: 1.6rem; font-weight: 700; font-variant-numeric: tabular-nums; line-height: 1; min-width: 2ch; text-align: center; }
.puntate__stato div { display: flex; align-items: baseline; gap: var(--space-sm); }
.puntate__stato dt { color: var(--color-ink-2); }
.puntate__elo { margin-left: var(--space-xs); font-size: var(--text-sm); font-weight: 400; color: var(--color-ink-2); }
.puntate__stato dd { margin: 0; font-weight: 700; font-variant-numeric: tabular-nums; }
.puntate__offerta { display: grid; gap: var(--space-sm); padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); }
/* tasti non utilizzabili (offerte, streamer): grigi, non verdi, e senza effetto al passaggio del mouse */
.puntate .btn:disabled, .puntate .btn:disabled:hover {
  background: var(--color-paper-3); border-color: var(--color-rule-2); color: var(--color-muted);
  opacity: 1; cursor: not-allowed; box-shadow: none;
}
.puntate__fatti { display: grid; gap: var(--space-xs); padding-top: var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2); min-height: 0; }
.puntate__fatti-elenco { list-style: none; margin: 0; padding: 0; max-height: 9rem; overflow: auto; font-size: var(--text-sm); }
.puntate__fatti-elenco li {
  display: flex; justify-content: space-between; gap: var(--space-sm); padding: 2px var(--space-xs);
  border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.puntate__fatti-elenco li.evidenza { font-weight: 700; background: color-mix(in oklch, oklch(86% 0.14 100) 34%, transparent); }
.puntate__fatti-elenco li.acquisto { font-weight: 600; }
.puntate__fatti-elenco li.vuoto { color: var(--color-muted); }
.puntate__fatti-elenco time { flex: 0 0 auto; color: var(--color-muted); font-variant-numeric: tabular-nums; }
.conferma { display: grid; gap: var(--space-md); }
.conferma p { margin: 0; }
.conferma__azioni { display: flex; justify-content: flex-end; gap: var(--space-xs); }
.puntate__errore { margin: 0; color: var(--color-danger, #e5484d); font-size: var(--text-sm); }
.puntate__etichetta { margin: 0; font-size: var(--text-md); color: var(--color-ink-2); }
.puntate__streamer { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--space-sm); }
.puntate__rialzi { display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--space-sm); }
.puntate__libera { display: flex; gap: var(--space-sm); }
.puntate__libera .campo { flex: 1 1 auto; min-width: 0; }
/* fuori dalla fase di offerta (o in pausa) la textbox è spenta, come i tasti */
.puntate__libera .campo:disabled { background: var(--color-paper-3); color: var(--color-muted); cursor: not-allowed; opacity: 1; }
.puntate__nome { min-width: 0; display: flex; align-items: center; gap: var(--space-xs); font-weight: 600; }
.puntate__testo { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
/* cerchio: verde se il capitano è connesso alla pagina, rosso se no */
.puntate__presenza { flex: 0 0 auto; width: 0.7em; height: 0.7em; border-radius: 50%; }
.puntate__presenza.on { background: oklch(66% 0.17 150); }
.puntate__presenza.off { background: oklch(60% 0.2 25); }
/* stella grande come il testo, accanto al capitano che ha fatto la prima offerta */
.puntate__stella { flex: 0 0 auto; font-size: 1.1em; line-height: 1; color: oklch(72% 0.16 85); }
.puntate__spazio {
  text-align: center; padding: 1px 0; font-size: var(--text-xs, 0.75rem); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
  background: var(--color-paper); font-variant-numeric: tabular-nums; min-height: 1.6em;
}
/* la puntata più alta: ultima puntata del capitano che la detiene */
.puntate__spazio.migliore { background: oklch(58% 0.2 25); border-color: oklch(48% 0.2 25); color: white; font-weight: 700; }
.puntate__spazio.vuoto { border-style: dashed; background: transparent; }

/* ---------- movimento ---------- */
/* la puntata rossa passa da una casella all'altra sfumando */
.puntate__spazio { transition: background-color var(--dur-short) var(--ease-out), color var(--dur-short) var(--ease-out), border-color var(--dur-short) var(--ease-out); }
/* una puntata nuova entra con un piccolo scatto (le caselle vuote spariscono subito) */
.puntata-enter-active { transition: transform var(--dur-long) var(--ease-out); }
.puntata-enter-from { transform: scale(0.55); }
/* i fatti nuovi scendono dall'alto, gli altri scivolano sotto */
.fatto-enter-active { transition: transform var(--dur-long) var(--ease-out); }
.fatto-enter-from { transform: translateY(-8px); }
.fatto-move { transition: transform var(--dur-long) var(--ease-out); }
/* il cerchio di chi si collega fa un'onda, una volta */
.puntate__presenza { position: relative; transition: background-color var(--dur-short) var(--ease-out); }
.puntate__presenza.on::after {
  content: ""; position: absolute; inset: 0; border-radius: 50%;
  box-shadow: 0 0 0 2px var(--color-accent); opacity: 0; animation: presenza-onda 900ms var(--ease-out) 1;
}
@keyframes presenza-onda { from { opacity: 0.9; transform: scale(1); } to { opacity: 0; transform: scale(2.4); } }
/* anello del timer: la traccia e il tempo che resta */
.puntate__timer-anello { flex: 0 0 auto; }
.puntate__timer-traccia, .puntate__timer-resto { fill: none; stroke-width: 4; }
.puntate__timer-traccia { stroke: var(--color-rule-2); }
.puntate__timer-resto { stroke: currentColor; stroke-linecap: round; }
.puntate__timer:not(.attivo) .puntate__timer-resto { opacity: 0; }
/* sotto i 10 secondi il numero batte a ogni secondo */
.puntate__timer.urgente .puntate__timer-testo { animation: timer-batte 1s var(--ease-out) infinite; }
@keyframes timer-batte { 0% { transform: scale(1.08); } 40%, 100% { transform: scale(1); } }
</style>
