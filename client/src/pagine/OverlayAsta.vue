<script setup>
import { computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { elo } from '../api.js'
import { usaAstaLive } from '../astaLive.js'

const route = useRoute()
const { dati: asta } = usaAstaLive()

const sfondoScuro = computed(() => route.query.sfondo === 'scuro')
const squadraMigliore = computed(() =>
  asta.value?.squadre?.find((s) => s.id === asta.value.squadraMiglioreId) || null
)
const squadraTurno = computed(() =>
  asta.value?.squadre?.find((s) => s.id === asta.value.turnoSquadraId) || null
)

onMounted(() => {
  if (!sfondoScuro.value) {
    document.documentElement.style.background = 'transparent'
    document.body.style.background = 'transparent'
  }
})
onUnmounted(() => {
  document.documentElement.style.background = ''
  document.body.style.background = ''
})
</script>

<template>
  <div class="obs" :class="{ 'obs--scuro': sfondoScuro }">
    <!-- Banner pausa / stato -->
    <div v-if="asta?.stato === 'pausa'" class="obs-banner obs-banner--pausa">
      <strong>ASTA IN PAUSA</strong>
      <span v-if="asta.messaggio"> · {{ asta.messaggio }}</span>
    </div>
    <div v-else-if="asta?.messaggio" class="obs-banner">
      {{ asta.messaggio }}
    </div>

    <!-- Scheda principale in basso a sinistra -->
    <div class="obs-card" v-if="asta?.giocatore">
      <div class="obs-card__testa">
        <img src="/logo-elmo.png" alt="" width="36" height="36" />
        <div>
          <span class="obs-tag">FANTAAGE · FASCIA {{ asta.giocatore.fascia }}</span>
          <h1>{{ asta.giocatore.nick }}</h1>
        </div>
      </div>

      <div class="obs-stats">
        <div><span>Seed</span><strong>{{ asta.giocatore.seeding ?? '—' }}</strong></div>
        <div><span>1v1</span><strong>{{ elo(asta.giocatore.elo1v1) }}</strong></div>
        <div><span>ELO int.</span><strong>{{ elo(asta.giocatore.eloInterno) }}</strong></div>
        <div><span>TG</span><strong>{{ elo(asta.giocatore.eloTg) }}</strong></div>
      </div>

      <div class="obs-offerta">
        <div>
          <span class="obs-etichetta">OFFERTA ATTUALE</span>
          <strong class="obs-cifra">{{ asta.offertaAttuale }} <em>Berlu</em></strong>
        </div>
        <div class="obs-leader">
          <span class="obs-etichetta">IN VANTAGGIO</span>
          <strong>{{ squadraMigliore ? squadraMigliore.nome : 'Nessuna offerta' }}</strong>
        </div>
      </div>
    </div>

    <div class="obs-card obs-card--attesa" v-else>
      <div class="obs-card__testa">
        <img src="/logo-elmo.png" alt="" width="36" height="36" />
        <div>
          <span class="obs-tag">FANTAAGE · ASTA DAL VIVO</span>
          <h1>{{ asta?.stato === 'chiusa' ? 'Asta conclusa' : 'In attesa della prossima chiamata' }}</h1>
          <p v-if="squadraTurno" class="obs-sub">Round {{ asta?.roundNumero }} · Tocca a {{ squadraTurno.nome }}</p>
        </div>
      </div>
    </div>

    <!-- Barra inferiore con tutte le squadre e i Berlu residui -->
    <div v-if="asta?.squadre?.length" class="obs-barra">
      <div
        v-for="s in asta.squadre"
        :key="s.id"
        class="obs-sq"
        :class="{ 'obs-sq--leader': s.id === asta.squadraMiglioreId, 'obs-sq--turno': s.id === asta.turnoSquadraId }"
      >
        <span class="obs-sq__nome">{{ s.nome }}</span>
        <strong class="obs-sq__berlu">{{ s.berluResidui }} B</strong>
        <span class="obs-sq__rosa">{{ s.nGiocatori }}/{{ asta.maxRosa }}</span>
      </div>
    </div>
  </div>
</template>
