Muzyka do easter egga "Toyota Yaris Luna" (why this dealer)
============================================================

Wrzuć tutaj plik o nazwie:  YarisDealer.mp3   (albo YarisDealer.ogg / YarisDealer.wav)
Nazwa bez rozszerzenia musi być dokładnie "YarisDealer" - gra ładuje go przez
Resources.Load<AudioClip>("Audio/YarisDealer").

Co się z nim dzieje:
- W rundzie, w której wylosowało się Yaris (20%, przełącznik -testyaris = 100%), autko RC
  gra ten utwór w pętli jako dźwięk 3D (słychać do ok. 24 m, przez ściany i stropy ciszej)
  TYLKO gdy ktoś nim jeździ. Gdy kierowca wysiada (auto stoi / na półce / w ręce), taniec
  i muzyka cichną w ~0.4 s, utwór się pauzuje i przy następnej jeździe gra dalej od tego miejsca.
  Głośność = efekty x muzyka z ustawień gry.
- Autko spłaszcza się w rytm utworu. Siatka tego pliku (Niko B - Why's this dealer) jest
  ustawiona na sztywno w Assets/_Project/Game/Fx/Yaris/YarisEgg.cs: MusicBpm = 135,
  MusicFirstBeat = 0.12 s (DetectBpm = false). Inny plik: zmień te liczby albo włącz DetectBpm
  i ustaw Load Type: Decompress On Load (wykrywanie potrzebuje nieskompresowanych próbek).
- Po rozdeptaniu autka muzyka „zwalnia jak kaseta” i milknie.
- Brak pliku = autko tańczy bez muzyki (120 BPM), w logu jedna linijka [Yaris].

Import w Unity: Compressed In Memory, Vorbis 60%, Force To Mono (dźwięk 3D), Preload Audio Data.
