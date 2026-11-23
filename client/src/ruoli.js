// I pannelli della schermata iniziale. Le funzioni sono quelle chieste dalla community (lista di Null, 23/11/2026);
// "pronto" vale solo per la consultazione. Il login con Discord arriva nella tappa successiva.

export const consultazione = {
  chiave: 'consulta',
  nome: 'Consulta',
  cosa: 'Classifica, partite e civiltà dei TG Dito',
  link: '/classifica',
  accesso: 'Libero',
}

export const ruoli = [
  {
    chiave: 'giocatore',
    nome: 'Giocatore iscritto',
    cosa: 'Il tuo profilo, le disponibilità, i ticket',
    intro: 'Il posto di chi gioca il fanta: le tue statistiche e quando puoi giocare.',
    funzioni: [
      ['Il tuo profilo', 'ELO interno e ranked, storico dei Diti, civiltà giocate e prezzo pagato all\'asta.'],
      ['Calendario delle disponibilità', 'Segni le sere in cui puoi giocare; il capitano le vede subito.'],
      ['Ticket', 'Segnalazioni, richieste di restart e infrazioni, con lo stato della risposta.'],
      ['Link al tuo Discord', 'Il profilo collegato al tuo nome sul server AoE Italia.'],
    ],
  },
  {
    chiave: 'capitano',
    nome: 'Capitano',
    cosa: 'Asta, rosa e squadra',
    intro: 'Tutto quello che serve a costruire e guidare la squadra.',
    funzioni: [
      ['Asta dal vivo', 'Il giocatore all\'asta, il turno, i token spesi e quelli rimasti, in tempo reale.'],
      ['Seeding e prezzi', 'Il seeding di ogni giocatore e quanto è stato pagato.'],
      ['La tua rosa', 'I giocatori presi, i loro ELO e le disponibilità della settimana.'],
      ['Canale della squadra', 'Il collegamento alla chat del team su Discord.'],
    ],
  },
  {
    chiave: 'admin',
    nome: 'Admin',
    cosa: 'Conduzione dell\'asta, giocatori, ticket',
    intro: 'La regia del fanta: asta, iscritti e segnalazioni.',
    funzioni: [
      ['Regia dell\'asta', 'Avvio, pausa e salvataggio; correzione di un\'offerta sbagliata.'],
      ['Iscritti e seeding', 'Chi partecipa, in che fascia, con quale prezzo di partenza.'],
      ['Ticket', 'Segnalazioni, restart e infrazioni da gestire, con lo storico delle decisioni.'],
      ['Capitani e streamer', 'Chi ha accesso a quale pannello.'],
    ],
  },
  {
    chiave: 'streamer',
    nome: 'Streamer',
    cosa: 'Overlay dell\'asta per la diretta',
    intro: 'L\'asta pronta da mettere in diretta, senza aggiornare niente a mano.',
    funzioni: [
      ['Overlay per OBS', 'Una pagina da aggiungere come sorgente browser: giocatore all\'asta, offerte, token.'],
      ['Sempre aggiornato', 'L\'overlay segue l\'asta da solo mentre l\'admin la conduce.'],
      ['Pausa visibile', 'Quando l\'asta è in pausa, l\'overlay lo mostra.'],
    ],
  },
]

export const trovaRuolo = (chiave) => ruoli.find((r) => r.chiave === chiave)
