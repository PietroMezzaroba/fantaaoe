<script setup>
import { computed } from 'vue'
import { dataConOra } from '../api.js'

const props = defineProps({ partita: Object, giocatori: Array })

const vincitori = computed(() => props.giocatori.filter((g) => g.vinto))
const perdenti = computed(() => props.giocatori.filter((g) => !g.vinto))
</script>

<template>
  <RouterLink :to="`/partita/${partita.idGame}`" class="pannello scheda-partita">
    <div class="riga-alta">
      <span>
        <span v-if="partita.dito" class="etichetta-dito">Dito #{{ partita.dito }} · </span>
        {{ dataConOra(partita.startedAt) }}
      </span>
      <span class="tenue piccolo">
        {{ partita.durataInGameMinuti }} min
        <template v-if="!partita.valida"> · non valida</template>
      </span>
    </div>
    <div class="squadre">
      <div class="squadra">
        <span v-for="g in vincitori" :key="g.aoe2Id" class="chip vinto">{{ g.nick || g.aoe2Id }}</span>
      </div>
      <div class="contro">vs</div>
      <div class="squadra destra">
        <span v-for="g in perdenti" :key="g.aoe2Id" class="chip perso">{{ g.nick || g.aoe2Id }}</span>
      </div>
    </div>
  </RouterLink>
</template>
