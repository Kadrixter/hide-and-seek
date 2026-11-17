# Hide & Seek — Game Design Document

## Koncept
Wieloosobowa gra w chowanego 3D (PC). Mapy są całkowicie ciemne (noc). Szukający widzi tylko to, co oświetli latarką.
Postacie: blokowe, w stylu Minecrafta (głowa / tułów / ręce / nogi z prostopadłościanów, tekstury pikselowe 64x64).

## Zasady rundy
| Parametr | Domyślnie | Ustawialne w lobby |
|---|---|---|
| Czas na schowanie | 30 s | tak |
| Czas szukania | 600 s (10 min) | tak |
| Start podpowiedzi | połowa czasu szukania | tak |
| Interwał podpowiedzi | 60 s | tak |
| Tryb podpowiedzi | dźwięk / minimapa / oba | tak |
| Min. graczy | 2 | — |
| Max. graczy | brak | — |

Przebieg: lobby → każdy gracz klika **Ready** → losowanie szukającego → faza chowania (szukający zablokowany, ekran czarny)
→ faza szukania → podpowiedzi (dźwięk `.mp3` z folderu `voice` odtwarzany w pozycji chowającego i/lub pinezka na minimapie)
→ koniec: wszyscy znalezieni (wygrywa szukający) lub koniec czasu (wygrywają ukryci).

## HUD
- Prawa strona: lista graczy — Szukający / Chowający się / Znaleziony.
- Lewy dolny róg: minimapa.
- Licznik czasu u góry.

## Sterowanie
| Akcja | Klawisz |
|---|---|
| Ruch | WASD |
| Bieg | Shift |
| Kucanie | Ctrl / C |
| Skok | Spacja |
| Latarka on/off | LPM |

Kamera FPP z dopracowanym head-bobbingiem (inny rytm dla chodu, biegu i kucania).

## Dźwięk
- Dopracowane kroki (beton / metal / woda), skok, lądowanie, szelest ubrania; kroki innych graczy jako dźwięk 3D (bieg słychać z ~25 m, kucanie z ~6 m).
- Ambient map (wentylacja, kapanie, buczenie lamp) — generowany przez ElevenLabs.
- Podpowiedzi głosowe: `Assets/_Project/Audio/Voice/*.mp3`.

## Skiny
5 wariantów kolorystycznych tego samego modelu. Gracz o nicku **cendy** ma twarz z pliku
`Assets/_Project/Characters/Resources/Faces/opszczymorwformie.png`.

## Mapy
1. **Podziemny parking** — 3 piętra po ok. 180 x 120 m, połączone rampami i klatkami schodowymi; filary, zaparkowane auta, strefy (myjnia, budka ochrony, strefa remontowa, rampa załadunkowa, tunel serwisowy), pomieszczenia techniczne. Poziom -3 starszy i częściowo zalany.
2. Labirynt (później).

## Architektura (Assets/_Project)
| Moduł | asmdef | Odpowiedzialność |
|---|---|---|
| Core | HideAndSeek.Core | kontrakty (ICharacterMotionSource), GameSettings, stałe |
| Map | HideAndSeek.Map | proceduralne budowanie map, tekstury, oświetlenie |
| Characters | HideAndSeek.Characters | blokowy model, skiny, proceduralne animacje |
| Player | HideAndSeek.Player | kontroler ruchu, kamera, head-bob, latarka |
| Game | HideAndSeek.Game | spięcie modułów, sceny, (później) sieć i zasady rundy |

Sieć (kolejny etap): Netcode for GameObjects + Unity Relay/Lobby.

## Zakres BETA 1
- 1 model postaci + scena podglądu
- duża mapa parkingu podziemnego (2 piętra)
- chodzenie / bieganie / kucanie, head-bob, latarka (LPM)
