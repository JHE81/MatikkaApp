# Matikkapeli

Pelillinen harjoitussovellus yläkoulun (7.–9. lk) matematiikkaan. Ensimmäinen peli on
viisi peliä:

- **Yhtälövaaka**: yhtälö ratkaistaan tekemällä sama siirto vaa'an molemmille puolille (8 tasoa).
  Negatiivisia lukuja sisältävillä tasoilla vaaka vaihtuu kahdeksi lämpömittariksi: vakio on
  lämpötila (plus lämpöä, miinus pakkasta), ja sama siirto liikuttaa molempia mittareita yhtä paljon.
- **Yhtälödomino**: tummissa dominolaatoissa on vastaus ja yhtälö. Pelaaja raahaa ketjun perään
  laatan, jonka vastaus ratkaisee avoimen yhtälön; koko ketju yhtälöineen jää näkyviin. Väärä laatta
  selitetään sijoittamalla (6 tasoa, sulkeisiin asti). Raahaus: `wwwroot/drag.js`.
- **Lukusuorahyppy**: hahmo hyppii lukusuoralla laskukorteilla (+, −, ·, :, neliö, kuutio) tavoite-
  lukuun. Kortit ovat kertakäyttöisiä, ja järjestys ratkaisee. Negatiivisella kertominen peilaa nollan
  yli (6 tasoa, 7. lk negatiiviset luvut ja potenssin käsite).

- **Potenssiyhdistely**: 2048-tyylinen ruudukko, jossa laatat ovat potensseja. Samankantaiset
  laatat yhdistyvät liukuessaan vastakkain (eksponentit yhteen), jakajalaatat vähentävät ja
  korotuslaatat (  )ⁿ kertovat eksponentin. Kokoa tavoitepotenssi (7 tasoa, 8. lk laskusäännöt).

- **Koodipolku**: pelaaja kokoaa örkilleen ohjelman lohkoista (eteen, käänny, toista, aliohjelma)
  ja ajaa sen. Örkki kulkee pimeän labyrintin läpi portaaliin, kerää kristallit ja napsii pisteitä
  matkalla. Virheellinen lohko korostetaan (6 tasoa, OPS:n algoritminen ajattelu).

Pelihahmot (Napsu, Möykky, Pörri ja Bitti) ovat alkuperäisiä SVG-piirroksia, ja sama valittu
hahmo esiintyy Koodipolussa ja Lukusuorahypyssä.

Vaaka ja domino kasvattavat samaa yhtälötaitoa, muut pelit omaa taitoaan (Elo).

- .NET MAUI + Blazor Hybrid (Android, iOS), raikas moderni ilme, neljä valittavaa värimaailmaa
- Offline-first: tulokset tallentuvat ensin laitteen SQLite-tietokantaan, palvelin on vain varmuuskopio ja laitteiden välinen synkronointi
- Pelilogiikka erillisessä `netstandard2.1`-kirjastossa, joka siirtyy sellaisenaan Unityyn

## Rakenne

```
src/
  Matikka.Core      Pelilogiikka: säännöt, tehtävägeneraattori, ratkaisija, Elo, edistyminen.
                    netstandard2.1 + C# 9, ei riippuvuuksia → käy myös Unityyn.
  Matikka.Client    Laitteen palvelut: SQLite-tallennus, edistyminen, synkronointi, profiili.
  Matikka.Ui        Kaikki näkymät (Razor), tyylit ja värimaailmat (wwwroot/app.css).
  Matikka.App       MAUI-isäntä: palveluiden kytkentä, SecureStorage, ikonit, alustatiedostot.
  Matikka.Api       Synkronointi-API (ASP.NET Core minimal API, PostgreSQL / muistivarasto).
dev/
  Matikka.DevHost   Ajaa saman käyttöliittymän selaimessa. Nopein kehityssilmukka.
tests/              xUnit-testit Corelle, Clientille ja API:lle.
```

Riippuvuudet kulkevat yhteen suuntaan: `App → Ui → Client → Core` ja `Api → Core`.

## Käynnistys

Vaatimukset: .NET 10 SDK. Mobiilisovellukseen lisäksi `dotnet workload install maui`
(Android SDK; iOS-käännös vaatii Macin ja Xcoden).

```bash
# 1. API (muistivarasto, ei tietokantaa tarvita)
dotnet run --project src/Matikka.Api            # http://localhost:5080

# 2. Käyttöliittymä selaimessa: kehitä tällä, muutokset näkyvät heti
dotnet watch --project dev/Matikka.DevHost      # http://localhost:5090

# 3. Mobiilisovellus
dotnet build src/Matikka.App -t:Run -f net10.0-android
dotnet build src/Matikka.App -t:Run -f net10.0-ios       # Macilla

# Testit
dotnet test tests/Matikka.Core.Tests
dotnet test tests/Matikka.Client.Tests
dotnet test tests/Matikka.Api.Tests

# API + PostgreSQL Dockerissa
docker compose up --build
```

Selainikkunan voi kaventaa puhelimen levyiseksi (kehittäjätyökalujen laitetila), niin näkymä vastaa sovellusta.
Koko ratkaisun `dotnet build Matikka.slnx` vaatii MAUI-työkuorman; ilman sitä rakenna projektit erikseen.

## Keskeiset ratkaisut

