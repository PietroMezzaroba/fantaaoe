<script setup>
defineProps({ controllo: Boolean })
defineEmits(['riprova'])
</script>

<template>
  <main class="server-giu" role="alert">
    <h1>Server non raggiungibile</h1>
    <p>
      Il server locale non risponde, quindi in questo momento non si può fare nulla.
      Avvia il backend (<code>FantaAoe.Api</code>) e questa pagina si aggiorna da sola.
    </p>
    <button type="button" class="btn btn--linea" :disabled="controllo" @click="$emit('riprova')">
      {{ controllo ? 'Controllo in corso…' : 'Riprova ora' }}
    </button>
  </main>
</template>

<style scoped>
.server-giu {
  min-height: 100vh;
  display: grid;
  align-content: center;
  justify-items: center;
  gap: var(--space-md);
  padding: var(--space-lg);
  text-align: center;
  background: var(--color-paper);
  color: var(--color-ink);
}
h1 { margin: 0; font-size: var(--text-3xl); font-weight: var(--display-weight); letter-spacing: var(--tracking-display); }
p { margin: 0; max-width: 46ch; color: var(--color-ink-2); }
code { color: var(--color-accent); }
</style>
