<script setup>
import { ref, onUnmounted } from 'vue'
import { suonoRilancio } from '../suoni.js'

const props = defineProps({
  tipo: { type: String, required: true }, // 'duello' | 'paladino'
  didascalia: { type: String, default: '' },
})

const attacco = ref(false)
const scintilla = ref(false)
let t1 = null
let t2 = null

function suClick() {
  suonoRilancio()
  scintilla.value = true
  clearTimeout(t1)
  t1 = setTimeout(() => { scintilla.value = false }, 420)

  if (props.tipo === 'paladino') {
    attacco.value = true
    clearTimeout(t2)
    t2 = setTimeout(() => { attacco.value = false }, 1500)
  }
}

onUnmounted(() => {
  clearTimeout(t1)
  clearTimeout(t2)
})
</script>

<template>
  <figure class="unita-box angoli" @click="suClick" title="Clicca per l'azione">
    <div v-if="tipo === 'duello'" class="unita-arena" :class="{ 'unita-arena--colpo': scintilla }">
      <img class="unita-sprite unita-sprite--sx" src="/sprite/milizia-rossa.webp" alt="Milizia Rossa AoE2 DE" width="125" height="124" />
      <span class="unita-scintilla" aria-hidden="true"></span>
      <img class="unita-sprite unita-sprite--dx" src="/sprite/milizia-verde.webp" alt="Milizia Verde AoE2 DE" width="125" height="124" />
    </div>

    <div v-else class="unita-arena" :class="{ 'unita-arena--colpo': scintilla }">
      <img
        class="unita-sprite unita-sprite--paladino"
        :src="attacco ? '/sprite/paladino-attacco.webp' : '/sprite/paladino-galoppo.webp'"
        alt="Paladino AoE2 DE"
        width="116"
        height="140"
      />
    </div>

    <figcaption v-if="didascalia" class="piccolo muto unita-cap">{{ didascalia }}</figcaption>
  </figure>
</template>