**Tapahtumat ovat ainoa tallennettava asia.** Jokainen pelattu tehtävä on muuttumaton `GameEvent`
(taso, siemen, siirrot, tähdet, kesto). Taitoluvut, tähdet ja putket lasketaan tapahtumista
(`ProgressModel.Replay`). Siksi kahden laitteen yhdistäminen on pelkkää tapahtumien yhdistämistä,
synkronointi on idempotentti ja uusia tilastoja voi laskea jälkikäteen vanhasta datasta.

**Tehtävät generoidaan.** Taso + siemen tuottaa aina saman tehtävän kaikilla alustoilla
(oma deterministinen satunnaislukugeneraattori). Testit tarkistavat jokaiselta tasolta 2000 tehtävää:
ratkaisu on kokonaisluku, tehtävä ratkeaa par-siirroilla ja vihje johtaa aina ratkaisuun.

**Adaptiivisuus Elo-mallilla** (vrt. Math Garden). Pelaajan taitoluku ja tason vaikeus ovat samalla
asteikolla. "Pelaa"-nappi valitsee avoimista tasoista sen, jossa onnistumisen odotus on lähimpänä 75 %:a,
ja tarjoaa välillä kertaustehtävän tasoa alempaa. Tasojen vaikeudet (`EquationBalanceGame.Info`)
ovat alkuarvauksia; kun dataa kertyy, ne kannattaa kalibroida palvelimen `events`-taulusta.

**Pisteytys kuin golfissa.** Tehtävästä ei voi epäonnistua. Par-siirroilla 3 tähteä, vihjeellä enintään 2.
Väärä siirto (esim. jako, joka ei mene tasan) selitetään eikä lasketa siirroksi.

**Tietosuoja.** Nimimerkki ja luokka-aste ovat vain laitteella. Palvelin tuntee nimettömän tilin
(satunnainen token, tietokannassa vain sen SHA-256-tiiviste) ja pelitapahtumat. Tilin ja kaiken datan
voi poistaa sovelluksen asetuksista (App Store vaatii tämän).

**Domino generoidaan takaperin.** Ensin arvotaan erilliset vastaukset, sitten jokaiselle
yhtälö, jonka ratkaisu on seuraavan laatan vastaus. Siksi jokaisessa vaiheessa täsmälleen yksi
laatta sopii (testi tarkistaa 1000 peliä per taso). Merkkiansat-tasolla kädessä on aina sekä
x = a että x = -a, jolloin etumerkkivirhe johtaa väärään laattaan.

**Potenssipulmat ovat tarkkoja.** Generaattori arpoo laudan tason laattavalikoimasta, käy
leveyshaulla läpi kaikki tilat par-syvyyteen asti ja valitsee tavoitteeksi laatan, joka syntyy
aikaisintaan juuri par-siirrolla. Sama ratkaisija antaa vihjeen ja tunnistaa umpikujan (esim.
eksponentti kasvoi liian suureksi eikä jakajaa ole jäljellä). Jokainen yhdistyminen näytetään
laskusääntönä auki kirjoitettuna, esim. a² · a³ = a²⁺³ = a⁵.

**Koodipolun kentät rakennetaan malliohjelmasta.** Generaattori kirjoittaa ensin tason mukaisen
malliohjelman (esim. toistuva porraskuvio tai aliohjelma kolmessa kohdassa), kulkee sen reitin ja
istuttaa puut ympärille. Lohkoraja asetetaan niin, ettei pelkillä peruslohkoilla pääse: leveyshaku
tarkistaa, että lyhinkin peruslohko-ohjelma ylittää rajan. Tulkki tallentaa jokaisen askeleen, ja
käyttöliittymä animoi askeleet jälkikäteen. Loputon rekursio pysähtyy askel- ja operaatiorajaan.

## Uuden pelin lisääminen

1. Corehen: `GameInfo` (tunniste, taitoalue, tasot), säännöt, generaattori, `ToEvent`. Testit.
2. Lisää peli `GameCatalog.All`-listaan. Edistyminen, Elo ja synkronointi toimivat sellaisinaan.
3. Ui:hin näkymä ja linkki aloitussivulle.

Hyviä seuraavia: prosenttikauppa, lineaarisen funktion "osu kohteisiin" (k ja b), pinta-alapulmat.

## Ennen kauppajulkaisua

- [ ] Tilaus sovelluskaupan kautta: esim. Plugin.InAppBilling tai RevenueCat, kuitin tarkistus API:ssa
- [ ] Kirjautuminen useammalle laitteelle: Sign in with Apple ja Google, liitetään nykyiseen nimettömään tiliin
- [ ] Tuotanto-osoite `AppConfig.ApiBaseUrl`, https, EF Core -migraatiot `EnsureCreated`in tilalle
- [ ] Sovellustunniste `ApplicationId`, nimi ja ikonit `Matikka.App.csproj`:ssa
- [ ] Tietosuojaseloste ja huoltajan hyväksyntä (alle 13-v.), kauppojen lapsille suunnattujen sovellusten säännöt
- [ ] Kolikot ja ostettavat hahmot (CharacterService on valmis laajennettavaksi; kolikot ansaitaan vain pelaamalla)
- [ ] Tarkista `PrivacyInfo.xcprivacy` Microsoftin ajantasaisesta MAUI-ohjeesta

## Lisenssit

Fontti Lexend: SIL Open Font License 1.1 (`src/Matikka.Ui/wwwroot/fonts`).
