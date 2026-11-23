Muzyka do easter egga "Toyota Yaris Luna" (why this dealer)
============================================================

Wrzuć tutaj plik o nazwie:  YarisDealer.mp3   (albo YarisDealer.ogg / YarisDealer.wav)
Nazwa bez rozszerzenia musi być dokładnie "YarisDealer" - gra ładuje go przez
Resources.Load<AudioClip>("Audio/YarisDealer").

Co się z nim dzieje:
- W rundzie, w której wylosowało się Yaris (20%, przełącznik -testyaris = 100%), autko RC
  postawione na ziemi albo sterowane gra ten utwór w pętli jako dźwięk 3D (słychać do ok. 24 m,
  przez ściany i stropy ciszej), głośność = efekty x muzyka z ustawień gry.
- Autko spłaszcza się i rozpłaszcza w rytm utworu. Tempo jest wykrywane automatycznie
  z obwiedni głośności (pierwsze 40 s utworu); jeśli wykrycie się nie uda, używane jest
  YarisEgg.Bpm (domyślnie 120) w Assets/_Project/Game/Fx/Yaris/YarisEgg.cs - tam też szansa,
  zasięg i głośność.
- Po rozdeptaniu autka muzyka „zwalnia jak kaseta” i milknie.
- Brak pliku = autko tańczy bez muzyki (120 BPM), w logu jedna linijka [Yaris].

Import w Unity: zostaw domyślne ustawienia (Load Type: Decompress On Load) - wtedy działa
wykrywanie tempa. Przy "Streaming" tempo nie jest wykrywane (używane jest Bpm).
