<script setup>
import { ref, computed, onMounted } from 'vue'
import { api } from '../api.js'
import { sessione, impostazioniPredefinite } from '../sessione.js'
import NuvolaElmo from '../componenti/NuvolaElmo.vue'
import UnitaAoe from '../componenti/UnitaAoe.vue'
import PaladinoAoe from '../componenti/PaladinoAoe.vue'
import NumeroVivo from '../componenti/NumeroVivo.vue'
import ConfermaIscrizione from '../componenti/ConfermaIscrizione.vue'

// Fatti dall'handbook del Fanta Age 2025 (5ª edizione) e dai fogli 2024 e 2025.
const HANDBOOK = 'https://docs.google.com/document/d/1l6GwnV2-LH0DC-zBCRcFXmZbd14KPQopuweMsNGHitM/edit?usp=sharing'

const fasce = [
  ['A', 'I tre seed più alti della rosa'],
  ['B', 'I tre successivi, sostituiscono la A'],
  ['C', 'Gli altri quattro, sostituiscono tutti'],
]
const punti = [
  ['3–0', 4],
  ['2–1', 3],
  ['1–2', 2],
  ['0–3', 1],
]
const calendario2025 = [
  ['12 ottobre', 'Chiusura iscrizioni'],
  ['16 ottobre', 'Asta dei capitani'],
  ['23 ottobre – 19 novembre', 'Fase a gironi, in due turni'],
  ['20 novembre – 3 dicembre', 'Quarti e semifinali'],
  ['Dal 4 dicembre', 'Finale'],
]
const albo = [
  ['2025', 'Team Pier', 'finale 5–3 con Team Apo'],
  ['2024', 'Team Carma', 'finale 6–3 con Team Scranton'],
]

// Animazione dell'elmo (profilo: animazione_iniziale). null = impostazioni ancora in arrivo, l'elmo aspetta.
const animazioneElmo = computed(() =>
  sessione.caricata ? (sessione.impostazioni?.animazioneIniziale ?? impostazioniPredefinite.animazioneIniziale) : null)

// Iscrizione: serve essere connessi; per ora la conferma chiude soltanto la form
const confermaAperta = ref(false)

const stat = ref(null)
const errore = ref(false)
onMounted(async () => {
  try {
    stat.value = await api('statistiche')
  } catch {
    errore.value = true
  }
})

const n = (v) => (v == null ? 'n.d.' : Number(v).toLocaleString('it-IT'))
const ore = computed(() => (stat.value ? Math.round(stat.value.numeri.minutiTotali / 60) : null))
const anno = computed(() => (stat.value?.numeri.primaPartita ? new Date(stat.value.numeri.primaPartita).getFullYear() : null))
const barre = computed(() => {
  const s = stat.value?.settimane ?? []
  const max = Math.max(1, ...s.map((x) => x.partite))
  return s.map((x) => ({ ...x, h: x.partite / max }))
})
const descrizioneBarre = computed(() => {
  const s = stat.value?.settimane ?? []
  if (!s.length) return ''
  const max = Math.max(...s.map((x) => x.partite))
  return `Partite per settimana nelle ultime ${s.length} settimane, da 0 a ${max}.`
})
</script>

