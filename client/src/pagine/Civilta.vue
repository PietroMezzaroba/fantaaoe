<script setup>
// Le civiltà con il loro stemma e i numeri veri dei TG Dito validi.
//  - stemmi e nomi italiani: anagrafica fanta_torneo.civilta (/api/civilta); lo stemma è client/public/civilta/<avatar>.png
//    e il nome inglese è già scritto nello stemma, sotto si vede la descrizione italiana;
//  - numeri: /api/civ (partite giocate e vinte con quella civiltà, per periodo), abbinati per nome inglese.
// Periodo e ordine stanno nell'indirizzo: un link condiviso apre la stessa vista. Cambiando ordine o periodo
// le schede scivolano al loro posto nuovo (movimento: transform), le barre e i numeri scorrono al valore nuovo.
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, percento } from '../api.js'
import StatoDati from '../componenti/StatoDati.vue'
import NumeroVivo from '../componenti/NumeroVivo.vue'

const route = useRoute()
const router = useRouter()

const periodi = [
  { valore: null, titolo: 'Sempre' },
  { valore: 365, titolo: '1 anno' },
  { valore: 90, titolo: '90 giorni' },
]
const ordini = [
  { valore: 'partite', titolo: 'Più giocate' },
  { valore: 'winrate', titolo: 'Più vincenti' },
  { valore: 'nome', titolo: 'Dalla A alla Z' },
]
// sotto questa soglia il winrate dice poco: si vede attenuato e, ordinando per vittorie, va dopo le altre
const POCHE = 10

const periodo = computed(() => (['365', '90'].includes(route.query.giorni) ? Number(route.query.giorni) : null))
const ordine = computed(() => (['winrate', 'nome'].includes(route.query.ordine) ? route.query.ordine : 'partite'))

function imposta(cambi) {
  const q = { ...route.query, ...cambi }
  for (const k of Object.keys(q)) if (q[k] == null) delete q[k]
  router.replace({ query: q })
}

const anagrafica = ref([])
const statistiche = ref([])
const caricamento = ref(true)
const errore = ref('')
const senzaStemma = reactive(new Set()) // avatar per cui il file non si trova

async function caricaStatistiche() {
  errore.value = ''
  try {
    statistiche.value = await api(periodo.value ? `civ?giorni=${periodo.value}` : 'civ')
  } catch (e) {
    errore.value = e.message
  }
}
async function carica() {
  caricamento.value = true
  // senza anagrafica la pagina vive lo stesso: nome e stemma si prendono dal nome inglese delle statistiche
  const [anag] = await Promise.allSettled([api('civilta'), caricaStatistiche()])
  if (anag.status === 'fulfilled') anagrafica.value = anag.value
  caricamento.value = false
}
onMounted(carica)
watch(periodo, caricaStatistiche)

const totale = computed(() => statistiche.value.reduce((t, c) => t + c.partite, 0))
const perNome = computed(() => new Map(statistiche.value.map((c) => [c.civ.toLowerCase(), c])))

const righe = computed(() => {
  const base = anagrafica.value.length
    ? anagrafica.value.map((c) => ({ id: c.id, chiave: c.nome, nome: c.descrizione || c.nome, avatar: c.avatar }))
    : statistiche.value.map((c) => ({ id: c.civ, chiave: c.civ, nome: c.civ, avatar: c.civ }))
  const lista = base.map((c) => {
    const s = perNome.value.get(c.chiave.toLowerCase())
    const partite = s?.partite ?? 0
    const vittorie = s?.vittorie ?? 0
    return {
      ...c,
      partite,
      winrate: partite ? (vittorie / partite) * 100 : null,
      pick: totale.value ? (partite / totale.value) * 100 : 0,
      poche: partite < POCHE,
    }
  })
  const perNomeIt = (a, b) => a.nome.localeCompare(b.nome, 'it', { sensitivity: 'base' })
  if (ordine.value === 'nome') return lista.sort(perNomeIt)
  if (ordine.value === 'winrate')
    return lista.sort((a, b) => a.poche - b.poche || (b.winrate ?? -1) - (a.winrate ?? -1) || b.partite - a.partite || perNomeIt(a, b))
  return lista.sort((a, b) => b.partite - a.partite || perNomeIt(a, b))
})

const quota = (n) => `${n.toLocaleString('it-IT', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} %`
const descrizionePeriodo = computed(() => (periodo.value ? `negli ultimi ${periodo.value === 365 ? '12 mesi' : '90 giorni'}` : 'da sempre'))
</script>

