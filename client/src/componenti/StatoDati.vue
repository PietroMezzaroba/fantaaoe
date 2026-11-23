<script setup>
// Le tre facce di un'area che carica dati: caricamento (scheletro), errore (con riprova), vuoto.
// Quando i dati ci sono, mostra lo slot.
defineProps({
  caricamento: Boolean,
  errore: String,
  vuoto: Boolean,
  righe: { type: Number, default: 8 },
  testoVuoto: { type: String, default: 'Qui non c\'è ancora niente.' },
})
defineEmits(['riprova'])
</script>

<template>
  <div v-if="caricamento" class="scheletro" aria-busy="true" aria-label="Caricamento">
    <span v-for="i in righe" :key="i" :style="{ '--w': `${40 + ((i * 37) % 45)}%` }" />
  </div>
  <div v-else-if="errore" class="avviso" role="alert">
    <strong>Non riesco a leggere i dati.</strong>
    <span>Il server ha risposto: {{ errore }}. Di solito basta riprovare tra qualche secondo.</span>
    <button type="button" class="btn btn--linea" @click="$emit('riprova')">Riprova</button>
  </div>
  <div v-else-if="vuoto" class="avviso">
    <span>{{ testoVuoto }}</span>
    <slot name="vuoto" />
  </div>
  <slot v-else />
</template>
