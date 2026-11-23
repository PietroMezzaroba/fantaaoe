<script setup>
// Asta di prova con le regole dell'handbook del Fanta Age 2025:
// 401 Berlu a capitano, rosa da 10, offerta minima 1 Berlu, chi chiama apre a 1,
// offerta massima = Berlu rimasti meno un Berlu per ogni altro posto ancora vuoto,
// chiamata a serpente (A B C, C B A, ...). Giocatori veri dal database, capitani e prezzi finti.
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { api, elo } from '../api.js'

const BERLU = 401
const ROSA = 10
const DURATA = 8000
const RILANCIO = 4000
const LOTTI = 6

const stato = ref('attesa') // attesa | chiamata | asta | aggiudicato | finita | errore
const mazzo = ref([])
const lotto = ref(null)
const numeroLotto = ref(0)
const offerta = ref(0)
const offerente = ref(null)
const rimasto = ref(DURATA)
const annuncio = ref('')
const capitani = reactive([
  { id: 'tu', nome: 'Tu', berlu: BERLU, rosa: [] },
  { id: 'verde', nome: 'Capitano verde', berlu: BERLU, rosa: [] },
  { id: 'rosso', nome: 'Capitano rosso', berlu: BERLU, rosa: [] },
])

let valutazioni = {}
let timer = 0, prossimoBot = 0, ultimo = 0, attesa = 0
let visibile = true, osservatore
const radice = ref(null)

const tu = computed(() => capitani[0])
const chiOffre = computed(() => capitani.find((c) => c.id === offerente.value))
const quota = computed(() => Math.max(0, rimasto.value / DURATA))
const secondi = computed(() => Math.ceil(rimasto.value / 1000))

// ordine di chiamata a serpente: 0 1 2 2 1 0 0 1 2 ...
const chiama = computed(() => {
  const giro = Math.floor((numeroLotto.value) / 3)
  const pos = numeroLotto.value % 3
  return capitani[giro % 2 === 0 ? pos : 2 - pos]
})
const proposte = computed(() => mazzo.value.slice(0, 3))

const massimo = (c) => c.berlu - Math.max(0, ROSA - c.rosa.length - 1)

function mescola(a) {
  const b = a.slice()
  for (let i = b.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[b[i], b[j]] = [b[j], b[i]]
  }
  return b
}

let tutti = []
async function prepara() {
  try {
    tutti = (await api('giocatori')).slice(0, 40)
    mazzo.value = mescola(tutti)
  } catch {
    stato.value = 'errore'
  }
}

function valuta(g) {
  const base = Math.min(150, Math.max(2, (g.eloInterno - 1300) / 8))
  return {
    verde: Math.round(base * (0.7 + Math.random() * 0.55)),
    rosso: Math.round(base * (0.7 + Math.random() * 0.55)),
  }
}

function prossimo() {
  clearTimeout(attesa)
  if (numeroLotto.value >= LOTTI) {
    stato.value = 'finita'
    annuncio.value = 'Asta di prova finita.'
    return
  }
  if (chiama.value.id === 'tu') {
    stato.value = 'chiamata'
    annuncio.value = 'Tocca a te chiamare un giocatore.'
  } else {
    // i capitani automatici chiamano chi vale di più tra i primi tre del mazzo
    const scelta = proposte.value.slice().sort((a, b) => b.eloInterno - a.eloInterno)[Math.floor(Math.random() * 2)]
    apri(scelta, chiama.value)
  }
}

function apri(giocatore, chi) {
  mazzo.value = mazzo.value.filter((g) => g.playerId !== giocatore.playerId)
  lotto.value = giocatore
  numeroLotto.value++
  valutazioni = valuta(giocatore)
  offerta.value = 1
  offerente.value = chi.id
  rimasto.value = DURATA
  stato.value = 'asta'
  annuncio.value = `${chi.id === 'tu' ? 'Chiami' : chi.nome + ' chiama'} ${giocatore.nick} a 1 Berlu.`
  ultimo = performance.now()
  prossimoBot = ultimo + 900 + Math.random() * 900
}

function offri(capitano, quanto) {
  if (stato.value !== 'asta') return
  const nuova = offerta.value + quanto
  if (nuova > massimo(capitano) || offerente.value === capitano.id) return
  offerta.value = nuova
  offerente.value = capitano.id
  if (rimasto.value < RILANCIO) rimasto.value = RILANCIO
  annuncio.value = `${capitano.id === 'tu' ? 'Offri' : capitano.nome + ' offre'} ${nuova} Berlu.`
}

const puoiOffrire = (quanto) =>
  stato.value === 'asta' && offerente.value !== 'tu' && offerta.value + quanto <= massimo(tu.value)

function aggiudica() {
  const c = chiOffre.value
  c.berlu -= offerta.value
  c.rosa.push({ nick: lotto.value.nick, prezzo: offerta.value })
  annuncio.value = `${lotto.value.nick} va a ${c.id === 'tu' ? 'te' : c.nome} per ${offerta.value} Berlu.`
  stato.value = 'aggiudicato'
  attesa = setTimeout(prossimo, 2600)
}

