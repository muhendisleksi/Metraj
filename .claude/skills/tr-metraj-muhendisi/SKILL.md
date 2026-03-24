---
name: tr-metraj-muhendisi
description: >
  Metraj (ölçü alma) asistanı. AutoCAD ve Civil 3D çizimlerinden uzunluk, alan,
  hacim ve metin toplama hesapları yapan eklenti geliştirme rehberi. Kullanıcı
  metraj hesabı, kübaj, en kesit hacmi, ortalama alan metodu, prismoidal formül,
  Bruckner diyagramı, birim dönüşüm (m², ha, dönüm, m³) veya AutoCAD obje ölçümü
  sorduğunda tetiklenir. Türkçe sayı formatı (virgül ondalık) kurallarını içerir.
---

# TR Metraj Mühendisi

AutoCAD/Civil 3D çizimlerinden ölçü alma (metraj) uzmanı. Uzunluk, alan, hacim
hesapları ve metin toplama işlemleri konusunda derin bilgi sahibi.

## Desteklenen Ölçüm Tipleri

### 1. Uzunluk Hesabı
**AutoCAD nesneleri:**
- `Line` → `StartPoint` - `EndPoint` mesafesi
- `Polyline` / `LWPolyline` → `Length` property
- `Polyline3d` → `Length` property (3B uzunluk)
- `Arc` → `Length` property
- `Circle` → `Circumference` (çevre)
- `Spline` → `GetDistAtParameter(EndParam)`
- `Ellipse` → yaklaşık çevre formülü

**Civil 3D nesneleri (runtime tespitli):**
- `FeatureLine` → `MaxPointElevation` arası 3B uzunluk
- `Alignment` → `Length` property

**Gruplama:** Katman, Renk, Nesne Tipi veya Grupsuz

### 2. Alan Hesabı
**AutoCAD nesneleri:**
- Kapalı `Polyline` → `Area` property
- `Circle` → `Area` property
- `Ellipse` → `Area` property
- `Hatch` → döngü alanları toplamı
- `Region` → `Area` property

**Civil 3D nesneleri:**
- `TinSurface` sınır alanı
- `Parcel` alanı

**Birim dönüşüm:**
| Kaynak | Hedef | Formül |
|--------|-------|--------|
| m² | hektar (ha) | / 10000 |
| m² | dönüm | / 1000 |
| m² | ar | / 100 |

### 3. Hacim/Kübaj Hesabı

**Ortalama Alan Metodu (Average End Area):**
```
V = (A₁ + A₂) / 2 × L
```
- A₁, A₂: ardışık en kesit alanları
- L: istasyonlar arası mesafe

**Prismoidal Formül:**
```
V = L/6 × (A₁ + 4×Am + A₂)
```
- Am: orta istasyondaki alan (enterpolasyon)

**Bruckner Diyagramı:**
- Her istasyondaki kümülatif hacim
- Kazı (+), dolgu (-) olarak işaretlenir
- Taşıma mesafesi optimizasyonu için kullanılır

**Civil 3D hacim:**
- `TinVolumeSurface` — iki yüzey arasındaki hacim
- Net kazı / net dolgu ayrımı

### 4. Metin Toplama
- `DBText` ve `MText` nesnelerinden sayı ayrıştırma
- Türkçe format: `1.234,56` (nokta binlik, virgül ondalık)
- İngilizce format: `1,234.56`
- Ön ek / son ek filtreleme: "A=" → sadece "A=" ile başlayan metinler

## Türkçe Sayı Formatı Kuralları

```
Girdi: "1.234,56"   → Çıktı: 1234.56 (double)
Girdi: "1234,56"    → Çıktı: 1234.56
Girdi: "1,234.56"   → Çıktı: 1234.56 (İngilizce algıla)
Girdi: "1234.56"    → Çıktı: 1234.56
Girdi: "A=125,30"   → Çıktı: 125.30 (ön ek temizle)
Girdi: "45.6 m²"    → Çıktı: 45.6 (son ek temizle)
```

## Layer İsimlendirme Kuralları

| Layer | Renk | Kullanım |
|-------|------|----------|
| METRAJ-UZUNLUK | Cyan (4) | Uzunluk annotasyonları |
| METRAJ-ALAN | Green (3) | Alan annotasyonları |
| METRAJ-HACIM | Magenta (6) | Hacim annotasyonları |
| METRAJ-TOPLAMA | Yellow (2) | Toplama annotasyonları |
| METRAJ-ETIKET | White (7) | Genel etiketler |

## Kodlama Kuralları

- Domain terimleri Türkçe: `Uzunluk`, `Alan`, `Hacim`, `Istasyon`, `Enkesit`, `Toplama`
- Programlama terimleri İngilizce: Service, ViewModel, Command
- Her sınıf max 1000 satır → partial class'a böl
- AutoCAD işlemleri mutlaka Transaction içinde
- Civil 3D kodu `.Civil3d.cs` partial dosyalarında izole
- Nullable: disable, ImplicitUsings: disable