<template>
  <section class="apertura" aria-labelledby="titolo-apertura">
    <div class="container apertura__griglia">
      <div class="apertura__testo">
        <h1 id="titolo-apertura">FantaAge</h1>
        <p class="lead">Il torneo a squadre di AoE Italia. I capitani comprano la rosa all'asta.</p>
        <a :href="HANDBOOK" class="btn btn--linea btn--grande" target="_blank" rel="noopener">Il regolamento</a>
        <p class="apertura__quando">
          Sesta edizione: da metà dicembre 2026, date precise in arrivo
        </p>
        <p class="lead">
          Vuoi iscriverti a questo fantastico torneo?
        </p>
        <button
          type="button"
          class="btn btn--linea btn--grande btn--a-capo"
          :disabled="!sessione.autenticato"
          @click="confermaAperta = true"
        >
          {{ !sessione.caricata || sessione.autenticato ? 'Iscriviti!' : 'Per iscriversi devi prima loggarti' }}
        </button>
      </div>
      <div class="apertura__scena">
        <NuvolaElmo :centro-x="0.5" :quota="0.95" :animata="animazioneElmo" />
      </div>
    </div>
  </section>

  <ConfermaIscrizione :aperta="confermaAperta" @chiudi="confermaAperta = false" />

  <div class="container">
    <section class="riga" aria-labelledby="titolo-numeri">
      <div class="riga__testo">
        <h2 id="titolo-numeri">Qui si gioca davvero</h2>
        <p>Le partite di squadra della community, le serate TG Dito, registrate una per una<template v-if="anno"> dal {{ anno }}</template>.</p>
        <RouterLink to="/partite" class="btn btn--linea">Le ultime partite</RouterLink>
      </div>
      <div class="riga__prova">
        <p v-if="errore" class="muto">I numeri non arrivano in questo momento. Riprova tra poco.</p>
        <template v-else>
          <dl class="numeri">
            <div class="numero angoli">
              <dt>Giocatori attivi negli ultimi 90 giorni</dt>
              <dd class="num"><NumeroVivo :valore="stat?.numeri.attivi90 ?? null" alla-vista :durata="900" /></dd>
              <small>su {{ n(stat?.numeri.giocatoriTotali) }} passati dal server</small>
            </div>
            <div class="numero">
              <dt>Partite nell'ultimo mese</dt>
              <dd class="num"><NumeroVivo :valore="stat?.numeri.partite30 ?? null" alla-vista :durata="900" /></dd>
              <small>{{ n(stat?.numeri.partite7) }} negli ultimi sette giorni</small>
            </div>
            <div class="numero">
              <dt>Partite registrate</dt>
              <dd class="num"><NumeroVivo :valore="stat?.numeri.partiteValide ?? null" alla-vista :durata="900" /></dd>
              <small>l'ultima è il TG Dito {{ n(stat?.numeri.ultimoDito) }}</small>
            </div>
            <div class="numero">
              <dt>Ore di gioco</dt>
              <dd class="num"><NumeroVivo :valore="ore" alla-vista :durata="900" /></dd>
              <small>contando solo il tempo in partita</small>
            </div>
          </dl>
          <figure v-if="barre.length" class="settimane">
            <svg viewBox="0 0 320 64" width="100%" role="img" :aria-label="descrizioneBarre" preserveAspectRatio="none">
              <rect
                v-for="(b, i) in barre"
                :key="b.inizio"
                class="settimane__barra"
                :style="{ '--i': i }"
                :x="i * 20 + 2"
                :y="62 - Math.max(1, b.h * 58)"
                width="14"
                :height="Math.max(1, b.h * 58)"
                :fill="i === barre.length - 1 ? 'var(--color-accent)' : 'var(--color-ink-2)'"
                :opacity="b.partite ? 1 : 0.35"
              />
            </svg>
            <figcaption class="muto piccolo">Partite per settimana, le ultime {{ barre.length }}; in verde quella in corso.</figcaption>
          </figure>
        </template>
      </div>
    </section>

    <section class="riga riga--inversa" aria-labelledby="titolo-fasce">
      <div class="riga__testo">
        <h2 id="titolo-fasce">Dieci giocatori, tre fasce</h2>
        <p>Il seeding parte dall'ELO 1v1 più alto di ogni giocatore. Ogni fascia gioca i suoi 3vs3 al meglio delle tre partite, e i punti finiscono in un'unica classifica.</p>
        <RouterLink to="/classifica" class="btn btn--linea">Vedi gli ELO</RouterLink>
        <UnitaAoe tipo="duello" didascalia="Milizie in campo · clicca per il colpo" />
      </div>
      <div class="riga__prova fasce-punti">
        <ul class="fasce">
          <li v-for="[lettera, testo] in fasce" :key="lettera">
            <span class="fasce__lettera">{{ lettera }}</span>
            <span>{{ testo }}</span>
          </li>
        </ul>
        <table class="punti">
          <caption class="muto piccolo">Punti per incontro</caption>
          <thead><tr><th scope="col">Risultato</th><th scope="col" class="num">Punti</th></tr></thead>
          <tbody>
            <tr v-for="[ris, p] in punti" :key="ris"><td>{{ ris }}</td><td class="num">{{ p }}</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="riga" aria-labelledby="titolo-2025">
      <div class="riga__testo">
        <h2 id="titolo-2025">Com'è andata nel 2025</h2>
        <p>Quinta edizione, nove squadre, nove mappe del pack di Tiniae.</p>
        <ul class="albo">
          <li v-for="[a, squadra, nota] in albo" :key="a">
            <span class="albo__anno num">{{ a }}</span>
            <strong>{{ squadra }}</strong>
            <span class="muto">{{ nota }}</span>
          </li>
        </ul>
        <PaladinoAoe />
      </div>
      <ol class="calendario riga__prova">
        <li v-for="[quando, cosa] in calendario2025" :key="cosa">
          <span class="calendario__quando">{{ quando }}</span>
          <span>{{ cosa }}</span>
        </li>
      </ol>
    </section>
  </div>
</template>
