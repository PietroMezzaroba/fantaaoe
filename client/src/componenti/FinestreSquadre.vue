<script setup>
// Le finestre dell'asta: una "finestra" per squadra (nome, capitano, elenco giocatori), quattro per riga.
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { api, apiPost } from '../api.js'
import StatoDati from './StatoDati.vue'
import FinestraModale from './FinestraModale.vue'
import NumeroVivo from './NumeroVivo.vue'
import { ascoltaAsta } from '../astaLive.js'

// quattro = true: sempre 4 finestre per riga; se il contenitore è stretto, tutto (testi compresi) si rimpicciolisce
// streamer = true: tasto destro su un giocatore acquistato per eliminare l'acquisto (solo a offerta terminata)
const props = defineProps({ quattro: { type: Boolean, default: false }, streamer: Boolean })
const LARGHEZZA_BASE = 1000 // larghezza a cui 4 finestre stanno a grandezza normale
const contenitore = ref(null)
const scala = ref(1)
let osservatore
function misura() {
  if (props.quattro && contenitore.value) scala.value = Math.min(1, contenitore.value.clientWidth / LARGHEZZA_BASE)
}
watch(contenitore, (el) => {
  osservatore?.disconnect()
  if (el && props.quattro) {
    osservatore = new ResizeObserver(misura)
    osservatore.observe(el)
    misura()
  }
})
onBeforeUnmount(() => osservatore?.disconnect())

// righe dell'elenco giocatori (uguali per tutte le squadre): almeno 8, al massimo 12
const RIGHE_MIN = 8
const RIGHE_MAX = 12

const squadre = ref([])
const caricamento = ref(true)
const errore = ref('')

// movimento (movimento.css): la squadra che ha appena comprato si illumina un attimo e il giocatore nuovo entra nella rosa
const squadreAppena = ref(new Set())
const giocatoriNuovi = ref(new Set())
// le righe scorrono solo quando le rose cambiano davvero (acquisto o acquisto eliminato), non quando cambia lo zoom
const animaRighe = ref(false)
let spegniLampi = 0
function segnaNovita(prima, dopo) {
  if (!prima.length) return
  const creditoPrima = new Map(prima.map((s) => [s.id, s.credito]))
  const acquisti = (lista) => lista.flatMap((s) => s.giocatori.map((g) => g.idAcquisto)).filter(Boolean)
  const acquistiPrima = new Set(acquisti(prima))
  const acquistiDopo = acquisti(dopo)
  squadreAppena.value = new Set(dopo.filter((s) => creditoPrima.has(s.id) && s.credito < creditoPrima.get(s.id)).map((s) => s.id))
  giocatoriNuovi.value = new Set(acquistiDopo.filter((id) => !acquistiPrima.has(id)))
  animaRighe.value = giocatoriNuovi.value.size > 0 || acquistiDopo.length !== acquistiPrima.size
  clearTimeout(spegniLampi)
  spegniLampi = setTimeout(() => { squadreAppena.value = new Set(); giocatoriNuovi.value = new Set(); animaRighe.value = false }, 1800)
}
onBeforeUnmount(() => clearTimeout(spegniLampi))

// chiave stabile di una riga: il giocatore resta lo stesso elemento quando la rosa si riordina (così scorre al suo posto);
// le righe vuote sono contate dal fondo, così quando arriva un giocatore sparisce solo la prima vuota
const chiaveRiga = (s, g, i) => (g ? (g.idAcquisto ? `a${g.idAcquisto}` : `c${s.id}-${g.nome}`) : `v${righeComuni.value - 1 - i}`)

