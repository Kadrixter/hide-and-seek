# Photo face

Drop the photo for the player with nick **cendy** here, named exactly:

```
Assets/_Project/Characters/Resources/Faces/opszczymorwformie.png
```

- Any size or aspect ratio works; it is centre-cropped to a square (slightly biased towards the top) and
  shown on the front of the character's head. A tightly cropped, front-facing face photo looks best.
- Import settings (readable, clamp, uncompressed) are applied automatically by
  `CharacterTexturePostprocessor`.
- If the file is missing, the character silently falls back to the normal pixel-art face.
- Loaded at runtime with `Resources.Load<Texture2D>(GameLayers.SpecialFaceResource)`.
