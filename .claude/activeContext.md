# Active Context — Yol Enkesit Okuma Modülü

## Şu An Ne Yapıyoruz?
Metraj-master projesine "Yol Enkesit Okuma" modülü eklendi. Bu modül ihale DWG dosyalarından enkesit verilerini yarı-otomatik okuyarak alan hesabı yapar ve firmanın tablolarıyla kıyaslar.

## Durum: TÜM FAZLAR TAMAMLANDI

## Faz 1 — Temel Altyapı (Models + Anchor Tarama) ✓
- [x] Models/YolEnkesit/ klasörü oluşturuldu (9 dosya)
- [x] CizgiRolu enum + DogrulamaDurumu enum
- [x] AnchorNokta, KesitPenceresi, CizgiTanimi, ReferansKesitSablonu, KesitGrubu
- [x] AlanHesapSonucu, TabloKiyasSonucu, TopluTaramaSonucu
- [x] IAnchorTaramaService + AnchorTaramaService
- [x] YolKesitService.IstasyonParseStatik() → public static eklendi
- [x] YolKesitService.IstasyonFormatla() → public static yapıldı
- [x] EnKesitAlanService: ClipToXRange, InterpolateY, ShoelaceAlan → public yapıldı
- [x] IEnKesitAlanService: 3 yeni metod eklendi
- [x] AnchorTaramaTests.cs — 22 test başarılı

## Faz 2 — Pencere + Entity Toplama ✓
- [x] IKesitGruplamaService + KesitGruplamaService
- [x] Pencere bazlı entity toplama (SelectCrossingWindow mantığı)
- [x] Polyline → CizgiTanimi dönüşümü
- [x] Text obje ayrıştırma

## Faz 3 — Kalibrasyon (WPF Ekranı + Rol Atama) ✓
- [x] ICizgiRolAtamaService + CizgiRolAtamaService (kalibrasyon + otomatik mod)
- [x] KesitOnizlemeControl: 2D Canvas render, zoom/pan, çizgi seçimi
- [x] ReferansKesitWindow: rol atama UI, şablon kaydet/yükle
- [x] ReferansKesitViewModel

## Faz 4 — Otomatik Eşleşme + Alan Hesabı ✓
- [x] IKesitAlanHesapService + KesitAlanHesapService
- [x] Yarma/Dolgu bölünme (kesişim noktası ile ikili arama)
- [x] B.T. Yerine Konan/Konmayan hesabı
- [x] Üstyapı tabaka hesapları (Aşınma→Kırmataş)

## Faz 5 — Tablo Okuma + Kıyaslama ✓
- [x] ITabloOkumaService + TabloOkumaService
- [x] DWG text parse (malzeme anahtar kelimeleri)
- [x] Tablo vs geometri kıyaslama (%2/%5 tolerans)

## Faz 6 — Doğrulama Ekranı + Çıktı ✓
- [x] KesitDogrulamaWindow: ileri/geri gezinme, filtre, toplu onay
- [x] KesitDogrulamaViewModel
- [x] EnkesitOkuMainControl: 6 adımlı wizard akışı
- [x] EnkesitOkumaMainViewModel: ana orkestrasyon
- [x] EnkesitOkuCommands: YOLENKESITOKU, YOLKALIBRE, YOLTARA, YOLDOGRULA
- [x] EnkesitOkuPaletteManager: bağımsız pencere yönetimi
- [x] ServiceContainer: 5 servis + 3 ViewModel DI kaydı
- [x] RibbonManager: "Enkesit Oku" butonu eklendi
- [x] ExcelExportService.EnkesitOkuma.cs: Excel export
- [x] IExcelExportService: EnkesitOkumaExport metodu eklendi
- [x] MetrajCommands: Initialize/Terminate entegrasyonu

## Oluşturulan Dosyalar (toplam 30+)
### Models/YolEnkesit/ (9 dosya)
AnchorNokta.cs, AlanHesapSonucu.cs, CizgiRolu.cs, CizgiTanimi.cs, KesitGrubu.cs, KesitPenceresi.cs, ReferansKesitSablonu.cs, TabloKiyasSonucu.cs, TopluTaramaSonucu.cs

### Services/YolEnkesit/ (10 dosya)
IAnchorTaramaService.cs, AnchorTaramaService.cs, IKesitGruplamaService.cs, KesitGruplamaService.cs, ICizgiRolAtamaService.cs, CizgiRolAtamaService.cs, IKesitAlanHesapService.cs, KesitAlanHesapService.cs, ITabloOkumaService.cs, TabloOkumaService.cs

### ViewModels/EnkesitOkuma/ (3 dosya)
EnkesitOkumaMainViewModel.cs, ReferansKesitViewModel.cs, KesitDogrulamaViewModel.cs

### Views/EnkesitOkuma/ (8 dosya)
EnkesitOkuMainControl.xaml/.cs, KesitOnizlemeControl.xaml/.cs, ReferansKesitWindow.xaml/.cs, KesitDogrulamaWindow.xaml/.cs

### Commands/ (2 dosya)
EnkesitOkuCommands.cs, EnkesitOkuPaletteManager.cs

### Diğer
ExcelExportService.EnkesitOkuma.cs, AnchorTaramaTests.cs, docs/YolEnkesitOkuma_Plan.md
