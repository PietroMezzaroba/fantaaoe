// I pannelli della schermata iniziale. Le funzioni sono quelle chieste dalla community (lista di Null, 23/11/2026);
// "pronto" vale solo per la consultazione. Il login con Discord arriva nella tappa successiva.

export const consultazione = {
  chiave: 'consulta',
  nome: 'Consulta',
  cosa: 'ELO, storico e civiltà dei giocatori',
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
    cosa: 'Rosa e squadra',
    intro: 'Tutto quello che serve a costruire e guidare la squadra.',
    funzioni: [
      ['Seeding e prezzi', 'Il seeding di ogni giocatore e quanto è stato pagato.'],
      ['La tua rosa', 'I giocatori presi, i loro ELO e le disponibilità della settimana.'],
      ['Canale della squadra', 'Il collegamento alla chat del team su Discord.'],
    ],
  },
  {
    chiave: 'admin',
    nome: 'Admin',
    cosa: 'Iscritti, giocatori, ticket',
    intro: 'La regia del fanta: iscritti e segnalazioni.',
    funzioni: [
      ['Iscritti e seeding', 'Chi partecipa, in che fascia, con quale prezzo di partenza.'],
      ['Ticket', 'Segnalazioni, restart e infrazioni da gestire, con lo storico delle decisioni.'],
      ['Capitani e streamer', 'Chi ha accesso a quale pannello.'],
    ],
  },
]

export const trovaRuolo = (chiave) => ruoli.find((r) => r.chiave === chiave)
