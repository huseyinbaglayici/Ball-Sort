ORBIT SORT - ORB SPRITES
========================

GENEL
-----
- Tüm orb sprite'ları 256x256 px, şeffaf zemin, orb tam ortada.
- Orb (kabuk) çapı: 180 px. Dış ışıma ile birlikte ~203 px.
- Tüp sprite'larıyla AYNI Pixels Per Unit ile import edin, ölçeklemeyin.
  Oranlar bu şekilde birebir doğru çıkar.
- Pivot: Center.

DOSYALAR
--------
Ortak (tüm renklerde aynı, 1'er adet):
  Orb_Back.png               Normal blend
  Orb_Front.png              ADDITIVE blend (siyah zeminli cam yansıması)

Renge özel (her renk için 3 dosya):
  Orb_Core_<Renk>.png        Normal blend
  Orb_InnerGlow_<Renk>.png   Normal blend
  Orb_OuterGlow_<Renk>.png   Normal blend

Renkler (<Renk> yerine):
  Blue     #1C1FFF
  Pink     #FF70D9
  Red      #FF120D
  Green    #B3FF0D
  Orange   #FF6B0D
  Purple   #A836FF
  Yellow   #FFC40D
  Cyan     #00FFEB

Örnek (mor): Orb_Core_Purple.png, Orb_InnerGlow_Purple.png,
             Orb_OuterGlow_Purple.png

KATMAN SIRASI (alttan üste, bir orb için)
-----------------------------------------
1. Orb_OuterGlow_<Renk>
2. Orb_Back
3. Orb_Core_<Renk>
4. Orb_Front              (additive)
5. Orb_InnerGlow_<Renk>

Beşi de aynı konumda, offset yok. Bir prefab içinde child olarak
tutulması önerilir; renk değişince sadece 3 renk sprite'ı değişir.

TÜP İÇİNDEKİ KONUMLAR
---------------------
Tüp sprite'ı 384x1024. Toplar tüp katmanlarının ARASINA girer:

  Tube_Back
  [Orb'lar]
  Tube_Front (additive)
  Tube_Outline
  Empty_Tube_Cap / Completed_Tube_Cap + Gem

Orb merkezleri (tüp sprite'ının sol üst köşesine göre, px):
  Slot 1 (en alt)   x = 192   y = 807
  Slot 2            x = 192   y = 624
  Slot 3            x = 192   y = 441
  Slot 4 (en üst)   x = 192   y = 258

Tüp pivot'u Center iken orb localPosition (px, Unity'de / PPU):
  Slot 1   x = 0   y = -295
  Slot 2   x = 0   y = -112
  Slot 3   x = 0   y =  +71
  Slot 4   x = 0   y = +254

Slotlar arası mesafe: 183 px. Tüp kapasitesi: 4 orb.

EKRAN
-----
- Canvas Scaler reference resolution: 1170x2532 (iPhone Pro).
- Tüp ekranda 264x703 gösterilir (ölçek 0.6875); orb'lar ve
  offset'ler de aynı oranda küçülür (orb ~124 px).

NOTLAR
------
- InnerGlow ve OuterGlow ayrı olduğu için seçili / tamamlanan
  orb'larda opaklıkları animasyonla nabız gibi atabilir (opsiyonel).
- Gizli top (asteroid) ve kilitli tüp (cryo) sprite'ları ayrıca
  gönderilecek.
