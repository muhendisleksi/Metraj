# Yol Enkesit Okuma Sistemi — Mimari Plan

**Proje:** Metraj-master / Yol Metraj Modülü v2
**Tarih:** 2026-03-28
**Amaç:** İhale DWG dosyalarından enkesit verilerini yarı-otomatik okuyarak alan hesabı yapan ve firmanın tablolarıyla kıyaslayan sistem.

---

## 1. Genel İş Akışı

```
[Toplu Seçim] → [Anchor Tarama] → [Pencere Kalibrasyonu] → [Entity Toplama]
     → [Çizgi Rol Atama (Kalibrasyon)] → [Otomatik Eşleşme] → [Alan Hesabı]
     → [Tablo Okuma & Kıyaslama] → [WPF Doğrulama] → [Hacim & Excel Çıktı]
```

### Kullanıcı Etkileşimleri (toplam 4 adım)
1. Tüm entity'leri seç (window select)
2. İlk kesitin pencere boyutunu belirle (2 köşe tıkla)
3. Referans kesitte çizgilere rol ata (WPF ekranında)
4. Doğrulama ekranında hızlı tarama (ileri/geri, sadece hatalıları düzelt)

---

## 2. Modül Yapısı

### 2.1 Yeni Models

```
Models/
├── YolEnkesit/
│   ├── AnchorNokta.cs              — İstasyon text'inin konumu ve km bilgisi
│   ├── KesitPenceresi.cs           — Pencere boyutu ve anchor'dan offset
│   ├── CizgiRolu.cs                — Enum: çizgi rollerinin tanımı
│   ├── CizgiTanimi.cs              — Bir çizginin geometrisi + atanan rol
│   ├── ReferansKesitSablonu.cs     — Kalibrasyon şablonu (layer/renk → rol)
│   ├── KesitGrubu.cs               — Bir kesitin tüm entity'leri ve çizgi tanımları
│   ├── AlanHesapSonucu.cs          — Çizgi çifti arası hesaplanan alan + malzeme adı
│   ├── TabloKiyasSonucu.cs         — Geometri alanı vs tablo değeri karşılaştırması
│   └── TopluTaramaSonucu.cs        — Tüm kesitlerin toplu sonucu
```

### 2.2 Yeni Services

```
Services/
├── YolEnkesit/
│   ├── IAnchorTaramaService.cs     — Interface
│   ├── AnchorTaramaService.cs      — Text tarama + anchor tespiti
│   ├── IKesitGruplamaService.cs    — Interface
│   ├── KesitGruplamaService.cs     — Pencere bazlı entity toplama
│   ├── ICizgiRolAtamaService.cs    — Interface
│   ├── CizgiRolAtamaService.cs     — Otomatik + manuel rol atama
│   ├── IKesitAlanHesapService.cs   — Interface
│   ├── KesitAlanHesapService.cs    — Çizgi çiftleri arası alan hesabı
│   ├── ITabloOkumaService.cs       — Interface
│   └── TabloOkumaService.cs        — DWG'deki malzeme tablosunu parse etme
```

### 2.3 Yeni ViewModels & Views

```
ViewModels/
├── EnkesitOkuma/
│   ├── EnkesitOkumaMainViewModel.cs    — Ana wizard orkestrasyon VM
│   ├── ReferansKesitViewModel.cs       — Kalibrasyon penceresi VM
│   └── KesitDogrulamaViewModel.cs      — Doğrulama penceresi VM

Views/
├── EnkesitOkuma/
│   ├── EnkesitOkuMainControl.xaml      — PaletteSet'e eklenen ana kontrol
│   ├── ReferansKesitWindow.xaml        — Kalibrasyon penceresi (modal)
│   ├── KesitDogrulamaWindow.xaml       — Doğrulama penceresi (modal)
│   └── KesitOnizlemeControl.xaml       — 2D kesit görüntüleme (paylaşılan)

Commands/
├── EnkesitOkuCommands.cs               — YOLENKESITOKU + diğer komutlar
├── EnkesitOkuPaletteManager.cs         — PaletteSet oluşturma/gösterme
```

---

## 3. Model Detayları

### 3.1 CizgiRolu (Enum)

