-- Nome della moneta dell'asta (configurazioni.valuta): sostituisce la parola "crediti" nelle pagine dell'asta.
-- Idempotente: si può rieseguire senza cambiare un valore già modificato.
BEGIN;

INSERT INTO fanta_torneo.configurazioni (chiave, valore, descrizione)
VALUES ('valuta', 'Berlu', 'Nome della moneta dell''asta: compare al posto della parola "crediti" in tutte le pagine dell''asta')
ON CONFLICT (chiave) DO NOTHING;

COMMIT;
