<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, data } from '../api.js'

const caricamento = ref(true)
const errore = ref(null)
const tabellone = ref({ classifica: [], incontri: [], squadre: [] })
const vista = ref('classifica')

const gironi = computed(() => tabellone.value.incontri.filter((i) => i.fase === 'girone'))
const playoff = computed(() => tabellone.value.incontri.filter((i) => i.fase !== 'girone'))

const giornate = computed(() => {
  const m = new Map()
  for (const inc of gironi.value) {
    if (!m.has(inc.giornata)) m.set(inc.giornata, [])
    m.get(inc.giornata).push(inc)
  }
  return Array.from(m.entries()).sort((a, b) => a[0] - b[0])
})

const fasiPlayoff = computed(() => {
  const ordine = [
    ['quarti', 'Quarti di finale'],
    ['semifinale', 'Semifinali'],
    ['finale', 'Finale (al meglio delle 9)'],
  ]
  return ordine
    .map(([chiave, titolo]) => ({ chiave, titolo, incontri: playoff.value.filter((i) => i.fase === chiave) }))
    .filter((f) => f.incontri.length > 0)
})

async function carica() {
  caricamento.value = true
  errore.value = null
  try {
    tabellone.value = await api('torneo/tabellone')
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

onMounted(carica)
</script>

<template>
  <div class="pannello-app">
    <div class="pannello-testa">
      <div class="testa-pagina" style="margin-bottom: 0">
        <h1>Torneo FantaAge 2026</h1>
        <p>Classifica gironi (Play All 3: 4/3/2/1 punti), calendario giornate e tabellone playoff.</p>
      </div>
      <div class="azioni-inline">
        <div class="scelta" role="tablist">
          <button type="button" :aria-pressed="vista === 'classifica'" @click="vista = 'classifica'">Classifica</button>
          <button type="button" :aria-pressed="vista === 'calendario'" @click="vista = 'calendario'">Gironi ({{ gironi.length }})</button>
          <button type="button" :aria-pressed="vista === 'playoff'" @click="vista = 'playoff'">Playoff ({{ playoff.length }})</button>
        </div>
        <a class="btn btn--linea" href="/api/torneo/export/rose.csv" download>Scarica rose CSV</a>
      </div>
    </div>

    <p v-if="errore" class="avviso-riga avviso-riga--err" role="alert">{{ errore }}</p>

    <div v-if="caricamento" class="scheletro" aria-label="Caricamento">
      <span style="--w: 50%"></span>
      <span style="--w: 75%"></span>
      <span style="--w: 60%"></span>
    </div>

    <!-- ==================== CLASSIFICA GIRONI ==================== -->
    <section v-else-if="vista === 'classifica'" class="riquadro angoli">
      <h2>Classifica gironi</h2>
      <p v-if="!tabellone.classifica.length" class="muto piccolo">
        Le squadre della 6ª edizione non sono ancora state pubblicate. Appena gli admin creano le squadre, la classifica apparirà qui.
      </p>
      <div v-else class="tabella">
        <table>
          <thead>
            <tr>
              <th class="num">#</th>
              <th>Squadra</th>
              <th>Capitano</th>
              <th class="num">G</th>
              <th class="num">V</th>
              <th class="num">P</th>
              <th class="num">Mappe</th>
              <th class="num">Diff</th>
              <th class="num">Punti</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(s, idx) in tabellone.classifica" :key="s.squadraId" :class="{ vivo: idx < 4 && s.giocate > 0 }">
              <td class="num pos">{{ idx + 1 }}</td>
              <td class="forte">{{ s.nome }}</td>
              <td class="tenue">{{ s.capitanoNome }}</td>
              <td class="num">{{ s.giocate }}</td>
              <td class="num vinto">{{ s.vinte }}</td>
              <td class="num perso">{{ s.perse }}</td>
              <td class="num">{{ s.mappeVinte }}–{{ s.mappePerse }}</td>
              <td class="num">{{ s.mappeVinte - s.mappePerse >= 0 ? `+${s.mappeVinte - s.mappePerse}` : s.mappeVinte - s.mappePerse }}</td>
              <td class="num forte">{{ s.punti }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ==================== CALENDARIO GIRONI ==================== -->
    <section v-else-if="vista === 'calendario'" class="blocco-pannello">
      <div v-if="!giornate.length" class="riquadro angoli">
        <p class="muto piccolo">Il calendario dei gironi non è ancora stato generato.</p>
      </div>
      <div v-for="[num, lista] in giornate" :key="num" class="riquadro angoli">
        <h2>Giornata {{ num }}</h2>
        <ul class="incontri-lista">
          <li v-for="i in lista" :key="i.id" class="incontro-riga">
            <span class="incontro-sq" :class="{ vinto: i.stato === 'giocato' && i.vinteA > i.vinteB }">{{ i.squadraANome }}</span>
            <span class="incontro-score num">
              <template v-if="i.stato === 'giocato' && i.vinteA != null && i.vinteB != null">
                {{ i.vinteA }} – {{ i.vinteB }}
              </template>
              <template v-else-if="i.stato === 'rinviato'">Rinviata</template>
              <template v-else>{{ i.dataProgrammata ? data(i.dataProgrammata) : 'vs' }}</template>
            </span>
            <span class="incontro-sq incontro-sq--dx" :class="{ vinto: i.stato === 'giocato' && i.vinteB > i.vinteA }">{{ i.squadraBNome }}</span>
          </li>
        </ul>
      </div>
    </section>

    <!-- ==================== PLAYOFF ==================== -->
    <section v-else class="blocco-pannello">
      <div v-if="!fasiPlayoff.length" class="riquadro angoli">
        <p class="muto piccolo">Il tabellone playoff sarà pubblicato al termine della fase a gironi.</p>
      </div>
      <div v-for="f in fasiPlayoff" :key="f.chiave" class="riquadro angoli">
        <h2>{{ f.titolo }}</h2>
        <ul class="incontri-lista">
          <li v-for="i in f.incontri" :key="i.id" class="incontro-riga">
            <span class="incontro-sq" :class="{ vinto: i.stato === 'giocato' && i.vinteA > i.vinteB }">{{ i.squadraANome }}</span>
            <span class="incontro-score num">
              <template v-if="i.stato === 'giocato' && i.vinteA != null && i.vinteB != null">
                {{ i.vinteA }} – {{ i.vinteB }}
              </template>
              <template v-else>{{ i.dataProgrammata ? data(i.dataProgrammata) : 'vs' }}</template>
            </span>
            <span class="incontro-sq incontro-sq--dx" :class="{ vinto: i.stato === 'giocato' && i.vinteB > i.vinteA }">{{ i.squadraBNome }}</span>
          </li>
        </ul>
      </div>
    </section>
  </div>
</template>
