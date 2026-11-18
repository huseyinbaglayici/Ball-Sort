ORBIT SORT - TUBE SPRITES (revize v2)
=====================================

PROJEDEKİ ADLAR (Assets/Art/Sprites/Tube)
-----------------------------------------
Empty_Tube_Cap     -> Tube_Mouth
                      (ön yarısı slice_mouth.py ile Tube_RimFront olarak kesilir, topların üstüne çizilir)
Completed_Tube_Cap -> Tube_Stopper
Completed_Tube_Gem -> Tube_Gem

DOSYALAR
--------
Tube_Back.png            384x1024   Normal blend
Tube_Front.png           384x1024   ADDITIVE blend
Tube_Outline.png         384x1024   Normal blend
Empty_Tube_Cap.png       384x1024   Normal blend   (tüp dolu değilken)
Completed_Tube_Cap.png   384x384    Normal blend   (tüp dolunca)
Completed_Tube_Gem.png   384x384    Normal blend   (tüp dolunca, kapağın üstünde)

KATMAN SIRASI (alttan üste)
---------------------------
1. Tube_Back
2. [Enerji topları]
3. Tube_Front              (additive)
4. Tube_Outline
5. Empty_Tube_Cap          -> tüp dolunca GİZLENİR
   veya
5. Completed_Tube_Cap      -> tüp dolunca görünür
6. Completed_Tube_Gem      -> tüp dolunca görünür

KONUMLAR
--------
- Tüm sprite'lar aynı Pixels Per Unit ile import edilmeli, pivot: Center.
- Tube_Back, Tube_Front, Tube_Outline, Empty_Tube_Cap:
  aynı konumda, offset yok (0, 0).
- Completed_Tube_Cap ve Completed_Tube_Gem:
  tüp sprite'ının sol üst köşesine göre (0, -172) px, yani 172 px yukarıda.
  Pivot Center ile: tüp objesinin child'ı olarak localPosition
  x = 0, y = +492 px  (Unity'de: y = 492 / PPU).
- Cap ve Gem aynı konumda durur, Gem her zaman Cap'in üstünde.

ÖLÇÜLER (384x1024 sprite içinde)
--------------------------------
- Toplam tüp yüksekliği: ~1004 px, dikeyde ortalı
- Cam dış genişlik: 328 px
- Cam iç genişlik: 290 px
- En geniş nokta: 342 px (Empty_Tube_Cap üst altın bant)
  -> tüpler arası boşluğu buna göre ayarlayın
- Completed_Tube_Cap genişliği: 337 px

EKRAN
-----
- Canvas Scaler reference resolution: 1170x2532 (iPhone Pro)
- Tüp sprite'ı ekranda 264x703 gösterilir (ölçek 0.6875).
  Cap ve Gem de aynı ölçekle küçülür (264x264), offset'ler de aynı oranda.

TOP KONUMLARI
-------------
Top boyutu netleşince eklenecek (iç genişlik 290 px).

PROJEDEKİ ADLAR (Assets/Art/Sprites/Tube)
-----------------------------------------
Son paket (Orbit_Sort_Tube_Sprites) adları -> proje adları:
  Tube_Back            -> Tube_Back
  Tube_Front           -> Tube_Front      (additive)
  Tube_Glow            -> Tube_Outline
  Tube_Empty_Cap       -> Tube_Mouth      (+ Tube_RimFront: ArtSource/Tube/slice_mouth.py ile kesilen ön yarı)
  Tube_Completed_Cap   -> Tube_Stopper
  Tube_Completed_Gem   -> Tube_Gem
Tube_OutlineGlow ve Tube_CapGlow, Create Board Prefabs tarafından üretilir.
