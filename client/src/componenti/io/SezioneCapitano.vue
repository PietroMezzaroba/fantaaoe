<script setup>
// «La tua pagina», parte del capitano: la squadra, i Berlu, il turno di chiamata, la rosa e quando possono giocare.
// Dati: /api/capitano/squadra (squadra, rosa, disponibilità 14 giorni), /api/asta/stato e /api/asta/turno.
// Dal vivo con SignalR (astaLive.js): a ogni chiamata, offerta o acquisto si rileggono turno e squadra.
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api } from '../../api.js'
import { ascoltaAsta } from '../../astaLive.js'
import { moneta, caricaValuta } from '../../valuta.js'
import NumeroVivo from '../NumeroVivo.vue'
import StatoDati from '../StatoDati.vue'

const RIGHE_ROSA = 10 // tre fasce: A 3, B 3, C 4

const dati = ref(null)
const turno = ref(null)
const astaAttiva = ref(false)
const caricamento = ref(true)
const errore = ref('')

async function carica(silenzioso = false) {
  if (!silenzioso) caricamento.value = true
  errore.value = ''
  try {
    const [squadra, stato, t] = await Promise.all([api('capitano/squadra'), api('asta/stato'), api('asta/turno')])
    dati.value = squadra
    astaAttiva.value = !!stato.attiva
    turno.value = t
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

let smetti = () => {}
onMounted(() => {
  caricaValuta()
  carica()
  smetti = ascoltaAsta({ suEvento: () => carica(true), suRiconnesso: () => carica(true) })
})
onBeforeUnmount(() => smetti())

const squadra = computed(() => dati.value?.squadra ?? null)
const rosa = computed(() => dati.value?.rosa ?? [])
const tocca = computed(() => astaAttiva.value && turno.value?.tuoTurno)
const eloMedio = computed(() => {
  const conElo = rosa.value.filter((g) => g.elo)
  return conElo.length ? Math.round(conElo.reduce((t, g) => t + g.elo, 0) / conElo.length) : null
})

// ---------- disponibilità della squadra: righe = giocatori, colonne = i prossimi 14 giorni ----------
const giorni = computed(() => {
  const fmt = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric' })
  const fmtLungo = new Intl.DateTimeFormat('it-IT', { weekday: 'long', day: 'numeric', month: 'long' })
  const oggi = new Date()
  oggi.setHours(12, 0, 0, 0)
  return Array.from({ length: 14 }, (_, i) => {
    const d = new Date(oggi)
    d.setDate(oggi.getDate() + i)
    const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
    return { iso, breve: fmt.format(d), lungo: fmtLungo.format(d), weekend: d.getDay() === 0 || d.getDay() === 6 }
  })
})
const statoDi = computed(() => {
  const m = new Map()
  for (const r of dati.value?.disponibilita ?? []) m.set(`${r.discordId}|${String(r.data).slice(0, 10)}`, r.stato)
  return m
})
const stato = (g, d) => statoDi.value.get(`${g.idDiscord}|${d.iso}`) || null
const quanti = (iso) => rosa.value.filter((g) => statoDi.value.get(`${g.idDiscord}|${iso}`) === 'disponibile').length
// la sera con più giocatori disponibili (a parità, la prima)
const seraMigliore = computed(() => {
  let migliore = null
  for (const g of giorni.value) {
    const n = quanti(g.iso)
    if (n && (!migliore || n > migliore.n)) migliore = { ...g, n }
  }
  return migliore
})
const etichettaStato = { disponibile: 'disponibile', forse: 'forse', indisponibile: 'non può' }
</script>

<template>
  <section class="io-sezione" aria-labelledby="io-capitano">
    <div class="io-sezione__testa">
      <h2 id="io-capitano">La tua squadra</h2>
      <RouterLink v-if="astaAttiva" to="/asta/inizia" class="btn">Entra nell'asta</RouterLink>
    </div>

    <StatoDati
      :caricamento="caricamento"
      :errore="errore"
      :vuoto="!squadra"
      :righe="4"
      testo-vuoto="Non hai ancora una squadra: la assegnano gli admin prima dell'asta."
      @riprova="carica"
    >
      <!-- il turno: quando tocca a te si accende -->
      <Transition name="turno" mode="out-in">
        <p v-if="tocca" key="tocca" class="io-turno io-turno--tuo" role="status">
          <strong>Tocca a te.</strong> Chiama un giocatore: gli altri capitani aspettano.
          <RouterLink to="/asta/inizia" class="btn">Chiama</RouterLink>
        </p>
        <p v-else-if="astaAttiva && turno?.capitano" key="altro" class="io-turno" role="status">
          L'asta è aperta: ora chiama <strong>{{ turno.capitano }}</strong>.
        </p>
        <p v-else key="chiusa" class="io-turno muto">L'asta non è ancora aperta. Quando parte, qui vedi il tuo turno.</p>
      </Transition>

      <dl class="misure io-misure">
        <div class="misura">
          <dt>{{ moneta.nome }} rimasti</dt>
          <dd class="segnale"><NumeroVivo :valore="squadra.creditoResiduo" /></dd>
          <small v-if="squadra.creditoIniziale != null">su {{ squadra.creditoIniziale }}</small>
        </div>
        <div class="misura">
          <dt>Posto nell'ordine di chiamata</dt>
          <dd>{{ squadra.ordine || '—' }}</dd>
          <small>{{ squadra.ordine ? 'deciso dagli admin' : 'non ancora deciso' }}</small>
        </div>
        <div class="misura">
          <dt>Giocatori in rosa</dt>
          <dd><NumeroVivo :valore="rosa.length" /></dd>
          <small>su {{ RIGHE_ROSA }}, capitano compreso</small>
        </div>
        <div class="misura">
          <dt>ELO medio</dt>
          <dd><NumeroVivo :valore="eloMedio" /></dd>
          <small>della rosa di oggi</small>
        </div>
      </dl>

      <div class="io-due">
        <div>
          <h3 class="io-titoletto">{{ squadra.nome }}</h3>
          <TransitionGroup tag="ol" name="io-rosa" class="io-rosa">
            <li v-for="g in rosa" :key="g.idDiscord" :class="{ 'io-rosa--capitano': g.capitano }">
              <span class="io-rosa__nome">{{ g.nome }}<span v-if="g.capitano" class="muto piccolo"> capitano</span></span>
              <span class="io-rosa__fascia muto piccolo">{{ g.fascia && g.fascia !== 'da_calcolare' ? `fascia ${g.fascia}` : '' }}</span>
              <span class="num">{{ g.elo ?? '—' }}</span>
              <span class="num muto">{{ g.capitano ? '' : `${g.costo ?? 0} ${moneta.nome}` }}</span>
            </li>
          </TransitionGroup>
          <p v-if="rosa.length < RIGHE_ROSA" class="muto piccolo">Ti mancano {{ RIGHE_ROSA - rosa.length }} giocatori: li compri all'asta.</p>
        </div>

        <div>
          <h3 class="io-titoletto">Quando possono giocare</h3>
          <p v-if="seraMigliore" class="piccolo">
            Sera migliore: <strong>{{ seraMigliore.lungo }}</strong>, {{ seraMigliore.n }} su {{ rosa.length }} disponibili.
          </p>
          <p v-else class="muto piccolo">Nessuno ha ancora segnato le sere libere. Ognuno le segna dalla sua pagina.</p>
          <div class="io-disp-scorri">
            <table class="io-disp">
              <caption class="visually-hidden">Disponibilità della squadra nei prossimi 14 giorni</caption>
              <thead>
                <tr>
                  <th scope="col"><span class="visually-hidden">Giocatore</span></th>
                  <th v-for="d in giorni" :key="d.iso" scope="col" :class="{ 'io-disp--we': d.weekend }" :title="d.lungo">{{ d.breve }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="g in rosa" :key="g.idDiscord">
                  <th scope="row">{{ g.nome }}</th>
                  <td v-for="d in giorni" :key="d.iso" :class="{ 'io-disp--we': d.weekend }">
                    <span
                      class="io-disp__cella"
                      :class="'io-disp__cella--' + (stato(g, d) || 'vuoto')"
                      role="img"
                      :aria-label="g.nome + ', ' + d.lungo + ': ' + (etichettaStato[stato(g, d)] || 'non segnato')"
                      :title="d.lungo + ': ' + (etichettaStato[stato(g, d)] || 'non segnato')"
                    ></span>
                  </td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <th scope="row" class="muto piccolo">Disponibili</th>
                  <td v-for="d in giorni" :key="d.iso" class="num piccolo" :class="{ 'io-disp--we': d.weekend, 'io-disp--migliore': seraMigliore?.iso === d.iso }">{{ quanti(d.iso) || '' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
          <p class="io-legenda muto piccolo">
            <span><span class="io-disp__cella io-disp__cella--disponibile"></span> disponibile</span>
            <span><span class="io-disp__cella io-disp__cella--forse"></span> forse</span>
            <span><span class="io-disp__cella io-disp__cella--indisponibile"></span> non può</span>
            <span><span class="io-disp__cella io-disp__cella--vuoto"></span> non segnato</span>
          </p>
        </div>
      </div>
    </StatoDati>
  </section>
</template>