<template>
  <div class="testa-pagina">
    <h1>Civiltà</h1>
    <p>Quante volte esce ogni civiltà nei TG Dito validi e quanto vince. Con meno di {{ POCHE }} partite il winrate dice poco.</p>
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
    <div class="scelta" role="group" aria-label="Ordine">
      <button
        v-for="o in ordini"
        :key="o.valore"
        type="button"
        :aria-pressed="ordine === o.valore"
        @click="imposta({ ordine: o.valore === 'partite' ? null : o.valore })"
      >
        {{ o.titolo }}
      </button>
    </div>
  </div>

  <StatoDati
    :caricamento="caricamento"
    :errore="errore"
    :vuoto="!righe.length"
    :righe="8"
    testo-vuoto="Non ci sono civiltà da mostrare."
    @riprova="carica"
  >
    <p class="muto piccolo civ__riassunto">{{ totale.toLocaleString('it-IT') }} scelte di civiltà {{ descrizionePeriodo }}.</p>
    <TransitionGroup tag="ul" name="civ" class="civ">
      <li v-for="(c, i) in righe" :key="c.id" class="civ__scheda" :class="{ 'civ__scheda--ferma': !c.partite }">
        <span class="civ__pos num" aria-hidden="true">{{ ordine === 'nome' ? '' : i + 1 }}</span>
        <span class="civ__stemma">
          <span v-if="senzaStemma.has(c.avatar)" class="civ__iniziale" aria-hidden="true">{{ c.nome.charAt(0) }}</span>
          <img
            v-else
            :src="`/civilta/${encodeURIComponent(c.avatar)}.png`"
            :alt="`Stemma: ${c.chiave}`"
            width="80"
            height="80"
            :loading="i < 12 ? 'eager' : 'lazy'"
            @error="senzaStemma.add(c.avatar)"
          />
        </span>
        <h2 class="civ__nome">{{ c.nome }}</h2>
        <dl class="civ__numeri">
          <div>
            <dt>Partite</dt>
            <dd class="num"><NumeroVivo :valore="c.partite" /></dd>
          </div>
          <div>
            <dt>Uscita</dt>
            <dd class="num">{{ c.partite ? quota(c.pick) : '—' }}</dd>
          </div>
        </dl>
        <div class="barretta civ__vittorie" :class="{ 'civ__vittorie--poche': c.poche }" :title="c.poche && c.partite ? 'Poche partite: il dato dice poco' : null">
          <span class="visually-hidden">Vittorie</span>
          <div class="barretta__traccia"><span :style="{ transform: `scaleX(${(c.winrate ?? 0) / 100})` }" /></div>
          <span class="num">{{ c.winrate == null ? '—' : percento(c.winrate) }}</span>
        </div>
      </li>
    </TransitionGroup>
  </StatoDati>
</template>

<style scoped>
.civ__riassunto { margin: 0 0 var(--space-md); }
.civ {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(10rem, 1fr)); gap: var(--space-md);
  list-style: none; margin: 0; padding: 0;
}
.civ__scheda {
  position: relative; display: grid; justify-items: center; align-content: start; gap: var(--space-xs); text-align: center;
  padding: var(--space-md) var(--space-sm); background: var(--color-paper-2);
  border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card); min-width: 0;
}
/* civiltà mai giocate nel periodo: la scheda resta, più quieta */
.civ__scheda--ferma .civ__stemma, .civ__scheda--ferma .civ__nome { opacity: 0.55; }
.civ__pos { position: absolute; top: var(--space-xs); left: var(--space-sm); color: var(--color-muted); font-size: var(--text-xs); }
.civ__stemma { display: grid; place-items: center; width: 80px; height: 80px; }
.civ__stemma img { width: 80px; height: 80px; object-fit: contain; }
.civ__iniziale {
  display: grid; place-items: center; width: 80px; height: 80px; border-radius: 50%;
  background: var(--color-paper-3); color: var(--color-muted); font-size: var(--text-3xl);
}
.civ__nome { margin: 0; font-size: var(--text-md); font-weight: 600; overflow-wrap: anywhere; min-width: 0; }
.civ__numeri { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-xs); width: 100%; margin: 0; }
.civ__numeri div { display: grid; gap: 0; }
.civ__numeri dt { color: var(--color-muted); font-size: var(--text-xs); }
.civ__numeri dd { margin: 0; font-weight: 600; }
.civ__vittorie { width: 100%; }
.civ__vittorie .barretta__traccia span { background: var(--color-accent); }
.civ__vittorie--poche .barretta__traccia span { background: var(--color-ink-2); opacity: 0.5; }
.civ__vittorie--poche .num { color: var(--color-muted); }

/* ---------- movimento ---------- */
/* cambiando ordine o periodo le schede scivolano al posto nuovo, le barre si allungano o si accorciano */
.civ-move { transition: transform var(--dur-long) var(--ease-out); }
.civ__vittorie .barretta__traccia span { transition: transform var(--dur-long) var(--ease-out); }
</style>
