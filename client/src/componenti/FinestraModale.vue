<script setup>
// Finestra modale riutilizzabile: velo scuro, riquadro al centro, titolo e contenuto a slot.
// Si chiude con Esc, cliccando fuori dal riquadro o con l'evento "chiudi" emesso dal contenitore.
import { onMounted, onUnmounted, useId } from 'vue'

defineProps({ titolo: { type: String, required: true }, largo: Boolean })
const emit = defineEmits(['chiudi'])
const idTitolo = useId()

function suTasto(e) {
  if (e.key === 'Escape') emit('chiudi')
}
onMounted(() => window.addEventListener('keydown', suTasto))
onUnmounted(() => window.removeEventListener('keydown', suTasto))
</script>

<template>
  <div class="modale-velo" @mousedown.self="emit('chiudi')">
    <div class="modale" :class="{ 'modale--larga': largo }" role="dialog" aria-modal="true" :aria-labelledby="idTitolo">
      <h2 :id="idTitolo">{{ titolo }}</h2>
      <slot />
    </div>
  </div>
</template>

<style scoped>
.modale-velo {
  position: fixed; inset: 0; z-index: var(--z-modal);
  display: grid; place-items: center; padding: var(--space-md);
  background: oklch(10% 0.02 255 / 0.7);
}
.modale {
  width: min(100%, 26rem); max-height: 100%; overflow-y: auto; display: grid; gap: var(--space-md);
  padding: var(--space-lg); background: var(--color-paper-2);
  border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.modale--larga { width: min(100%, 36rem); }
h2 { margin: 0; font-size: var(--text-xl); font-weight: var(--display-weight); }
</style>
