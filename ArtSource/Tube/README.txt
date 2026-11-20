ORBIT SORT - TUBE & ORB SPRITES (v3 - buyuk toplar)
====================================================

Bu dosya tup ve orb (top) sprite'larinin TAMAMINI kapsar.
Eski v2 README'deki tup ve orb degerleri GECERSIZDIR, bunu kullanin.


1. GENEL KURALLAR
-----------------
- Tum sprite'lar AYNI Pixels Per Unit (PPU) ile import edilmeli.
  Hicbir sprite'i Unity'de ayri olceklemeyin; oranlar birebir dogru gelir.
- Pivot: Center (tum sprite'lar).
- Tum PNG'ler seffaf zemindir.
- Asagidaki tum piksel degerleri 384x1024 tup sprite'inin
  SOL UST KOSESINE gore verilmistir (x saga, y asagi artar).
- Unity karsiliklari "tup pivot = Center" kabul edilerek verilmistir
  (Unity'de y yukari artar). Unity birimi = piksel / PPU.


2. TUP DOSYALARI
----------------
Dosya                     Boyut       Blend     Konum (sol ust)
Tube_Back_Glass.png       384x1024    Normal    (0, 0)
Tube_End_Piece.png        384x1024    Normal    (0, 0)
Tube_Front_Glass.png      384x1024    ADDITIVE  (0, 0)
Tube_Glow.png             384x1024    Normal    (0, 0)
Tube_Empty_Cap.png        384x1024    Normal    (0, 0)
Tube_Completed_Cap.png    384x384     Normal    (0, -192)
Tube_Completed_Gem.png    384x384     Normal    (0, -192)

Not: Tube_Front_Glass siyah zeminlidir. Normal blend ile konursa
toplari tamamen kapatir. Mutlaka Additive (veya Screen) kullanin.


3. KATMAN SIRASI (alttan uste)
------------------------------
1. Tube_Back_Glass
2. Tube_End_Piece
3. [ORB'LAR]                    <- toplar tup katmanlarinin ARASINDA
4. Tube_Front_Glass   (additive)
5. Tube_Glow
6a. Tube_Empty_Cap              -> tup DOLU DEGILKEN gorunur
6b. Tube_Completed_Cap          -> tup DOLUNCA gorunur
7.  Tube_Completed_Gem          -> tup DOLUNCA gorunur, Cap'in ustunde

Tup dolunca: Tube_Empty_Cap GIZLENIR, Tube_Completed_Cap ve
Tube_Completed_Gem gorunur hale gelir. Ikisi ayni anda gorunmez.


4. KAPAK KONUMLARI (tam oturma icin)
------------------------------------
Tube_Empty_Cap:
  - 384x1024, tup ile ayni konum, offset yok (0, 0).
  - Unity: localPosition (0, 0).

Tube_Completed_Cap ve Tube_Completed_Gem:
  - 384x384 sprite. Sol ust kosesi tup sprite'inin (0, -192) noktasinda,
    yani 192 px yukarida.
  - Sprite merkezi tup sprite'inda (192, 0) noktasina denk gelir.
  - Unity (tup pivot Center): localPosition x = 0, y = +512 px
    (Unity'de: y = 512 / PPU).
  - Cap ve Gem birebir ayni konumdadir, Gem her zaman Cap'in ustunde.
  - Bu konumda kapagin alt kenari tam camin basladigi cizgiye oturur,
    en ustteki top tamamen gorunur kalir.


5. TUP OLCULERI (384x1024 sprite icinde)
----------------------------------------
- Tup toplam yuksekligi (kapak dahil): y = 10 ... 1014
- Cam dis genislik: 329 px (x = 28 ... 356)
- Empty_Cap en genis: 339 px
- Completed_Cap en genis: 338 px
- End_Piece (kaide) en genis: 334 px
- Tube_Glow en genis (isima dahil): 350 px
  -> Tupler arasi bosluk icin en az 350 px gorsel genislik hesaplayin.


6. ORB (TOP) DOSYALARI
----------------------
Her orb 5 katmandan olusur. Tum katmanlar 256x256, orb tam ortada.

Ortak (tum renklerde ayni, 1'er adet):
  Orb_Back.png                Normal
  Orb_Front.png               ADDITIVE (siyah zeminli cam yansimasi)

Renge ozel (her renk icin 3 dosya):
  Orb_Core_<Renk>.png         Normal
  Orb_InnerGlow_<Renk>.png    Normal
  Orb_OuterGlow_<Renk>.png    Normal

!!! Orb_CoreGlow.png KULLANILMAYACAK. !!!
Orb'un ortasina hicbir merkez isigi / core glow / beyaz parlama
eklenmeyecek. Gonderilen eski pakette Orb_CoreGlow.png varsa silin.

Renkler (<Renk> yerine):
  LightBlue   Body #0198D8   Surface #5DC1FF
  DarkBlue    Body #1616BF   Surface #2955DD
  Orange      Body #E45900   Surface #EC7C25
  LightGreen  Body #70A502   Surface #A0C338
  DarkGreen   Body #0B3C01   Surface #2F6016
  White       Body #5D64A6   Surface #8086B9
  Wine        Body #6B0052   Surface #8E1667
  Brown       Body #70322B   Surface #A44734
  Yellow      Body #FCC006   Surface #FFD553
  Purple      Body #5F1E8E   Surface #963FCC
  Pink        Body #FE6FD7   Surface #F5A6DA
  Red         Body #C40602   Surface #E23936

Ornek (pembe): Orb_Core_Pink.png, Orb_InnerGlow_Pink.png,
               Orb_OuterGlow_Pink.png


7. ORB KATMAN SIRASI (alttan uste, tek orb)
-------------------------------------------
1. Orb_OuterGlow_<Renk>
2. Orb_Back
3. Orb_Core_<Renk>
4. Orb_Front            (additive)
5. Orb_InnerGlow_<Renk>

Besi de ayni konumda, offset yok. Bir prefab icinde child olarak
tutulmasi onerilir; renk degisince sadece 3 renk sprite'i degisir.


8. ORB BOYUTU
-------------
- Orb (kabuk) capi: 200 px  (256x256 sprite icinde, tam ortada)
- Orb sprite'i: 256x256, OLCEKLEMEDEN kullanilir.
- Toplar arasi mesafe (merkezden merkeze): 203 px
- Yanlarda camla top arasi bosluk: ~45 px

Not: Eski paketteki orb'lar 181 px idi. Yeni (200 px) orb paketi
gonderilene kadar eski orb'lari test icin kullanacaksaniz 1.105
olcekle buyutun. Yeni paket geldiginde olcek 1.0 olacak.


9. ORB KONUMLARI (TUP ICINDE)
-----------------------------
Orb merkezleri (tup sprite'inin sol ust kosesine gore, piksel):

  Slot 1 (en alt)   x = 192   y = 840
  Slot 2            x = 192   y = 638
  Slot 3            x = 192   y = 435
  Slot 4 (en ust)   x = 192   y = 232

Unity localPosition (tup pivot Center, Unity'de y yukari):

  Slot 1   x = 0   y = -328
  Slot 2   x = 0   y = -126
  Slot 3   x = 0   y =  +77
  Slot 4   x = 0   y = +280
  (Unity birimi: deger / PPU)

256x256 orb sprite'inin sol ust kosesi icin (Photoshop yerlesimi):
  Slot 1  (64, 712)   Slot 2  (64, 510)
  Slot 3  (64, 307)   Slot 4  (64, 104)

Tup kapasitesi: 4 orb.


10. EKRAN
---------
- Canvas Scaler reference resolution: 1170x2532 (iPhone Pro).
- Tup ekranda 264x703 gosterilir (olcek 0.6875).
- Orb'lar, kapaklar ve tum offset'ler ayni oranda kuculur
  (orb ekranda ~138 px).


11. KONTROL LISTESI
-------------------
[ ] Tum sprite'lar ayni PPU, pivot Center, olceksiz
[ ] Tube_Front_Glass ve Orb_Front -> Additive
[ ] Orb'lar Back_Glass/End_Piece ile Front_Glass ARASINDA
[ ] Orb_CoreGlow.png kullanilmiyor
[ ] Orb merkezleri: 840 / 638 / 435 / 232 (x = 192)
[ ] Tup dolunca Empty_Cap gizli, Completed_Cap + Gem (0, -192)'de


12. SONRADAN GELECEKLER
-----------------------
Gizli top (asteroid), bomba, kilitli tup (kepenk + kilit) ve
donmus tup sprite'lari yeni olculere gore ayrica gonderilecek.


PROJE NOTU
----------
Tube_RimFront.png: Tube_Empty_Cap.png ağzının ön yarısı (ArtSource/Tube/slice_mouth.py ile kesilir); topların üstüne çizilir, Empty_Cap topların arkasında kalır.