```csharp
public enum CizgiRolu
{
    Tanimsiz = 0,

    // Ana referans çizgileri
    Zemin,              // Doğal zemin / siyah kot (yeşil çizgi)
    SiyirmaTaban,       // Sıyırma tabanı (mavi çizgi)
    ProjeKotu,          // Kırmızı kot / platform üstü (kırmızı çizgi)
    UstyapiAltKotu,     // Üstyapı en alt seviyesi (kırmataş/formasyon tabanı)

    // Üstyapı tabakaları (yukarıdan aşağıya)
    AsinmaTaban,        // Aşınma tabakası alt çizgisi
    BinderTaban,        // Binder alt çizgisi
    BitumluTemelTaban,  // Bitümlü temel alt çizgisi
    PlentmiksTaban,     // Plentmiks temel alt çizgisi
    AltTemelTaban,      // Alttemel alt çizgisi
    KirmatasTaban,      // Kırmataş temel alt çizgisi

    // Özel elemanlar
    HendekCizgisi,      // Hendek/kanal dış sınırı
    SevCizgisi,         // Yarma/dolgu şevi
    BanketCizgisi,      // Banket sınırı
    EksenCizgisi,       // CL - merkez eksen

    // Filtrelenenler
    CerceveCizgisi,     // Kesit çerçevesi (hesaba dahil değil)
    GridCizgisi,        // Ölçek grid çizgileri (hesaba dahil değil)
    Diger               // Tanınmayan çizgiler
}
```

### 3.2 AnchorNokta

```csharp
public class AnchorNokta
{
    public double Istasyon { get; set; }          // metre: 820.0
    public string IstasyonMetni { get; set; }     // "0+820"
    public double X { get; set; }                 // Text'in DWG X koordinatı
    public double Y { get; set; }                 // Text'in DWG Y koordinatı
    public ObjectId TextId { get; set; }          // Kaynak text objesi
}
```

### 3.3 KesitPenceresi

```csharp
public class KesitPenceresi
{
    public double Genislik { get; set; }          // Pencere W
    public double Yukseklik { get; set; }         // Pencere H
    public double OffsetSolX { get; set; }        // Anchor'dan sola mesafe
    public double OffsetSagX { get; set; }        // Anchor'dan sağa mesafe
    public double OffsetAltY { get; set; }        // Anchor'dan aşağı mesafe
    public double OffsetUstY { get; set; }        // Anchor'dan yukarı mesafe
}
```

### 3.4 CizgiTanimi

```csharp
public class CizgiTanimi
{
    public ObjectId EntityId { get; set; }
    public CizgiRolu Rol { get; set; } = CizgiRolu.Tanimsiz;
    public string LayerAdi { get; set; }
    public short RenkIndex { get; set; }
    public List<Point2d> Noktalar { get; set; }   // Polyline vertex'leri
    public bool OtomatikAtanmis { get; set; }     // Kalibrasyon eşleşmesiyle mi?
    public double OrtalamaY { get; set; }          // Dikey pozisyon (sıralama için)
}
```

### 3.5 ReferansKesitSablonu

```csharp
public class RolEslestirmeKurali
{
    public CizgiRolu Rol { get; set; }
    public string LayerPattern { get; set; }       // "ZEMIN*", "SIYAH_KOT*"
    public short? RenkIndex { get; set; }          // null = herhangi renk
    public double? BagintiliYPozisyonu { get; set; } // CL'ye göre normalize Y
}

public class ReferansKesitSablonu
{
    public List<RolEslestirmeKurali> Kurallar { get; set; }
    public KesitPenceresi Pencere { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public string ProjeAdi { get; set; }
}
```

### 3.6 KesitGrubu

```csharp
public class KesitGrubu
{
    public AnchorNokta Anchor { get; set; }
    public List<CizgiTanimi> Cizgiler { get; set; }
    public List<ObjectId> TextObjeler { get; set; }    // Kesit içindeki text'ler
    public List<AlanHesapSonucu> HesaplananAlanlar { get; set; }
    public List<TabloKiyasSonucu> TabloKiyaslari { get; set; }
    public DogrulamaDurumu Durum { get; set; }

    // Kolay erişim
    public CizgiTanimi Zemin => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.Zemin);
    public CizgiTanimi ProjeKotu => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.ProjeKotu);
    public CizgiTanimi SiyirmaTaban => Cizgiler?.FirstOrDefault(c => c.Rol == CizgiRolu.SiyirmaTaban);
}

public enum DogrulamaDurumu
{
    Bekliyor,         // Henüz doğrulanmadı
    Onaylandi,        // Kullanıcı onayladı
    Duzeltildi,       // Kullanıcı düzeltme yaptı
    Sorunlu           // Eşleşme bulunamadı veya fark yüksek
}
```