async function carica(silenzioso = false) {
  if (!silenzioso) caricamento.value = true
  errore.value = ''
  try {
    const nuove = await api('asta/squadre')
    if (silenzioso) segnaNovita(squadre.value, nuove)
    squadre.value = nuove
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
// Eliminare un acquisto (streamer): tasto destro sul giocatore => "Elimina acquisto" => conferma. Solo a offerta terminata.
const fase = ref('')
async function leggiFase() {
  if (!props.streamer) return
  try {
    fase.value = (await api('asta/puntate')).fase
  } catch {
    /* resta com'era */
  }
}
const puoEliminare = computed(() => fase.value === 'acquisto fatto')
// lo streamer, a offerta terminata, vede il cursore del menu contestuale sui giocatori eliminabili
const puoEliminareRiga = (g) => props.streamer && puoEliminare.value && g.eliminabile
const menu = ref(null) // { x, y, squadra, giocatore }
const conferma = ref(null) // { squadra, giocatore }
const eliminando = ref(false)
const erroreElimina = ref('')
const vFocus = { mounted: (el) => el.focus() }

function suClickDestro(e, squadra, giocatore) {
  if (!props.streamer || !giocatore?.eliminabile) return // altrove resta il menu normale del browser
  e.preventDefault()
  menu.value = { x: e.clientX, y: e.clientY, squadra, giocatore }
}
function chiediConferma() {
  conferma.value = { squadra: menu.value.squadra, giocatore: menu.value.giocatore }
  erroreElimina.value = ''
  menu.value = null
}
async function eliminaAcquisto() {
  eliminando.value = true
  erroreElimina.value = ''
  try {
    await apiPost('asta/elimina-acquisto', { idAcquisto: conferma.value.giocatore.idAcquisto })
    conferma.value = null
  } catch (e) {
    erroreElimina.value = e.message
  } finally {
    eliminando.value = false
  }
}

// quando un'offerta si chiude il giocatore entra nella squadra: il server lo comunica (SignalR), le finestre si aggiornano senza polling
let smettiDiAscoltare = () => {}
onMounted(() => {
  carica()
  smettiDiAscoltare = ascoltaAsta({ suEvento: (e) => {
      if (e.puntate) fase.value = e.puntate.fase
      if (['acquisto', 'eliminato'].includes(e.tipo)) carica(true)
    },
    suRiconnesso: () => { carica(true); leggiFase() },
  })
  leggiFase()
})
onBeforeUnmount(() => smettiDiAscoltare())

// colore della riga in base alla posizione in classifica di squadra (per ELO): 1-3 rosso, 4-6 giallo, dal 7 azzurro
const fascia = (i) => (i < 3 ? 'rosso' : i < 6 ? 'giallo' : 'azzurro')

// totali di una squadra, su tutti i suoi giocatori (capitano compreso): ELO medio (media aritmetica, arrotondata) e somma dei costi
const eloMedio = (squadra) =>
  squadra.giocatori.length ? Math.round(squadra.giocatori.reduce((t, g) => t + g.elo, 0) / squadra.giocatori.length) : '—'
const costoTotale = (squadra) => squadra.giocatori.reduce((t, g) => t + g.costo, 0)

// tutte le finestre hanno lo stesso numero di righe: quante ne servono alla squadra più numerosa (min 8, max 12)
const righeComuni = computed(() =>
  Math.min(RIGHE_MAX, Math.max(RIGHE_MIN, ...squadre.value.map((s) => s.giocatori.length))),
)

// l'elenco di una squadra (capitano compreso), completato con righe vuote fino a righeComuni
function righe(squadra) {
  // ELO decrescente, capitano compreso; a parità resta l'ordine di arrivo (sort stabile)
  const ordinati = [...squadra.giocatori].sort((a, b) => b.elo - a.elo)
  return Array.from({ length: righeComuni.value }, (_, i) => ordinati[i] ?? null)
}
</script>

<template>
  <StatoDati
    :caricamento="caricamento"
    :errore="errore"
    :vuoto="!squadre.length"
    :righe="6"
    testo-vuoto="Non c'è ancora nessuna squadra."
    @riprova="carica"
  >
    <div ref="contenitore" class="squadre-contenitore" :class="{ quattro }"><div class="squadre" :style="quattro ? { zoom: scala } : null">
      <section v-for="s in squadre" :key="s.id" class="squadra" :class="{ appena: squadreAppena.has(s.id) }" :aria-labelledby="`squadra-${s.id}`">
        <header class="squadra__testa">
          <p class="squadra__credito" title="Crediti rimasti">
            <strong><NumeroVivo :valore="s.credito" /></strong>
            <span>crediti</span>
          </p>
          <h2 :id="`squadra-${s.id}`" class="squadra__nome">{{ s.nome }}</h2>
          <p class="squadra__capitano" :class="{ muto: !s.capitano }">{{ s.capitano || 'Nessun capitano' }}</p>
        </header>
        <div class="squadra__colonne" aria-hidden="true">
          <span></span><span>Giocatore</span><span class="num">ELO</span><span class="num">Costo</span>
        </div>
        <TransitionGroup tag="ol" :name="animaRighe ? 'riga' : 'riga-ferma'" class="squadra__elenco">
          <li
            v-for="(g, i) in righe(s)"
            :key="chiaveRiga(s, g, i)"
            :class="g ? [g.capitano && 'capitano', fascia(i), puoEliminareRiga(g) && 'eliminabile', giocatoriNuovi.has(g.idAcquisto) && 'nuovo'] : 'vuota'"
            @contextmenu="suClickDestro($event, s, g)"
          >
            <span class="squadra__numero" aria-hidden="true">{{ i + 1 }}</span>
            <span class="squadra__giocatore" :title="g?.nome">
              <span class="squadra__testo">{{ g?.nome }}</span>
              <span v-if="g?.tu" class="squadra__stella" title="Sei tu" role="img" aria-label="Sei tu">★</span>
            </span>
            <span class="squadra__num">{{ g?.elo }}</span>
            <span class="squadra__num">{{ g?.costo }}</span>
          </li>
        </TransitionGroup>
        <div class="squadra__totali" :aria-label="`Totali di ${s.nome}: ELO medio ${eloMedio(s)}, costo totale ${costoTotale(s)}`">
          <span></span><span></span>
          <span class="squadra__tot" title="ELO medio">
            <strong><NumeroVivo :valore="typeof eloMedio(s) === 'number' ? eloMedio(s) : null" /></strong>
            <span>elo medio</span>
          </span>
          <span class="squadra__tot" title="Costo totale">
            <strong><NumeroVivo :valore="costoTotale(s)" /></strong>
            <span>costo totale</span>
          </span>
        </div>
      </section>
    </div>
    </div>
  </StatoDati>

  <!-- fuori dal contenitore (Teleport): le finestre possono essere rimpicciolite e questi elementi devono restare a grandezza normale -->
  <Teleport to="body">
    <div v-if="menu" class="menu-velo" @click="menu = null" @contextmenu.prevent="menu = null" @keydown.esc="menu = null">
      <ul class="menu-contesto" role="menu" :style="{ left: menu.x + 'px', top: menu.y + 'px' }" @click.stop>
        <li role="none">
          <button
            type="button"
            role="menuitem"
            :disabled="!puoEliminare"
            :title="puoEliminare ? '' : 'Si può eliminare un acquisto solo a offerta terminata (prima di avviare una nuova offerta)'"
            @click="chiediConferma"
          >
            Elimina acquisto
          </button>
        </li>
      </ul>
    </div>
    <FinestraModale v-if="conferma" titolo="Elimina acquisto" @chiudi="conferma = null">
      <div class="conferma-elimina">
        <p>Vuoi eliminare l'acquisto di <strong>{{ conferma.giocatore.nome }}</strong> ({{ conferma.squadra.nome }}, {{ conferma.giocatore.costo }} crediti)?</p>
        <p>
          Il giocatore tornerà disponibile, le sue offerte verranno cancellate e
          <strong>{{ conferma.giocatore.costo }}</strong> crediti saranno restituiti a <strong>{{ conferma.squadra.capitano }}</strong>.
        </p>
        <p v-if="erroreElimina" class="conferma-elimina__errore" role="alert">{{ erroreElimina }}</p>
        <div class="conferma-elimina__azioni">
          <button v-focus type="button" class="btn btn--linea" :disabled="eliminando" @click="conferma = null">Annulla</button>
          <button type="button" class="btn" :disabled="eliminando" @click="eliminaAcquisto">Elimina acquisto</button>
        </div>
      </div>
    </FinestraModale>
  </Teleport>
</template>

<style scoped>
/* quattro finestre per riga, ognuna 1/4 della larghezza del contenitore (al netto degli spazi tra l'una e l'altra) */
.squadre { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--space-md); }
.squadra {
  position: relative; min-width: 0; background: var(--color-paper-2);
  border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card); overflow: hidden;
}
.squadra__testa { padding: var(--space-sm) var(--space-md); border-bottom: var(--rule-hair) solid var(--color-rule-2); text-align: center; }
/* crediti rimasti, in alto a sinistra della finestra */
.squadra__credito {
  position: absolute; top: var(--space-xs); left: var(--space-sm); margin: 0; display: grid; gap: 0; line-height: 1.1; text-align: left;
}
.squadra__credito strong { font-size: var(--text-sm); font-weight: 700; font-variant-numeric: tabular-nums; }
.squadra__credito span { font-size: 0.6rem; color: var(--color-muted); }
.squadra__nome { margin: 0; font-size: var(--text-lg); font-weight: 600; overflow-wrap: anywhere; }
.squadra__capitano { margin: var(--space-3xs, 2px) 0 0; color: var(--color-ink-2); font-size: var(--text-sm); overflow-wrap: anywhere; }
.squadra__elenco { list-style: none; margin: 0; padding: var(--space-2xs, 4px) var(--space-sm) var(--space-sm); font-size: var(--text-sm); }
.squadra__colonne, .squadra__elenco li, .squadra__totali {
  display: grid; grid-template-columns: 1.1rem minmax(0, 1fr) 2.6rem 2.2rem; align-items: baseline; gap: var(--space-2xs, 4px);
}
.squadra__colonne {
  padding: var(--space-xs) var(--space-sm) 0; color: var(--color-muted); font-size: var(--text-xs, 0.75rem);
}
.squadra__colonne span, .squadra__elenco li span, .squadra__totali span { text-align: center; }
.squadra__totali {
  align-items: start; padding: var(--space-xs) var(--space-sm); border-top: var(--rule-hair) solid var(--color-rule-2);
}
/* valore in grassetto con la sua etichetta piccola sotto, come i "crediti" in alto a sinistra */
.squadra__tot { display: grid; gap: 0; line-height: 1.1; justify-items: center; }
.squadra__tot strong { font-size: var(--text-sm); font-weight: 700; font-variant-numeric: tabular-nums; }
.squadra__totali .squadra__tot span { font-size: 0.6rem; color: var(--color-muted); font-weight: 400; text-align: center; overflow-wrap: normal; }
.squadra__num { font-variant-numeric: tabular-nums; }
.squadra__elenco li {
  min-height: 1.35rem; padding-block: 1px;
  border-bottom: var(--rule-hair) dashed var(--color-rule);
}
.squadra__elenco li:last-child { border-bottom: 0; }
/* colori tenui: una velatura sopra lo sfondo, valida anche con il tema chiaro */
.squadra__elenco li.rosso { background: color-mix(in oklch, oklch(64% 0.13 25) 20%, transparent); }
.squadra__elenco li.giallo { background: color-mix(in oklch, oklch(86% 0.14 100) 34%, transparent); }
.squadra__elenco li.azzurro { background: color-mix(in oklch, oklch(72% 0.09 235) 22%, transparent); }
.squadra__elenco li.capitano { font-weight: 700; }
.squadra__numero { color: var(--color-muted); font-size: var(--text-xs, 0.75rem); font-variant-numeric: tabular-nums; }
.squadra__giocatore { min-width: 0; display: flex; justify-content: center; align-items: flex-start; gap: 1px; }
.squadra__testo { min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
/* piccola stella in alto a destra del nome, per il giocatore che è l'utente connesso */
.squadra__stella { flex: 0 0 auto; font-size: 0.62rem; line-height: 1; margin-top: -1px; color: oklch(62% 0.15 75); }

/* il numero di colonne dipende dalla larghezza del contenitore (non dello schermo): 4 finestre per riga se c'è posto */
.squadre-contenitore { container-type: inline-size; }
@container (max-width: 1000px) { .quattro .squadre { grid-template-columns: repeat(4, minmax(0, 1fr)); } .squadre-contenitore:not(.quattro) .squadre { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@container (max-width: 560px) { .squadre-contenitore:not(.quattro) .squadre { grid-template-columns: minmax(0, 1fr); } }

/* giocatore eliminabile (tasto destro): mano al passaggio del mouse e riga verde (il cursore "context-menu" su Windows resta una freccia) */
.squadra__elenco li.eliminabile { cursor: pointer; }
.squadra__elenco li.eliminabile:hover { background: color-mix(in oklch, oklch(72% 0.17 150) 45%, transparent); }

/* menu col tasto destro */
.menu-velo { position: fixed; inset: 0; z-index: var(--z-modal, 100); }
.menu-contesto {
  position: fixed; margin: 0; padding: var(--space-3xs, 2px); list-style: none; min-width: 10rem;
  background: var(--color-paper-2); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
  box-shadow: 0 6px 20px oklch(10% 0.02 255 / 0.35);
}
.menu-contesto button {
  display: block; width: 100%; padding: var(--space-xs) var(--space-sm); text-align: left; font: inherit; color: inherit;
  background: transparent; border: 0; border-radius: calc(var(--radius-card) - 2px); cursor: pointer;
}
.menu-contesto button:hover:not(:disabled) { background: var(--color-paper-3); }
.menu-contesto button:disabled { color: var(--color-muted); cursor: not-allowed; }
.conferma-elimina { display: grid; gap: var(--space-md); }
.conferma-elimina p { margin: 0; }
.conferma-elimina__errore { color: var(--color-danger, #e5484d); }
.conferma-elimina__azioni { display: flex; justify-content: flex-end; gap: var(--space-xs); }

/* ---------- movimento ---------- */
/* la rosa si riordina per ELO: chi cambia posto scivola nella riga nuova */
.riga-move { transition: transform var(--dur-long) var(--ease-out); }
/* il giocatore appena comprato entra da sinistra e la sua riga si accende di verde, poi si spegne */
.riga-enter-active { transition: transform var(--dur-long) var(--ease-out); }
.riga-enter-from { transform: translateX(-16px); }
.squadra__elenco li.nuovo { position: relative; }
.squadra__elenco li.nuovo::after {
  content: ""; position: absolute; inset: 0; pointer-events: none; border-radius: 2px;
  background: var(--color-accent); opacity: 0; animation: riga-accesa 1.6s var(--ease-out) 1;
}
@keyframes riga-accesa { 0% { opacity: 0.55; } 100% { opacity: 0; } }
/* la squadra che ha comprato: un bordo verde che si accende e si spegne */
.squadra.appena::after {
  content: ""; position: absolute; inset: 0; pointer-events: none; border-radius: inherit;
  box-shadow: inset 0 0 0 2px var(--color-accent); opacity: 0; animation: squadra-accesa 1.6s var(--ease-out) 1;
}
@keyframes squadra-accesa { 0%, 30% { opacity: 1; } 100% { opacity: 0; } }
</style>
