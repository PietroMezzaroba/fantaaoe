<script setup>
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, elo, percento, fa } from '../api.js'
import StatoDati from '../componenti/StatoDati.vue'

// Filtri e ordine stanno nell'indirizzo: un link condiviso apre la stessa vista.
const route = useRoute()
const router = useRouter()

const colonne = [
  { chiave: 'eloInterno', titolo: 'Interno' },
  { chiave: 'elo1v1', titolo: '1v1' },
  { chiave: 'eloTg', titolo: 'TG' },
]

const tutti = computed(() => route.query.tutti === '1')
const ordine = computed(() => (colonne.some((c) => c.chiave === route.query.ordine) ? route.query.ordine : 'eloInterno'))
const cerca = ref(route.query.q ?? '')

function imposta(cambi) {
  const q = { ...route.query, ...cambi }
  for (const k of Object.keys(q)) if (q[k] === undefined || q[k] === '') delete q[k]
  router.replace({ query: q })
}
watch(cerca, (v) => imposta({ q: v.trim() || undefined }))

const giocatori = ref([])
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    giocatori.value = await api(`giocatori?tutti=${tutti.value}`)
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
watch(tutti, carica, { immediate: true })

const righe = computed(() => {
  const q = cerca.value.trim().toLowerCase()
  const k = ordine.value
  return giocatori.value
    .filter((g) => !q || g.nick.toLowerCase().includes(q) || (g.clan || '').toLowerCase().includes(q))
    .slice()
    .sort((a, b) => (b[k] ?? -1) - (a[k] ?? -1) || a.nick.localeCompare(b.nick))
})
</script>

<template>
  <div class="testa-pagina">
    <h1>Classifica</h1>
    <p>ELO interno dei TG Dito e ELO ranked di oggi. Per chi ha più account vale il più alto.</p>
  </div>

  <div class="strumenti">
    <label class="visually-hidden" for="cerca-giocatore">Cerca giocatore o clan</label>
    <input id="cerca-giocatore" v-model="cerca" class="cerca" type="search" name="q" autocomplete="off" placeholder="Nome o clan" />
    <div class="scelta" role="group" aria-label="Quali giocatori">
      <button type="button" :aria-pressed="!tutti" @click="imposta({ tutti: undefined })">Attivi in 90 giorni</button>
      <button type="button" :aria-pressed="tutti" @click="imposta({ tutti: '1' })">Tutti</button>
    </div>
  </div>

  <StatoDati
    :caricamento="caricamento"
    :errore="errore"
    :vuoto="!righe.length"
    :righe="12"
    testo-vuoto="Nessun giocatore con questo nome o clan."
    @riprova="carica"
  >
    <template #vuoto>
      <button type="button" class="btn btn--linea" @click="cerca = ''">Togli la ricerca</button>
    </template>
    <div class="tabella">
      <table>
        <caption class="visually-hidden">Classifica dei giocatori</caption>
        <thead>
          <tr>
            <th scope="col"><span class="visually-hidden">Posizione</span></th>
            <th scope="col">Giocatore</th>
            <th v-for="c in colonne" :key="c.chiave" scope="col" class="num" :aria-sort="ordine === c.chiave ? 'descending' : 'none'">
              <button type="button" :aria-pressed="ordine === c.chiave" @click="imposta({ ordine: c.chiave === 'eloInterno' ? undefined : c.chiave })">
                {{ c.titolo }}<span v-if="ordine === c.chiave" aria-hidden="true">↓</span>
              </button>
            </th>
            <th scope="col" class="num nascondi-stretto">Diti</th>
            <th scope="col" class="num nascondi-stretto">Vinti</th>
            <th scope="col" class="nascondi-stretto">Ultimo Dito</th>
          </tr>
        </thead>
        <!-- cambiando colonna le righe scivolano al posto nuovo; mentre si cerca la tabella cambia di colpo -->
        <TransitionGroup tag="tbody" :name="cerca.trim() ? 'classifica-ferma' : 'classifica'">
          <tr v-for="(g, i) in righe" :key="g.playerId">
            <td class="pos num">{{ i + 1 }}</td>
            <td class="nome">
              <RouterLink :to="`/giocatore/${g.playerId}`">{{ g.nick }}</RouterLink>
              <span v-if="g.clan" class="clan">{{ g.clan }}</span>
            </td>
            <td v-for="c in colonne" :key="c.chiave" class="num" :class="ordine === c.chiave ? 'forte' : 'tenue'">
              {{ elo(g[c.chiave]) }}
            </td>
            <td class="num tenue nascondi-stretto">{{ g.nDiti }}</td>
            <td class="num tenue nascondi-stretto">{{ g.nDiti ? percento(g.winrate) : '—' }}</td>
            <td class="muto nascondi-stretto">{{ fa(g.ultimoDito) }}</td>
          </tr>
        </TransitionGroup>
      </table>
    </div>
    <p class="muto piccolo" style="margin-top: var(--space-md)">{{ righe.length }} giocatori</p>
  </StatoDati>
</template>

<style scoped>
/* ---------- movimento ---------- */
.classifica-move { transition: transform var(--dur-long) var(--ease-out); }
</style>