### 3.7 AlanHesapSonucu

```csharp
public class AlanHesapSonucu
{
    public string MalzemeAdi { get; set; }         // "Yarma", "Dolgu", "Sıyırma", "Aşınma"...
    public double Alan { get; set; }               // m²
    public CizgiRolu UstCizgiRolu { get; set; }    // Hangi çizgi çifti kullanıldı
    public CizgiRolu AltCizgiRolu { get; set; }
    public string Aciklama { get; set; }           // "Sıyırma tabanı - Üstyapı alt kotu arası"
}
```

### 3.8 TabloKiyasSonucu

```csharp
public class TabloKiyasSonucu
{
    public string MalzemeAdi { get; set; }
    public double TabloAlani { get; set; }         // DWG tablosundan okunan
    public double HesaplananAlan { get; set; }     // Geometriden hesaplanan
    public double Fark { get; set; }               // m²
    public double FarkYuzde { get; set; }          // %
    public bool Uyumlu { get; set; }               // Tolerans içinde mi?
}
```

---

## 4. Servis Detayları

### 4.1 AnchorTaramaService

**Girdi:** SelectionSet (tüm seçilen entity'ler)
**Çıktı:** List<AnchorNokta> (km sıralı)

**Mantık:**
1. SelectionSet'teki tüm DBText ve MText objelerini tara
2. Her text'in içeriğine `IstasyonParse()` regex'i uygula (mevcut YolKesitService'ten)
3. Eşleşenleri AnchorNokta olarak topla
4. Km'ye göre sırala
5. Duplike anchor kontrolü (aynı km'de birden fazla text varsa yakın olanı al)

**Mevcut koddan kullanılan:** `YolKesitService.IstasyonParse()`, `NumberParserHelper`

### 4.2 KesitGruplamaService

**Girdi:** List<AnchorNokta>, KesitPenceresi, SelectionSet
**Çıktı:** List<KesitGrubu>

**Mantık:**
1. Her anchor için pencere koordinatlarını hesapla:
   - minX = anchor.X - pencere.OffsetSolX
   - maxX = anchor.X + pencere.OffsetSagX
   - minY = anchor.Y - pencere.OffsetAltY
   - maxY = anchor.Y + pencere.OffsetUstY
2. `Editor.SelectCrossingWindow(min, max)` ile entity'leri topla
3. Entity'lerden polyline/line olanları CizgiTanimi'ne dönüştür
4. Text objeleri ayrı listele (tablo okuma için)
5. Çakışma kontrolü: bir entity birden fazla pencereye düşüyorsa en yakın anchor'a ata

**Mevcut koddan kullanılan:** `EnKesitAlanService.PolylineNoktalariniAl()`

### 4.3 CizgiRolAtamaService

**İki mod:**

**A) Kalibrasyon modu (ilk kesit):**
- KesitGrubu'ndaki çizgiler WPF ekranında gösteriliyor
- Kullanıcı her çizgiye tıklayıp rol atıyor
- Sistem layer/renk/pozisyon bilgisini RolEslestirmeKurali olarak kaydediyor
- Çıktı: ReferansKesitSablonu

**B) Otomatik mod (diğer kesitler):**
- ReferansKesitSablonu'ndaki kuralları sırayla uygula:
  1. Önce layer adı eşleşmesi (en güvenilir)
  2. Sonra renk eşleşmesi
  3. Son olarak relative Y pozisyonu (CL'ye göre normalize)
- Eşleşemeyen çizgiler CizgiRolu.Tanimsiz kalır
- Tanimsiz çizgi sayısı belirli eşiği aşarsa DogrulamaDurumu = Sorunlu

**Mevcut koddan kullanılan:** `KatmanEslestirmeService`, `EnKesitAlanService.MalzemeAdiCikar()`

### 4.4 KesitAlanHesapService

**Girdi:** KesitGrubu (rolleri atanmış çizgilerle)
**Çıktı:** List<AlanHesapSonucu>

**Hesap kuralları (çizgi çiftleri):**

```
Malzeme              | Üst Çizgi           | Alt Çizgi            | Koşul
---------------------|----------------------|----------------------|------------------
Sıyırma              | Zemin                | SiyirmaTaban         | Her zaman
Kazı (Yarma)         | SiyirmaTaban         | UstyapiAltKotu       | Sıyırma tabanı > Üstyapı alt
Dolgu                | UstyapiAltKotu       | SiyirmaTaban         | Üstyapı alt > Sıyırma tabanı
B.T. Yerine Konan    | [Sıyırma alanı]      | —                    | Dolgu bölgesindeki sıyırma
B.T. Yerine Konmayan | [Sıyırma alanı]      | —                    | Yarma bölgesindeki sıyırma
Aşınma               | ProjeKotu            | AsinmaTaban          | Her zaman
Binder               | AsinmaTaban          | BinderTaban          | Her zaman
Bitümlü Temel        | BinderTaban          | BitumluTemelTaban    | Her zaman
Plentmiks            | BitumluTemelTaban    | PlentmiksTaban       | Her zaman
Alttemel             | PlentmiksTaban       | AltTemelTaban        | Her zaman
Kırmataş             | AltTemelTaban        | KirmatasTaban        | Varsa (= UstyapiAltKotu)
```

**Yarma/Dolgu/B.T. bölünme mantığı:**
1. Sıyırma tabanı ile üstyapı alt kotu kesişim X'ini bul
2. Kesişimden sola: hangi çizgi üstteyse o alan türü
3. Kesişimden sağa: aynı mantık
4. Sıyırma alanını da aynı X'ten böl → yerine konan/konmayan

**Mevcut koddan kullanılan:**
- `EnKesitAlanService.IkiCizgiArasiAlan()` — temel alan hesabı
- `EnKesitAlanService.ClipToXRange()` — çizgi kesme
- `EnKesitAlanService.InterpolateY()` — Y enterpolasyonu
- `EnKesitAlanService.ShoelaceAlan()` — polygon alan formülü

### 4.5 TabloOkumaService

**Girdi:** KesitGrubu (TextObjeler listesi)
**Çıktı:** Dictionary<string, double> (malzeme adı → alan değeri)

**Mantık:**
1. Text objelerini tara, tablo formatına uyan olanları bul
2. Malzeme tablosu tespit kuralları:
   - "Yarma", "Dolgu", "Aşınma", "Binder" gibi anahtar kelimeler
   - Yanında sayısal değer (alan m²)
3. Key-value çiftleri olarak parse et
4. KesitAlanHesapService sonuçlarıyla karşılaştır → TabloKiyasSonucu listesi

**Mevcut koddan kullanılan:** `NumberParserHelper`, `EnKesitAlanService.MalzemeAdiCikar()`

---

## 5. WPF Ekranları — Ayrı Panel (PaletteSet)

Enkesit Oku sistemi Metraj Asistanı'ndan tamamen bağımsız, kendi PaletteSet paneli olarak çalışır.
Komut: YOLENKESITOKU → EnkesitOkuPaletteSet açılır.
Kendi ribbon butonu, kendi DI kayıtları, kendi JSON kayıt dosyası.

### 5.1 EnkesitOkuMainControl (Ana Panel)

PaletteSet'e eklenen tek UserControl. Wizard tarzı adım adım akış.

**Üst bölüm:** Adım göstergesi (step indicator)
```
[1. Seçim] → [2. Tarama] → [3. Kalibrasyon] → [4. Hesap] → [5. Doğrulama] → [6. Çıktı]
   ●            ○               ○                ○              ○               ○
```
Aktif adım vurgulu, tamamlananlar yeşil tik, bekleyenler gri.

**Orta bölüm:** Aktif adımın içeriği (ContentControl ile değişir)

**Alt bölüm:** Sabit durum çubuğu + ileri/geri butonları

#### Adım 1 — Seçim
- [Entity Seç] butonu → SelectionSet oluştur
- Sonuç: "24.580 entity seçildi"
- [İleri →] aktif olur

#### Adım 2 — Anchor Tarama + Pencere
- [Anchor Tara] → otomatik çalışır, sonuç gösterilir
- "84 istasyon text'i bulundu (0+000 ... 0+830)"
- [Pencere Belirle] → 2 köşe tıklama (AutoCAD'e geçiş)
- Sonuç: "Pencere: 62.0 × 18.5 birim"
- [İleri →]

#### Adım 3 — Kalibrasyon
- [Kalibrasyon Aç] → ReferansKesitWindow (modal, büyük) açılır
- Veya [Şablon Yükle] → daha önce kaydedilmiş .json şablon
- Tamamlanınca: "12 çizgi rolü tanımlandı"
- [İleri →]

#### Adım 4 — Toplu Tarama + Hesap
- [Toplu Tara] → progress bar ile tüm kesitler işlenir
- "84/84 kesit tarandı, 1890 çizgi eşleştirildi"
- Otomatik alan hesabı yapılır
- İstatistik: "71 uyumlu, 8 uyarı, 5 sorunlu"
- [İleri →]

#### Adım 5 — Doğrulama
- [Doğrulama Aç] → KesitDogrulamaWindow (modal, büyük) açılır
- Tamamlanınca: "84 kesit doğrulandı (79 onay, 5 düzeltme)"
- [İleri →]

#### Adım 6 — Çıktı
- Hacim hesap metodu seçimi (Ortalama Alan / Prismoidal)
- [Hesapla] → hacim sonuçları panelde gösterilir
- Toplam kazı / dolgu / net hacim
- [Excel Aktar] → dosya kaydedilir
- [JSON Kaydet] → proje verisi + şablon kaydedilir

### 5.2 ReferansKesitWindow (Modal Pencere — büyük)

Kalibrasyon adımında açılan bağımsız WPF Window.
Boyut: 900×600 veya ekranın %70'i, boyutlandırılabilir.

**Sol panel (2/3 genişlik):** KesitOnizlemeControl
- Seçilen kesitin çizgileri 2D olarak gösterilir
- Her çizgi role göre renkli, tanımsız = gri/kesikli
- Tıklanan çizgi vurgulanır (kalın + yanıp sönen)
- Zoom/Pan: mouse wheel + orta tuş sürükleme

**Sağ panel (1/3):** Rol atama
- Çizgi listesi (üstte seçilen çizginin bilgisi: layer, renk, uzunluk)
- Rol dropdown (CizgiRolu enum'undan)
- Rol renk paleti önizleme
- [Tümünü Otomatik Ata] → layer adından tahmin et
- [Kaydet] → ReferansKesitSablonu oluştur + JSON'a yaz
- [İptal]

### 5.3 KesitDogrulamaWindow (Modal Pencere — büyük)

Doğrulama adımında açılan bağımsız WPF Window.
Boyut: 1000×650 veya ekranın %75'i, boyutlandırılabilir.

**Üst toolbar:** Gezinme
- [◀ Önceki] [▶ Sonraki] butonları + km göstergesi
- Durum badge'leri: "71 onaylı | 8 uyarı | 5 sorunlu"
- Filtre: [Sadece sorunlu göster] checkbox
- [Tümünü Onayla] (tolerans içindekileri toplu onay)

**Sol panel (2/3):** KesitOnizlemeControl
- Mevcut kesitin çizgileri rolleriyle renkli
- Yanlış atanmış çizgiye tıkla → sağ panelde rol değiştir

**Sağ panel (1/3) üst:** Alan kıyası tablosu
| Malzeme | Hesap | Tablo | Fark | % |
|---------|-------|-------|------|---|
| Dolgu   | 63.8  | 64.04 | 0.24 | 0.4% |
| Sıyırma | 11.9  | 12.51 | 0.61 | 4.9% |

Renk kodlu: yeşil (<=2%), sarı (2-5%), kırmızı (>5%)

**Sağ panel (1/3) alt:** Aksiyon butonları
- [Onayla] → DogrulamaDurumu = Onaylandi
- [Sorunlu] → DogrulamaDurumu = Sorunlu + not girişi
- [Çizgi Düzelt] → rol değiştirme modu aktif

### 5.4 KesitOnizlemeControl (Paylaşılan UserControl)

Hem kalibrasyon hem doğrulama penceresinde kullanılan ortak kontrol.
WPF Canvas üzerinde 2D kesit çizimi.

**Render:**
- DWG koordinatlarını Canvas koordinatlarına dönüştür (fit-to-view)
- Her CizgiRolu için sabit renk paleti (proje kodundaki renklerle uyumlu)
- CL eksen çizgisi daima ortada
- Ölçek grid'i (opsiyonel açma/kapama)

**Etkileşim:**
- Mouse hover → çizgi bilgisi tooltip (layer, rol, uzunluk)
- Mouse click → çizgi seçimi (rol atama için event fırlatır)
- Mouse wheel → zoom in/out
- Orta tuş sürükleme → pan
- Sağ tık → bağlam menüsü (Rolü değiştir, Filtrele, Tümünü göster)

---

## 6. Mevcut Kodla Entegrasyon

### 6.1 Yeniden Kullanılan Sınıflar

| Mevcut Sınıf | Kullanım Yeri | Ne İçin |
|---|---|---|
| `YolKesitService.IstasyonParse()` | AnchorTaramaService | Km text parse |
| `YolKesitService.IstasyonFormatla()` | Genel | Km formatı |
| `EnKesitAlanService.IkiCizgiArasiAlan()` | KesitAlanHesapService | Alan hesabı |
| `EnKesitAlanService.ClipToXRange()` | KesitAlanHesapService | Çizgi kesme |
| `EnKesitAlanService.InterpolateY()` | KesitAlanHesapService | Y enterpolasyonu |
| `EnKesitAlanService.ShoelaceAlan()` | KesitAlanHesapService | Polygon alanı |
| `EnKesitAlanService.PolylineNoktalariniAl()` | KesitGruplamaService | Vertex çıkarma |
| `EnKesitAlanService.MalzemeAdiCikar()` | CizgiRolAtamaService | Layer → malzeme |
| `KatmanEslestirmeService` | CizgiRolAtamaService | Pattern eşleşme |
| `NumberParserHelper` | TabloOkumaService | Sayı parse |
| `YolKubajService` | ViewModel | Hacim hesabı |
| `ExcelExportService.YolMetrajExport()` | ViewModel | Excel çıktı |
| `MalzemeHatchAyarService` | ViewModel | Renk/hatch ayarları |

### 6.2 Mevcut Kodda Değişiklikler

- `EnKesitAlanService`: `IkiCizgiArasiAlan`, `ClipToXRange`, `InterpolateY`, `ShoelaceAlan` metodlarını **public** yap (bazıları private)
- `YolKesitService`: `IstasyonParse` ve `IstasyonFormatla` metodlarını **static** yap (bağımsız utility)
- `ServiceContainer`: Yeni servisleri DI'a kaydet (EnkesitOkuma servisleri)
- `RibbonManager`: Yeni ribbon butonu ekle ("Enkesit Oku")

### 6.3 Dokunulmayanlar

- `MetrajMainControl.xaml` — hiçbir değişiklik yok, tab eklenmeyecek
- Mevcut YolMetrajViewModel, YolKesitService (tıkla-işaretle sistemi) aynen kalır
- HatchOlusturmaService aynen kalır
- Enkesit Oku tamamen bağımsız PaletteSet, Metraj Asistanı ile etkileşimi yok

---

## 7. Geliştirme Sırası (Fazlar)

### Faz 1 — Temel Altyapı (Models + Anchor Tarama)
- [ ] Tüm model sınıflarını oluştur
- [ ] CizgiRolu enum'unu tanımla
- [ ] AnchorTaramaService: text tarama + km parse
- [ ] Unit testler: IstasyonParse, anchor sıralama, duplike kontrol
- **Çıktı:** "1247 entity seçildi, 84 istasyon text'i bulundu"

### Faz 2 — Pencere + Entity Toplama
- [ ] KesitPenceresi modeli
- [ ] KesitGruplamaService: pencere bazlı SelectCrossingWindow
- [ ] Polyline → CizgiTanimi dönüşümü
- [ ] Text obje ayrıştırma
- [ ] Unit testler: pencere hesabı, çakışma kontrolü
- **Çıktı:** "84 kesit grubu oluşturuldu, toplam 1890 çizgi"

### Faz 3 — Kalibrasyon (WPF Ekranı + Rol Atama)
- [ ] KesitOnizlemeControl: 2D Canvas render
- [ ] ReferansKesitWindow: rol atama UI
- [ ] CizgiRolAtamaService: kalibrasyon modu
- [ ] ReferansKesitSablonu JSON kaydet/yükle
- **Çıktı:** Kullanıcı bir kesitte rolleri tanımlayıp şablon kaydediyor

### Faz 4 — Otomatik Eşleşme + Alan Hesabı
- [ ] CizgiRolAtamaService: otomatik mod (şablondan)
- [ ] KesitAlanHesapService: çizgi çifti alan hesapları
- [ ] Yarma/Dolgu bölünme (kesişim noktası tespiti)
- [ ] B.T. Yerine Konan/Konmayan hesabı
- [ ] Unit testler: alan hesap doğrulaması
- **Çıktı:** Her kesit için malzeme alanları hesaplandı

### Faz 5 — Tablo Okuma + Kıyaslama
- [ ] TabloOkumaService: DWG text parse
- [ ] Tablo vs geometri kıyaslama
- [ ] Tolerans kontrolü (varsayılan %2)
- **Çıktı:** Fark raporu oluşturuluyor

### Faz 6 — Doğrulama Ekranı + Çıktı
- [ ] KesitDogrulamaWindow: ileri/geri gezinme
- [ ] Sorunlu kesit filtreleme
- [ ] Manuel düzeltme
- [ ] YolEnkesitOkumaViewModel: ana orkestrasyon
- [ ] Hacim hesabı (mevcut YolKubajService)
- [ ] Excel çıktı (mevcut ExcelExportService genişletilir)
- **Çıktı:** Kullanılabilir ürün

---

## 8. Önemli Teknik Detaylar

### 8.1 Koordinat Dönüşümü (DWG → WPF Canvas)

İhale enkesitleri genelde ölçekli çizilir. Y ekseni abartılı olabilir (1:100 veya 1:200). KesitOnizlemeControl'de:
- Tüm çizgilerin min/max X ve Y değerlerini bul
- Canvas'a fit-to-view dönüşüm matrisi uygula
- Aspect ratio'yu koru

### 8.2 Performans

1000 kesit x 20 entity = 20.000 entity. SelectCrossingWindow çağrısı kesit başına ~5ms = toplam ~5 saniye. Kabul edilebilir. Ama tüm entity'leri memory'de tutmak yerine, anchor listesi + pencere boyutu saklanır, entity'ler lazım olduğunda (doğrulama ekranında gezinirken) on-demand yüklenir.

### 8.3 JSON Kayıt Formatı

ReferansKesitSablonu ve TopluTaramaSonucu JSON olarak kaydedilir (mevcut YolMetrajKayitVerisi gibi). Böylece:
- Bir projenin şablonu başka projeye uygulanabilir
- Yarım kalan tarama devam ettirilebilir
- Sonuçlar paylaşılabilir

### 8.4 Hata Toleransları

| Kontrol | Tolerans | Aksiyon |
|---------|----------|---------|
| Alan kıyası (geometri vs tablo) | %2 | Uyumlu |
| Alan kıyası | %2-5 | Uyarı (sarı) |
| Alan kıyası | >%5 | Sorunlu (kırmızı) |
| Anchor duplike mesafesi | <1m | Birleştir |
| Minimum çizgi uzunluğu | <0.1m | Filtrele (grid çizgisi olabilir) |

---

## 9. AutoCAD Komutları

| Komut | Açıklama |
|-------|----------|
| YOLENKESITOKU | Enkesit Oku panelini aç/kapat (ayrı PaletteSet) |
| YOLKALIBRE | Hızlı kalibrasyon (referans kesit tanımla) |
| YOLTARA | Hızlı toplu tarama (entity seç + tara + hesapla) |
| YOLDOGRULA | Doğrulama ekranını aç |

Not: Bu komutlar mevcut Metraj Asistanı komutlarından (METRAJ, METRAJALAN vb.) tamamen bağımsızdır. Ayrı IExtensionApplication veya aynı DLL içinde ayrı CommandClass olarak implemente edilir.
