<script setup>
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, percento } from '../api.js'
import StatoDati from '../componenti/StatoDati.vue'

const route = useRoute()
const router = useRouter()

const periodi = [
  { valore: null, titolo: 'Sempre' },
  { valore: 365, titolo: '1 anno' },
  { valore: 90, titolo: '90 giorni' },
]
const periodo = computed(() => (['365', '90'].includes(route.query.giorni) ? Number(route.query.giorni) : null))
const ordine = computed(() => (route.query.ordine === 'winrate' ? 'winrate' : 'partite'))

function imposta(cambi) {
  const q = { ...route.query, ...cambi }
  for (const k of Object.keys(q)) if (q[k] == null) delete q[k]
  router.replace({ query: q })
}

const civ = ref([])
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    civ.value = await api(periodo.value ? `civ?giorni=${periodo.value}` : 'civ')
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
watch(periodo, carica, { immediate: true })

const totale = computed(() => civ.value.reduce((a, c) => a + c.partite, 0))
const righe = computed(() =>
  civ.value
    .map((c) => ({
      ...c,
      winrate: c.partite ? (c.vittorie / c.partite) * 100 : 0,
      pick: totale.value ? (c.partite / totale.value) * 100 : 0,
    }))
    .sort((a, b) => b[ordine.value] - a[ordine.value] || b.partite - a.partite),
)
const pick = (n) => `${n.toLocaleString('it-IT', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} %`
</script>

<template>
  <div class="testa-pagina">
    <h1>Civiltà</h1>
    <p>Quante volte esce ogni civiltà nei TG Dito validi e quanto vince. Con poche partite il winrate dice poco.</p>
  </div>

  <div class="strumenti">
    <div class="scelta" role="group" aria-label="Periodo">
      <button
        v-for="p in periodi"
        :key="p.titolo"
        type="button"
        :aria-pressed="periodo === p.valore"
        @click="imposta({ giorni: p.valore ? String(p.valore) : null })"
      >
        {{ p.titolo }}
      </button>
    </div>
  </div>

  <StatoDati
    :caricamento="caricamento"
    :errore="errore"
    :vuoto="!righe.length"
    :righe="12"
    testo-vuoto="In questo periodo non ci sono Diti validi."
    @riprova="carica"
  >
    <div class="tabella">
      <table>
        <caption class="visually-hidden">Civiltà per numero di partite e vittorie</caption>
        <thead>
          <tr>
            <th scope="col">Civiltà</th>
            <th scope="col" class="num" :aria-sort="ordine === 'partite' ? 'descending' : 'none'">
              <button type="button" :aria-pressed="ordine === 'partite'" @click="imposta({ ordine: null })">
                Partite<span v-if="ordine === 'partite'" aria-hidden="true">↓</span>
              </button>
            </th>
            <th scope="col" class="num nascondi-stretto">Uscita</th>
            <th scope="col" :aria-sort="ordine === 'winrate' ? 'descending' : 'none'">
              <button type="button" :aria-pressed="ordine === 'winrate'" @click="imposta({ ordine: 'winrate' })">
                Vittorie<span v-if="ordine === 'winrate'" aria-hidden="true">↓</span>
              </button>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="c in righe" :key="c.civ">
            <td class="forte">{{ c.civ }}</td>
            <td class="num">{{ c.partite }}</td>
            <td class="num muto nascondi-stretto">{{ pick(c.pick) }}</td>
            <td>
              <div class="barretta">
                <div class="barretta__traccia"><span :style="{ transform: `scaleX(${c.winrate / 100})` }" /></div>
                <span class="num">{{ percento(c.winrate) }}</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </StatoDati>
</template>
