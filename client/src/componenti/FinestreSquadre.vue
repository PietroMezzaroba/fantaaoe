<script setup>
// Le finestre dell'asta: una "finestra" per squadra (nome, capitano, elenco giocatori), quattro per riga.
import { ref, computed, onMounted, onBeforeUnmount, watch, nextTick } from 'vue'
import { api } from '../api.js'
import StatoDati from './StatoDati.vue'

// quattro = true: sempre 4 finestre per riga; se il contenitore è stretto, tutto (testi compresi) si rimpicciolisce
const props = defineProps({ quattro: { type: Boolean, default: false } })
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

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    squadre.value = await api('asta/squadre')
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)

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
      <section v-for="s in squadre" :key="s.id" class="squadra" :aria-labelledby="`squadra-${s.id}`">
        <header class="squadra__testa">
          <p class="squadra__credito" title="Crediti rimasti">
            <strong>{{ s.credito ?? '—' }}</strong>
            <span>crediti</span>
          </p>
          <h2 :id="`squadra-${s.id}`" class="squadra__nome">{{ s.nome }}</h2>
          <p class="squadra__capitano" :class="{ muto: !s.capitano }">{{ s.capitano || 'Nessun capitano' }}</p>
        </header>
        <div class="squadra__colonne" aria-hidden="true">
          <span></span><span>Giocatore</span><span class="num">ELO</span><span class="num">Costo</span>
        </div>
        <ol class="squadra__elenco">
          <li
            v-for="(g, i) in righe(s)"
            :key="i"
            :class="g ? [g.capitano && 'capitano', fascia(i)] : 'vuota'"
          >
            <span class="squadra__numero" aria-hidden="true">{{ i + 1 }}</span>
            <span class="squadra__giocatore" :title="g?.nome">
              <span class="squadra__testo">{{ g?.nome }}</span>
              <span v-if="g?.tu" class="squadra__stella" title="Sei tu" role="img" aria-label="Sei tu">★</span>
            </span>
            <span class="squadra__num">{{ g?.elo }}</span>
            <span class="squadra__num">{{ g?.costo }}</span>
          </li>
        </ol>
        <div class="squadra__totali" :aria-label="`Totali di ${s.nome}: ELO medio ${eloMedio(s)}, costo totale ${costoTotale(s)}`">
          <span></span><span></span>
          <span class="squadra__tot" title="ELO medio">
            <strong>{{ eloMedio(s) }}</strong>
            <span>elo medio</span>
          </span>
          <span class="squadra__tot" title="Costo totale">
            <strong>{{ costoTotale(s) }}</strong>
            <span>costo totale</span>
          </span>
        </div>
      </section>
    </div>
    </div>
  </StatoDati>
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
  min-height: 2rem; padding-block: var(--space-2xs, 4px);
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
</style>