function passo() {
  const ora = performance.now()
  const dt = ora - ultimo
  ultimo = ora
  if (stato.value !== 'asta' || !visibile) return
  rimasto.value -= dt
  if (ora >= prossimoBot) {
    prossimoBot = ora + 600 + Math.random() * 1000
    const bot = mescola(capitani.slice(1)).find(
      (c) => c.id !== offerente.value && offerta.value < valutazioni[c.id] && offerta.value < massimo(c) && Math.random() < 0.6,
    )
    if (bot) {
      const salto = Math.min(massimo(bot) - offerta.value, [1, 2, 5, 10][Math.floor(Math.random() * 4)])
      if (salto > 0) offri(bot, salto)
    }
  }
  if (rimasto.value <= 0) {
    rimasto.value = 0
    aggiudica()
  }
}

function inizia() {
  for (const c of capitani) {
    c.berlu = BERLU
    c.rosa = []
  }
  numeroLotto.value = 0
  mazzo.value = mescola(tutti)
  prossimo()
}

onMounted(async () => {
  await prepara()
  timer = setInterval(passo, 100)
  osservatore = new IntersectionObserver(([v]) => {
    visibile = v.isIntersecting
    ultimo = performance.now()
  })
  osservatore.observe(radice.value)
})
onBeforeUnmount(() => {
  clearInterval(timer)
  clearTimeout(attesa)
  osservatore?.disconnect()
})
</script>

<template>
  <div ref="radice" class="asta angoli">
    <p class="visually-hidden" aria-live="polite">{{ annuncio }}</p>

    <div v-if="stato === 'errore'" class="avviso">
      <strong>L'asta di prova non riesce a leggere i giocatori.</strong>
      <span>Il server non ha risposto. Riprova tra poco.</span>
    </div>

    <template v-else>
      <div class="asta__lotto">
        <span class="asta__etichetta">
          <template v-if="stato === 'attesa'">{{ LOTTI }} chiamate, 401 Berlu a capitano</template>
          <template v-else-if="stato === 'finita'">Asta di prova finita</template>
          <template v-else-if="stato === 'chiamata'">Chiamata {{ numeroLotto + 1 }} di {{ LOTTI }}, tocca a te</template>
          <template v-else>Chiamata {{ numeroLotto }} di {{ LOTTI }}</template>
        </span>
        <strong class="asta__nome">
          <template v-if="stato === 'attesa'">Pronto?</template>
          <template v-else-if="stato === 'finita'">Le rose</template>
          <template v-else-if="stato === 'chiamata'">Chi chiami?</template>
          <template v-else>{{ lotto?.nick }}</template>
        </strong>
        <span v-if="lotto && (stato === 'asta' || stato === 'aggiudicato')" class="asta__elo num">
          interno {{ elo(lotto.eloInterno) }}, 1v1 {{ elo(lotto.elo1v1) }}, TG {{ elo(lotto.eloTg) }}
        </span>
      </div>

      <div v-if="stato === 'chiamata'" class="asta__proposte">
        <button v-for="g in proposte" :key="g.playerId" type="button" class="asta__proposta" @click="apri(g, tu)">
          <span class="asta__proposta-nome">{{ g.nick }}</span>
          <span class="num muto">interno {{ elo(g.eloInterno) }}, 1v1 {{ elo(g.elo1v1) }}</span>
        </button>
      </div>

      <div v-if="stato === 'asta' || stato === 'aggiudicato'" class="asta__offerta">
        <div class="asta__cifra">
          <span class="num">{{ offerta }}</span><span class="u">&thinsp;Berlu</span>
        </div>
        <span class="asta__chi">
          <template v-if="stato === 'aggiudicato'">aggiudicato a {{ chiOffre.nome }}</template>
          <template v-else>{{ chiOffre.nome }}</template>
        </span>
        <div class="asta__tempo">
          <span class="asta__barra" aria-hidden="true"><span :style="{ transform: `scaleX(${quota})` }" /></span>
          <span class="num muto">{{ secondi }}&thinsp;s</span>
        </div>
      </div>

      <div v-if="stato !== 'chiamata'" class="asta__comandi">
        <template v-if="stato === 'attesa' || stato === 'finita'">
          <button type="button" class="btn btn--linea btn--grande" :disabled="!mazzo.length" @click="inizia">
            {{ stato === 'finita' ? 'Rifai l\'asta' : 'Inizia l\'asta di prova' }}
          </button>
        </template>
        <template v-else>
          <button v-for="q in [1, 5, 10]" :key="q" type="button" class="btn btn--linea btn--grande" :disabled="!puoiOffrire(q)" @click="offri(tu, q)">
            +{{ q }}
          </button>
          <span class="asta__massimo muto">la tua offerta massima: <span class="num">{{ massimo(tu) }}</span></span>
        </template>
      </div>

      <table class="asta__capitani">
        <caption class="visually-hidden">Capitani, Berlu rimasti e rose</caption>
        <thead>
          <tr><th scope="col">Capitano</th><th scope="col" class="num">Berlu</th><th scope="col">Rosa</th></tr>
        </thead>
        <tbody>
          <tr v-for="c in capitani" :key="c.id" :class="{ 'asta__guida': offerente === c.id && stato === 'asta' }">
            <td :class="`capitano capitano--${c.id}`">{{ c.nome }}</td>
            <td class="num">{{ c.berlu }}</td>
            <td class="tenue">
              <template v-if="c.rosa.length">{{ c.rosa.map((r) => `${r.nick} ${r.prezzo}`).join(', ') }}</template>
              <span v-else class="muto">nessuno</span>
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </div>
</template>
