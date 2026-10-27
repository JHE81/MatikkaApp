using System;
using System.Collections.Generic;
using Matikka.Core.Progress;

namespace Matikka.Core.Sync
{
    // Sovelluksen ja palvelimen yhteiset viestit. Pidetään yksinkertaisina luokkina,
    // jotta System.Text.Json (ja tarvittaessa Unityn JSON-kirjastot) sarjallistavat ne suoraan.

    /// <summary>POST /api/accounts -vastaus. Token tallennetaan laitteen suojattuun tallennustilaan.</summary>
    public sealed class AccountCreatedResponse
    {
        public Guid AccountId { get; set; }

        public string Token { get; set; } = "";
    }

    /// <summary>POST /api/sync: lähetä uudet tapahtumat, vastaanota muiden laitteiden tapahtumat.</summary>
    public sealed class SyncRequest
    {
        public const int MaxEventsPerRequest = 500;

        /// <summary>Suurin palvelimen järjestysnumero, jonka laite on jo saanut.</summary>
        public long SinceSeq { get; set; }

        public List<GameEvent> Events { get; set; } = new List<GameEvent>();
    }

    public sealed class SyncResponse
    {
        /// <summary>Tapahtumat, jotka palvelimella on nyt tallessa (myös aiemmin tallennetut).</summary>
        public List<Guid> AcceptedIds { get; set; } = new List<Guid>();

        /// <summary>Tapahtumat, joita laitteella ei vielä ole (esim. toiselta laitteelta).</summary>
        public List<GameEvent> Incoming { get; set; } = new List<GameEvent>();

        /// <summary>Tallennetaan laitteelle seuraavan synkronoinnin SinceSeq-arvoksi.</summary>
        public long LatestSeq { get; set; }

        /// <summary>Palvelimella on vielä lisää tapahtumia: laite synkronoi heti uudelleen.</summary>
        public bool HasMore { get; set; }
    }
}
